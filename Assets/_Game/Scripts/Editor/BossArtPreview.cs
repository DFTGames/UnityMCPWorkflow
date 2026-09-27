using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using YASS.Gameplay;

namespace YASS.Editor
{
    /// <summary>
    /// Shows a boss's body art while its prefab is open, so the engines and the beam can be placed against
    /// the ship they belong to rather than against an empty space.
    /// </summary>
    /// <remarks>
    /// **The prefab deliberately has no sprite in it, and must not gain one.** A boss's art is the heaviest
    /// thing in the game and is held by path, not by reference (`BossDefinition.SpritePath`, loaded through
    /// the runner's `BossContent` cache): the whole reason it was taken out of the shared atlas was that one
    /// reference pulls every boss's art into the dependency graph behind whatever points at the prefab. So
    /// the body renderer is empty at rest, which is correct, and which also left nothing on screen to aim at
    /// when positioning the children.
    ///
    /// This puts the art back for the editor only. The preview object carries <see cref="HideFlags.DontSave"/>,
    /// so saving the prefab cannot write it into the asset: the reference exists in memory while the stage is
    /// open and nowhere else. <c>BossPrefabArtTests</c> is the belt to this braces, and fails if a boss prefab
    /// is ever committed with a body sprite in it.
    ///
    /// It is drawn one sorting order below the body's own, so the children being positioned stay in front of
    /// it rather than disappearing behind a reference image.
    /// </remarks>
    [InitializeOnLoad]
    static class BossArtPreview
    {
        /// <summary>Named with brackets so it is obviously not part of the prefab if it is ever seen.</summary>
        const string PreviewName = "(boss art preview)";

        static BossArtPreview()
        {
            PrefabStage.prefabStageOpened += Show;
        }

        static void Show(PrefabStage stage)
        {
            if (stage == null || stage.prefabContentsRoot == null) return;

            var boss = stage.prefabContentsRoot.GetComponent<BossView>();
            if (boss == null) return;

            var definition = boss.Definition;
            if (definition == null || string.IsNullOrEmpty(definition.SpritePath)) return;

            var art = Resources.Load<Sprite>(definition.SpritePath);
            if (art == null)
            {
                Debug.LogWarning($"{nameof(BossArtPreview)}: no art at Resources/{definition.SpritePath} " +
                                 $"for {stage.prefabContentsRoot.name}.");
                return;
            }

            var body = stage.prefabContentsRoot.GetComponent<SpriteRenderer>();

            var preview = new GameObject(PreviewName)
            {
                // The one line that keeps this out of the asset. Without it, saving the prefab after nudging
                // an engine would quietly commit a reference to the boss's art and undo the work that took
                // that art out of the atlas in the first place.
                hideFlags = HideFlags.DontSave | HideFlags.NotEditable
            };

            preview.transform.SetParent(stage.prefabContentsRoot.transform, false);

            var renderer = preview.AddComponent<SpriteRenderer>();
            renderer.sprite = art;
            renderer.sortingLayerID = body != null ? body.sortingLayerID : 0;
            renderer.sortingOrder = (body != null ? body.sortingOrder : 1) - 1;
        }
    }
}
