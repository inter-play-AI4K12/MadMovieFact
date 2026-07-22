using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MadFact
{
    /// <summary>
    /// Shared uGUI construction and layout helpers. Authored prefabs are the normal
    /// production path; these methods also support the bootstrap's empty-scene fallback.
    /// </summary>
    public static class UIFactory
    {
        // ---- RectTransform helpers ----------------------------------------
        public static GameObject Node(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        public static RectTransform RT(GameObject go) => (RectTransform)go.transform;

        public static T FindDeep<T>(Transform root, string objectName) where T : Component
        {
            // Include inactive level panels because their controls are rebound before opening.
            foreach (var component in root.GetComponentsInChildren<T>(true))
                if (component.gameObject.name == objectName) return component;
            return null;
        }

        /// <summary>Anchor + stretch to fill parent with padding (l,t,r,b).</summary>
        public static RectTransform Fill(RectTransform rt, float l = 0, float t = 0, float r = 0, float b = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(-r, -t);
            return rt;
        }

        /// <summary>Place with explicit anchor/pivot/size/pos.</summary>
        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 size, Vector2 pos)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            return rt;
        }

        // ---- Graphics ------------------------------------------------------
        public static Image Image(Transform parent, string name, Color color, Sprite sprite = null,
            Image.Type type = UnityEngine.UI.Image.Type.Simple, bool raycast = true)
        {
            var go = Node(parent, name);
            var img = go.AddComponent<Image>();
            img.color = color;
            img.sprite = sprite != null ? sprite : Theme.Solid;
            img.type = type;
            if (type == UnityEngine.UI.Image.Type.Tiled || type == UnityEngine.UI.Image.Type.Sliced)
                img.pixelsPerUnitMultiplier = 1f;
            img.raycastTarget = raycast;
            return img;
        }

        /// <summary>Raised/sunken Windows-95 bevel panel.</summary>
        public static Image Bevel(Transform parent, string name, Color tint, bool sunken = false, bool raycast = true)
        {
            var img = Image(parent, name, tint, sunken ? Theme.Sunken : Theme.Raised, UnityEngine.UI.Image.Type.Sliced, raycast);
            return img;
        }

        /// <summary>Pixel-art application window with a readable, caller-coloured body.</summary>
        public static Image DialogWindow(Transform parent, string name, Color bodyColor)
        {
            // Use the clean panel chrome instead of the atlas' decorative desktop
            // window, whose fake minimize/maximize/close controls looked interactive.
            var frame = Image(parent, name, Color.white, ArtSprites.PanelChrome(), UnityEngine.UI.Image.Type.Sliced);
            var body = Image(frame.transform, "WindowBody", bodyColor, Theme.Solid, UnityEngine.UI.Image.Type.Simple, false);
            Fill(RT(body.gameObject), 9, 36, 9, 9);
            return frame;
        }

        public static Text Text(Transform parent, string name, string text, int size, Color color,
            Font font = null, TextAnchor anchor = TextAnchor.UpperLeft, bool wrap = true, FontStyle style = FontStyle.Normal)
        {
            var go = Node(parent, name);
            var t = go.AddComponent<Text>();
            t.text = text;
            t.font = font != null ? font : Theme.SystemSans;
            // The game is authored at 960x540 and often previewed below that size.
            // Legacy uGUI text otherwise collapses into 8-10 screen pixels and looks
            // noticeably softer than the supplied pixel art.
            t.fontSize = Mathf.CeilToInt(size * 1.15f);
            t.color = color;
            t.alignment = anchor;
            t.fontStyle = style;
            t.alignByGeometry = true;
            t.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            t.raycastTarget = false;
            return t;
        }

        // ---- Buttons -------------------------------------------------------
        /// <summary>Chunky depressing button. Returns the Image (background) so callers can recolor.</summary>
        public static Button Button(Transform parent, string name, string label, UnityAction onClick,
            Color? face = null, int fontSize = 16, Font font = null, Color? textColor = null)
        {
            Color normal = face ?? Color.white;
            var bg = Image(parent, name, normal, ArtSprites.ButtonChrome(), UnityEngine.UI.Image.Type.Sliced);
            var btn = bg.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.transition = Selectable.Transition.ColorTint;
            var colors = btn.colors;
            colors.normalColor = normal;
            colors.highlightedColor = Color.Lerp(normal, Color.white, 0.22f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(normal.r * 0.68f, normal.g * 0.68f, normal.b * 0.68f, normal.a);
            colors.disabledColor = new Color(normal.r * 0.45f, normal.g * 0.45f, normal.b * 0.45f, normal.a * 0.65f);
            colors.fadeDuration = 0.06f;
            btn.colors = colors;

            var t = Text(bg.transform, "Label", label, fontSize, textColor ?? Theme.TitleText, font ?? Theme.SystemSans, TextAnchor.MiddleCenter, false);
            Fill(RT(t.gameObject), 4, 2, 4, 2);

            // nudge label down 1px while pressed for tactility
            var press = bg.gameObject.AddComponent<ButtonPressNudge>();
            press.label = (RectTransform)t.transform;
            if (onClick != null) btn.onClick.AddListener(onClick);
            return btn;
        }

        public static Text SetButtonLabel(Button b, string s)
        {
            var t = b.GetComponentInChildren<Text>();
            if (t != null) t.text = s;
            return t;
        }

        /// <summary>
        /// Scales an already-placed Image's sprite to COVER its current box — zooming in
        /// and cropping the overflow — instead of preserveAspect's shrink-to-fit, which
        /// letterboxes generated character art (real backgrounds baked around the subject)
        /// down to a small figure floating in a sea of dead space. Call this AFTER the
        /// sprite is assigned, since it reads the sprite's own pixel dimensions. The
        /// image's parent must clip (RectMask2D) or the crop will visibly overflow.
        /// </summary>
        public static void CoverFit(Image img)
        {
            if (img.sprite == null) return;
            var rt = img.rectTransform;
            Vector2 box = rt.rect.size;
            Vector2 src = img.sprite.rect.size;
            if (box.x <= 0f || box.y <= 0f || src.x <= 0f || src.y <= 0f) return;

            float scale = Mathf.Max(box.x / src.x, box.y / src.y);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = src * scale;
            rt.anchoredPosition = Vector2.zero;
            img.preserveAspect = false;
        }

        /// <summary>Add a supplied pixel-art icon to a button without changing its interaction.</summary>
        public static Image ButtonIcon(Button button, Sprite sprite, float size = 20f, bool iconOnly = false)
        {
            var icon = Image(button.transform, "Icon", Color.white, sprite, UnityEngine.UI.Image.Type.Simple, false);
            icon.preserveAspect = true;
            if (iconOnly)
            {
                Place(RT(icon.gameObject), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(size, size), Vector2.zero);
                SetButtonLabel(button, "");
            }
            else
            {
                Place(RT(icon.gameObject), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(size, size), new Vector2(5, 0));
                var label = button.GetComponentInChildren<Text>();
                if (label != null) label.rectTransform.offsetMin = new Vector2(size + 8, label.rectTransform.offsetMin.y);
            }
            return icon;
        }

        // ---- Plastic equalizer slider -------------------------------------
        /// <summary>Vertical "plastic equalizer" slider with chunky handle and a coloured fill.</summary>
        public static Slider VSlider(Transform parent, float min, float max, float value, Color fillColor,
            UnityAction<float> onChange)
        {
            var root = Node(parent, "Slider");
            var slider = root.AddComponent<Slider>();
            slider.direction = Slider.Direction.BottomToTop;
            slider.minValue = min; slider.maxValue = max; slider.wholeNumbers = false;

            // sunken track
            var track = Bevel(root.transform, "Track", Theme.FaceShade, sunken: true, raycast: true);
            Fill(RT(track.gameObject));

            var fillArea = Node(root.transform, "FillArea");
            Fill(RT(fillArea.gameObject), 5, 8, 5, 8);
            var fill = Image(fillArea.transform, "Fill", fillColor);
            var fillRt = RT(fill.gameObject);
            fillRt.anchorMin = new Vector2(0, 0); fillRt.anchorMax = new Vector2(1, 1);
            fillRt.offsetMin = Vector2.zero; fillRt.offsetMax = Vector2.zero;
            slider.fillRect = fillRt;

            var handleArea = Node(root.transform, "HandleArea");
            Fill(RT(handleArea.gameObject), 2, 8, 2, 8);
            var handle = Bevel(handleArea.transform, "Handle", Theme.Face);
            var hrt = RT(handle.gameObject);
            hrt.sizeDelta = new Vector2(0, 22);
            slider.handleRect = hrt;
            slider.targetGraphic = handle;
            // grip lines on the handle
            var grip = Image(handle.transform, "Grip", Theme.FaceDark);
            Place(RT(grip.gameObject), new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(18, 2), new Vector2(0, 0));

            slider.value = value;
            if (onChange != null) slider.onValueChanged.AddListener(onChange);
            return slider;
        }

        // ---- Scroll list (simple vertical) --------------------------------
        public static (ScrollRect scroll, RectTransform content) VScroll(Transform parent, string name, Color bg)
        {
            var viewport = Bevel(parent, name, bg, sunken: true);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            var mask = viewport.gameObject.AddComponent<RectMask2D>();
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;
            scroll.viewport = RT(viewport.gameObject);

            var content = Node(viewport.transform, "Content");
            var crt = RT(content.gameObject);
            crt.anchorMin = new Vector2(0, 1); crt.anchorMax = new Vector2(1, 1);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.offsetMin = new Vector2(0, 0); crt.offsetMax = new Vector2(0, 0);
            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            vlg.spacing = 4; vlg.padding = new RectOffset(6, 6, 6, 6);
            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = crt;
            return (scroll, crt);
        }
    }

}
