const axios = require("axios");

// Signs a player in to Unity Gaming Services with their itch.io account (GDD "Scoring", Leaderboards).
//
// WHY THIS EXISTS AT ALL
// Unity Player Accounts has no browser implementation on WebGL, so the web build could offer no sign-in and
// its players were stuck on local boards. itch.io can identify them, but Unity Authentication is explicit
// that a custom identity cannot be established from a game client: "it needs to rely on a server-authoritative
// backend and cannot allow game clients to request their own tokens." This is that backend. It lives in
// Cloud Code so the project needs nothing else hosted, and so the service account key never leaves UGS.
//
// WHY THE CLIENT'S CLAIM IS NEVER BELIEVED
// The itch token arrives from a browser, where anybody can type anything. It is not an identity until itch
// itself says whose it is, which is the first thing this does. Everything after that point uses the id itch
// returned and never a value the client sent. If this ever trusted the client's own idea of who it was, any
// player could file scores as any other.
//
// WHAT THE CALLER MUST ALREADY BE
// Signed in, as anybody, because Cloud Code cannot be called otherwise. The client signs in anonymously
// first purely to get through that door. That throwaway player is put to use below rather than discarded.

const ITCH_PROFILE = "https://api.itch.io/profile";
const TOKEN_EXCHANGE = "https://services.api.unity.com/auth/v1/token-exchange";
const PLAYER_AUTH = "https://player-auth.services.api.unity.com/v1/projects";

// The service account key, as "<KEY_ID>:<SECRET_KEY>". Held in Secret Manager, never in this file and never
// in the built game: it can mint a session for any player id, so a copy of it in a WebGL build would let
// anybody sign in as anybody.
const SECRET_NAME = "ITCH_SIGNIN_SERVICE_ACCOUNT";

// Long enough for a service having a slow minute, short enough that a player is not left staring at a
// spinner. The client has its own, longer deadline behind this one.
const TIMEOUT_MS = 8000;

module.exports = async ({ params, context, logger, secretManager }) => {
  const { projectId, environmentId, environmentName, accessToken } = context;

  const itchUser = await whoItchSaysThisIs(params.itchToken, logger);
  if (!itchUser) {
    // Deliberately the same answer for an expired token, a revoked one and a made-up one. Telling a caller
    // which of those it was tells somebody probing exactly how close they got.
    return { ok: false, reason: "itch-refused" };
  }

  const serviceToken = await statelessToken(secretManager, projectId, environmentId, logger);
  if (!serviceToken) return { ok: false, reason: "backend-misconfigured" };

  // The id, not the username. Names on itch can be changed; the id cannot, and it is what keeps a player's
  // scores theirs across a rename. Prefixed so this can never collide with another kind of id added later.
  const externalId = `itch:${itchUser.id}`;

  const tokens = await signIn(serviceToken, projectId, environmentName, externalId, accessToken, logger);
  if (!tokens) return { ok: false, reason: "sign-in-failed" };

  return {
    ok: true,
    idToken: tokens.idToken,
    sessionToken: tokens.sessionToken,
    // True only when this itch account had never been attached to a player before. The client takes the
    // itch handle as the pilot name on exactly that occasion: a returning player has had every chance to
    // choose their own, and overwriting it on each sign-in would undo that choice every time.
    linked: tokens.linked,
    // The handle the boards will show. Cleaned here as well as on the client, because this is the side that
    // cannot be edited by whoever is holding the game.
    pilotName: pilotNameFrom(itchUser)
  };
};

module.exports.params = { itchToken: "String" };

/**
 * Spends the token against itch's own API. This is the only step that establishes an identity: everything
 * downstream uses what itch returned here, never what the caller claimed.
 */
async function whoItchSaysThisIs(itchToken, logger) {
  if (typeof itchToken !== "string" || itchToken.length === 0 || itchToken.length > 512) return null;

  try {
    const answer = await axios.get(ITCH_PROFILE, {
      headers: { Authorization: `Bearer ${itchToken}` },
      timeout: TIMEOUT_MS,
      validateStatus: (status) => status === 200
    });

    const user = answer.data && answer.data.user;
    // The id is the whole point of the call, so a profile without one is not a profile.
    if (!user || (typeof user.id !== "number" && typeof user.id !== "string")) return null;

    return user;
  } catch (err) {
    // The token is never logged, not even in part. A log line is a place a credential can end up living.
    logger.info("itch.io would not identify this token", { status: err.response ? err.response.status : 0 });
    return null;
  }
}

