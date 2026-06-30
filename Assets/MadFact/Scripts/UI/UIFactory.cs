using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MadFact
{
    /// <summary>
    /// Procedural UI construction helpers. Everything in MadFact is built at runtime
    /// in code (Screen-Space-Overlay canvas) so the whole game lives in scripts.
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

        public static Text Text(Transform parent, string name, string text, int size, Color color,
            Font font = null, TextAnchor anchor = TextAnchor.UpperLeft, bool wrap = true, FontStyle style = FontStyle.Normal)
        {
            var go = Node(parent, name);
            var t = go.AddComponent<Text>();
            t.text = text;
            t.font = font != null ? font : Theme.SystemSans;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.fontStyle = style;
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
            var bg = Bevel(parent, name, face ?? Theme.Face);
            var btn = bg.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            var st = btn.spriteState;
            st.pressedSprite = Theme.Sunken;
            st.highlightedSprite = Theme.Raised;
            st.selectedSprite = Theme.Raised;
            st.disabledSprite = Theme.Sunken;
            btn.spriteState = st;
            btn.transition = Selectable.Transition.SpriteSwap;

            var t = Text(bg.transform, "Label", label, fontSize, textColor ?? Theme.Ink, font ?? Theme.SystemSans, TextAnchor.MiddleCenter, false);
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

    /// <summary>Nudges a button label down a pixel while held, for chunky tactility.</summary>
    public class ButtonPressNudge : MonoBehaviour,
        UnityEngine.EventSystems.IPointerDownHandler, UnityEngine.EventSystems.IPointerUpHandler
    {
        public RectTransform label;
        Vector2 _home;
        bool _init;
        void Ensure() { if (!_init && label) { _home = label.anchoredPosition; _init = true; } }
        public void OnPointerDown(UnityEngine.EventSystems.PointerEventData e) { Ensure(); if (label) label.anchoredPosition = _home + new Vector2(1, -1); }
        public void OnPointerUp(UnityEngine.EventSystems.PointerEventData e) { Ensure(); if (label) label.anchoredPosition = _home; }
    }
}
