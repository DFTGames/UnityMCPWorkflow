// The browser half of signing in with itch.io (GDD "Scoring", Leaderboards). Only the web build has this:
// it is the one build Unity Player Accounts cannot serve, and the one where a popup is the way to reach an
// identity provider at all.
//
// The game cannot navigate itself to itch and come back, because it *is* the page: a redirect would tear the
// running game down and start it again with nothing to resume. So the sign-in happens in a second window and
// the answer comes back by postMessage.
//
// Every answer goes to the same C# method, as one string beginning with a word that says which kind it is.
// One channel rather than four keeps the ordering honest: the window closing and the message arriving race
// each other, and with separate callbacks the loser used to be reported on top of the winner.

mergeInto(LibraryManager.library, {

  // url            the itch.io page to open, built and escaped in Core
  // listener       the GameObject name that receives the answer
  // calledBack     the origin of our hosted callback page; messages from anywhere else are ignored
  YassItchOpenSignIn: function (url, listener, calledBack) {
    var page = UTF8ToString(url);
    var target = UTF8ToString(listener);
    var trusted = UTF8ToString(calledBack);

    // A second attempt while the first is still open would leave two windows and two listeners racing to
    // answer. The existing window is brought forward instead, which is also what the player meant.
    if (window.yassItchPending) {
      try { window.yassItchPending.popup.focus(); } catch (ignored) { }
      // Answered rather than left silent. Returning without a word left the C# caller awaiting its full
      // five minute deadline for a window it was never going to be told about.
      SendMessage(target, "OnItchAnswer", "busy");
      return;
    }

    var answered = false;
    var watch = null;

    function answer(text) {
      if (answered) return;
      answered = true;

      window.removeEventListener("message", onMessage);
      if (watch) { clearInterval(watch); }
      window.yassItchPending = null;

      try { if (popup && !popup.closed) { popup.close(); } } catch (ignored) { }

      SendMessage(target, "OnItchAnswer", text);
    }

    function onMessage(event) {
      // The origin check is the whole security of this listener. Without it any page that could get a
      // message into this window could hand the game a token of its choosing, and the game would sign in
      // with it. The callback page is ours and is served from one origin; nothing else is listened to.
      if (event.origin !== trusted) return;

      // And that it came from the window we opened. The callback page is served from a shared host (a
      // GitHub Pages account serves every one of its projects from one origin), so the origin alone does
      // not single it out.
      if (popup && event.source !== popup) return;

      var data = event.data;
      if (!data || data.source !== "yass-itch-oauth" || typeof data.fragment !== "string") return;

      answer("token " + data.fragment);
    }

    window.addEventListener("message", onMessage);

    // Sized to itch's sign-in page and placed over the middle of the game rather than the screen, because
    // on a second monitor the screen's middle is somewhere the player is not looking.
    var width = 520;
    var height = 720;
    var left = window.screenX + Math.max(0, (window.outerWidth - width) / 2);
    var top = window.screenY + Math.max(0, (window.outerHeight - height) / 2);

    var popup = null;
    try {
      popup = window.open(
        page, "yass-itch-signin",
        "width=" + width + ",height=" + height + ",left=" + left + ",top=" + top +
        ",menubar=no,toolbar=no,location=yes,status=no,resizable=yes,scrollbars=yes"
      );
    } catch (ignored) {
      popup = null;
    }

    // itch.io embeds games in a sandboxed iframe, and a sandbox without allow-popups blocks this outright.
    // Saying so plainly matters: the game can then offer a plain link instead, which is a worse experience
    // but a working one, rather than a button that silently does nothing.
    if (!popup) {
      answer("blocked");
      return;
    }

    watch = setInterval(function () {
      if (!popup.closed) return;

      // Closed without a message. Almost always the player changing their mind; occasionally the message is
      // still in flight, so the answer is delayed a moment rather than sent the instant the window goes.
      setTimeout(function () { answer("cancelled"); }, 400);
      clearInterval(watch);
      watch = null;
    }, 500);

    window.yassItchPending = { popup: popup, close: function () { answer("cancelled"); } };
  },

  // Called when the game gives up waiting, so a window nobody is listening to is not left open over the game.
  YassItchCancelSignIn: function () {
    if (window.yassItchPending) { window.yassItchPending.close(); }
  },

  // Whether this build can open a popup at all, as far as can be told without trying. Used to decide whether
  // to offer the button; the real answer only arrives when it is pressed.
  YassItchCanOpenWindows: function () {
    return typeof window !== "undefined" && typeof window.open === "function" ? 1 : 0;
  }
});