/** Trades the service account key for a short-lived token that the player-auth API will accept. */
async function statelessToken(secretManager, projectId, environmentId, logger) {
  let key;
  try {
    const secret = await secretManager.getSecret(SECRET_NAME);
    key = secret.value;
  } catch (err) {
    // A deployment that has not had its secret set yet. Worth saying loudly, because from the player's side
    // it is indistinguishable from itch being down, and somebody would otherwise go looking at itch.
    logger.error("The itch sign-in service account secret is missing", { "error.message": err.message });
    return null;
  }

  try {
    const answer = await axios.post(
      `${TOKEN_EXCHANGE}?projectId=${projectId}&environmentId=${environmentId}`,
      {},
      {
        headers: { Authorization: `Basic ${Buffer.from(key).toString("base64")}` },
        timeout: TIMEOUT_MS
      }
    );

    return answer.data && answer.data.accessToken ? answer.data.accessToken : null;
  } catch (err) {
    logger.error("Could not exchange the service account key", {
      status: err.response ? err.response.status : 0
    });
    return null;
  }
}

/**
 * Establishes the player. Tried twice on purpose, and the order matters.
 *
 * First with the caller's own access token, which *links* this itch account to the player they already are.
 * That is what lets somebody who has been playing the web build anonymously keep the scores they have already
 * set when they finally sign in, instead of being handed an empty new identity for their trouble.
 *
 * If that is *refused* (a 4xx), it is because this itch account is already attached to a different player:
 * they have signed in before, on another machine or in another browser. Then the right answer is to become
 * that player, which is the plain call below.
 *
 * Only a refusal falls through. An outage or a timeout must not, because the plain call would then create a
 * second player for the same itch id and the first one's scores would be unreachable for ever.
 */
async function signIn(serviceToken, projectId, environmentName, externalId, playerToken, logger) {
  const url = `${PLAYER_AUTH}/${projectId}/authentication/server/custom-id`;
  const options = {
    headers: {
      Authorization: `Bearer ${serviceToken}`,
      "Content-Type": "application/json",
      UnityEnvironment: environmentName
    },
    timeout: TIMEOUT_MS
  };

  if (playerToken) {
    try {
      const linked = await axios.post(url, { externalId, accessToken: playerToken }, options);
      // The flag goes last so the response cannot supply its own and overrule what we know.
      if (linked.data && linked.data.idToken) return Object.assign({}, linked.data, { linked: true });
    } catch (err) {
      const status = err.response ? err.response.status : 0;

      // **Only a refusal falls through.** A 4xx means this itch account already belongs to another player,
      // which is the ordinary case for somebody signing in from a second browser, and becoming that player
      // is right. A 5xx or a timeout means nothing of the sort, and treating it the same way would create a
      // *second* player for this itch id and strand the first one's banked scores for ever: the external id
      // would resolve to the new player from then on, so the old one could never be reached again. There is
      // no repairing that afterwards, so an outage has to fail loudly instead.
      if (status < 400 || status >= 500) {
        logger.error("Could not link this itch account to the current player", { status });
        return null;
      }

      logger.info("This itch account belongs to an existing player; signing in as them instead", { status });
    }
  }

  try {
    const fresh = await axios.post(url, { externalId }, options);
    return fresh.data && fresh.data.idToken ? Object.assign({}, fresh.data, { linked: false }) : null;
  } catch (err) {
    logger.error("Could not sign the player in with their itch account", {
      status: err.response ? err.response.status : 0
    });
    return null;
  }
}

/**
 * The name the boards will show. The username rather than the display name: it is the handle the player is
 * known by on itch, it is unique there, and it is already restricted to characters a board can print, which
 * a freely typed display name is not.
 *
 * Whitespace is stripped because Unity Authentication refuses any name containing it, and that refusal would
 * arrive in the middle of submitting a score.
 */
function pilotNameFrom(user) {
  const raw = typeof user.username === "string" && user.username.length > 0 ? user.username : "Pilot";

  // 50 is Unity Authentication's documented limit, and 3 its practical floor; both are
  // Leaderboards.MaxNameLength / Credentials.MinNameLength on the C# side, which this cannot reference.
  // A handle too short to be accepted is dropped rather than sent, because the rename would be refused in
  // the middle of a sign-in and the player would keep a generated name without being told why.
  const cleaned = raw.replace(/\s+/g, "").slice(0, 50);
  return cleaned.length >= 3 ? cleaned : "Pilot";
}
