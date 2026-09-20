using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace YASS.Editor
{
    /// <summary>
    /// Builds the menu widgets used by the scene builders, in one place so every screen looks the same
    /// (GDD "Art Direction", Colour palette). Editor-only: the menus are authored into scenes, not spawned
    /// at run time.
    /// </summary>
    public static class MenuUiFactory
    {
        public static readonly Color Background = new Color32(0x12, 0x0A, 0x1E, 0xFF);
        public static readonly Color Panel = new Color32(0x1E, 0x10, 0x33, 0xF2);
        public static readonly Color Text = new Color32(0xF2, 0xE9, 0xFF, 0xFF);
        public static readonly Color Accent = new Color32(0xFF, 0x4F, 0xC3, 0xFF);
        public static readonly Color Secondary = new Color32(0xFF, 0xB1, 0x3B, 0xFF);
        public static readonly Color ButtonNormal = new Color32(0x2A, 0x1A, 0x47, 0xFF);
        public static readonly Color ButtonHighlighted = new Color32(0x46, 0x24, 0x6E, 0xFF);
        public static readonly Color ButtonPressed = new Color32(0xFF, 0x4F, 0xC3, 0xFF);
        public static readonly Color ButtonSelected = new Color32(0x5B, 0x2E, 0x8C, 0xFF);
        public static readonly Color ButtonDisabled = new Color32(0x24, 0x1A, 0x33, 0x80);

        public const float ButtonWidth = 420f;
        public const float ButtonHeight = 72f;
        public const int TitleFontSize = 96;
        public const int HeadingFontSize = 56;
        public const int BodyFontSize = 32;
        public const int ButtonFontSize = 34;

        public static Sprite UiSprite =>
            UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        public static Sprite BackgroundSprite =>
            UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");

        public static Sprite KnobSprite =>
            UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

        public static Sprite CheckmarkSprite =>
            UnityEditor.AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd");

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        /// <summary>A full-screen panel, stretched to its parent.</summary>
        public static RectTransform CreatePanel(string name, Transform parent, Color colour, bool raycastTarget = true)
        {
            var rect = CreateRect(name, parent);
            Stretch(rect);

            var image = rect.gameObject.AddComponent<Image>();
            image.color = colour;
            image.raycastTarget = raycastTarget;
            return rect;
        }

        /// <summary>A column of widgets, centred, laid out top to bottom.</summary>
        public static RectTransform CreateColumn(string name, Transform parent, float spacing = 18f)
        {
            var rect = CreateRect(name, parent);
            Stretch(rect);

            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = false;   // widths come from each child's own rect
            layout.childControlHeight = true;   // heights come from each child's preferred height
            layout.padding = new RectOffset(80, 80, 60, 60);
            return rect;
        }

        public static TMP_Text CreateText(string name, Transform parent, string content, int fontSize, Color colour,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center, float width = 900f, float height = 0f)
        {
            var rect = CreateRect(name, parent);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = fontSize;
            text.color = colour;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;

            // TextMeshPro reports its own preferred height, which the column reads: a wrapped or two-line
            // string is given the room it needs instead of overlapping whatever follows it.
            rect.sizeDelta = new Vector2(width, height > 0f ? height : fontSize * 1.6f);
            if (height > 0f) Fix(rect, height);

            return text;
        }

        public static Button CreateButton(string name, Transform parent, string label)
        {
            var rect = CreateRect(name, parent);
            rect.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);

            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = UiSprite;
            image.type = Image.Type.Sliced;
            image.color = Color.white; // the tint carries the palette colour, so every state value stays within 0..1

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = new ColorBlock
            {
                normalColor = ButtonNormal,
                highlightedColor = ButtonHighlighted,
                pressedColor = ButtonPressed,
                selectedColor = ButtonSelected,
                disabledColor = ButtonDisabled,
                colorMultiplier = 1f,
                fadeDuration = 0.1f
            };

            var text = CreateText("Label", rect, label, ButtonFontSize, Text);
            Stretch((RectTransform)text.transform);
            text.alignment = TextAlignmentOptions.Center;

            Fix(rect, ButtonHeight);
            return button;
        }

        /// <summary>A labelled row: caption on the left, widget on the right.</summary>
        public static RectTransform CreateRow(string name, Transform parent, string caption, out RectTransform slot)
        {
            var row = CreateRect(name, parent);
            row.sizeDelta = new Vector2(760f, 60f);

            var label = CreateText("Caption", row, caption, BodyFontSize, Text, TextAlignmentOptions.Left, 360f, 60f);
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = new Vector2(0f, 0.5f);
            labelRect.anchorMax = new Vector2(0f, 0.5f);
            labelRect.pivot = new Vector2(0f, 0.5f);
            labelRect.anchoredPosition = Vector2.zero;

            slot = CreateRect("Widget", row);
            slot.anchorMin = new Vector2(1f, 0.5f);
            slot.anchorMax = new Vector2(1f, 0.5f);
            slot.pivot = new Vector2(1f, 0.5f);
            slot.sizeDelta = new Vector2(360f, 44f);
            slot.anchoredPosition = Vector2.zero;

            Fix(row, 60f);
            return row;
        }

        public static Slider CreateSlider(string name, RectTransform slot)
        {
            var rect = CreateRect(name, slot);
            Stretch(rect);

            var background = CreateRect("Background", rect);
            Stretch(background);
            background.sizeDelta = new Vector2(0f, 14f);
            background.anchorMin = new Vector2(0f, 0.5f);
            background.anchorMax = new Vector2(1f, 0.5f);
            var backgroundImage = background.gameObject.AddComponent<Image>();
            backgroundImage.sprite = BackgroundSprite;
            backgroundImage.type = Image.Type.Sliced;
            backgroundImage.color = ButtonNormal;

            var fillArea = CreateRect("Fill Area", rect);
            Stretch(fillArea);
            fillArea.anchorMin = new Vector2(0f, 0.5f);
            fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.sizeDelta = new Vector2(-20f, 14f);

            var fill = CreateRect("Fill", fillArea);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.sizeDelta = new Vector2(20f, 0f);
            var fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.sprite = UiSprite;
            fillImage.type = Image.Type.Sliced;
            fillImage.color = Accent;

            var handleArea = CreateRect("Handle Slide Area", rect);
            Stretch(handleArea);
            handleArea.sizeDelta = new Vector2(-20f, 0f);

            var handle = CreateRect("Handle", handleArea);
            handle.sizeDelta = new Vector2(28f, 44f);
            handle.anchorMin = Vector2.zero;
            handle.anchorMax = new Vector2(0f, 1f);
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.sprite = KnobSprite;
            handleImage.color = Secondary;

            var slider = rect.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            slider.colors = SelectableColours(Secondary);
            return slider;
        }

        public static Toggle CreateToggle(string name, RectTransform slot)
        {
            var rect = CreateRect(name, slot);
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(44f, 44f);
            rect.anchoredPosition = Vector2.zero;

            var backgroundRect = CreateRect("Background", rect);
            Stretch(backgroundRect);
            var background = backgroundRect.gameObject.AddComponent<Image>();
            background.sprite = UiSprite;
            background.type = Image.Type.Sliced;
            background.color = ButtonNormal;

            var checkRect = CreateRect("Checkmark", backgroundRect);
            Stretch(checkRect);
            checkRect.sizeDelta = new Vector2(-10f, -10f);
            var check = checkRect.gameObject.AddComponent<Image>();
            check.sprite = CheckmarkSprite;
            check.color = Secondary;

            var toggle = rect.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = background;
            toggle.graphic = check;
            toggle.isOn = true;
            toggle.colors = SelectableColours(ButtonNormal);
            return toggle;
        }

        /// <summary>A handle colour that still reads against the track, used for sliders.</summary>
        public static ColorBlock HandleColours(Color baseColour) => SelectableColours(baseColour);

        /// <summary>Pins an element's height for the column, which otherwise sizes it from its content.</summary>
        static void Fix(RectTransform rect, float height)
        {
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static ColorBlock SelectableColours(Color baseColour) => new ColorBlock
        {
            normalColor = baseColour,
            highlightedColor = Lighten(baseColour, 0.12f),
            pressedColor = ButtonPressed,
            selectedColor = Lighten(baseColour, 0.2f),
            disabledColor = ButtonDisabled,
            colorMultiplier = 1f,
            fadeDuration = 0.1f
        };

        static Color Lighten(Color colour, float amount) => Color.Lerp(colour, Color.white, amount);

    }
}
