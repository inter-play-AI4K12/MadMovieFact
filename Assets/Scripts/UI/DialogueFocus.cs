using UnityEngine;
using UnityEngine.UI;

namespace MadFact
{
    /// <summary>
    /// Reusable dialogue spotlight. Attach this beside a CommsBox, then call Focus on
    /// any UI RectTransform. The rest of the Canvas dims while the target remains clear
    /// and receives a pulsing frame. The overlay never blocks clicks.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DialogueFocus : MonoBehaviour
    {
        [Header("Spotlight")]
        [SerializeField, Range(0f, 0.9f)] float _dimOpacity = 0.68f;
        [SerializeField] Color _highlightColor = new Color(1f, 0.76f, 0.18f, 1f);
        [SerializeField] Vector2 _padding = new Vector2(10f, 8f);
        [SerializeField, Min(1f)] float _borderWidth = 4f;

        [Header("Motion")]
        [SerializeField, Min(0f)] float _pulseSpeed = 3f;
        [SerializeField, Range(0f, 0.75f)] float _pulseAmount = 0.28f;

        DialogueFocusOverlay _overlay;

        public RectTransform CurrentTarget => _overlay != null ? _overlay.Target : null;
        public bool IsFocused => CurrentTarget != null && _overlay.gameObject.activeSelf;

        public void Focus(RectTransform target)
        {
            if (target == null)
            {
                Clear();
                return;
            }

            EnsureOverlay();
            if (_overlay == null) return;
            _overlay.Configure(
                target,
                new Color(0f, 0f, 0f, _dimOpacity),
                _highlightColor,
                _padding,
                _borderWidth,
                _pulseSpeed,
                _pulseAmount);
            PlaceBelowDialogue();
            _overlay.gameObject.SetActive(true);
        }

        public void Clear()
        {
            if (_overlay == null) return;
            _overlay.Target = null;
            _overlay.gameObject.SetActive(false);
        }

        void OnDisable() => Clear();

        void OnValidate()
        {
            _dimOpacity = Mathf.Clamp(_dimOpacity, 0f, 0.9f);
            _borderWidth = Mathf.Max(1f, _borderWidth);
            _pulseSpeed = Mathf.Max(0f, _pulseSpeed);
            if (_overlay != null && CurrentTarget != null)
                Focus(CurrentTarget);
        }

        void EnsureOverlay()
        {
            if (_overlay != null) return;

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("DialogueFocus needs to be under a Canvas.", this);
                return;
            }

            var overlayObject = new GameObject(
                "DialogueFocusOverlay",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(DialogueFocusOverlay));
            overlayObject.transform.SetParent(canvas.transform, false);
            overlayObject.hideFlags = HideFlags.DontSave;

            RectTransform rect = (RectTransform)overlayObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            _overlay = overlayObject.GetComponent<DialogueFocusOverlay>();
            _overlay.raycastTarget = false;
            _overlay.gameObject.SetActive(false);
        }

        void PlaceBelowDialogue()
        {
            if (_overlay == null || transform.parent != _overlay.transform.parent) return;
            int dialogueIndex = transform.GetSiblingIndex();
            _overlay.transform.SetSiblingIndex(Mathf.Max(0, dialogueIndex));
            transform.SetAsLastSibling();
        }
    }

    /// <summary>Draws four dim rectangles around a transparent hole plus its frame.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DialogueFocusOverlay : MaskableGraphic
    {
        readonly Vector3[] _worldCorners = new Vector3[4];

        Color _dimColor;
        Color _highlightColor;
        Vector2 _padding;
        float _borderWidth;
        float _pulseSpeed;
        float _pulseAmount;

        public RectTransform Target { get; set; }

        public void Configure(
            RectTransform target,
            Color dimColor,
            Color highlightColor,
            Vector2 padding,
            float borderWidth,
            float pulseSpeed,
            float pulseAmount)
        {
            Target = target;
            _dimColor = dimColor;
            _highlightColor = highlightColor;
            _padding = padding;
            _borderWidth = borderWidth;
            _pulseSpeed = pulseSpeed;
            _pulseAmount = pulseAmount;
            SetVerticesDirty();
        }

        void Update()
        {
            if (Target == null || !Target.gameObject.activeInHierarchy)
            {
                gameObject.SetActive(false);
                return;
            }
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            if (Target == null) return;

            Rect bounds = rectTransform.rect;
            Rect hole = TargetRectInOverlaySpace(bounds);
            if (hole.width <= 0f || hole.height <= 0f) return;

            AddQuad(vertexHelper, Rect.MinMaxRect(bounds.xMin, bounds.yMin, hole.xMin, bounds.yMax), _dimColor);
            AddQuad(vertexHelper, Rect.MinMaxRect(hole.xMax, bounds.yMin, bounds.xMax, bounds.yMax), _dimColor);
            AddQuad(vertexHelper, Rect.MinMaxRect(hole.xMin, bounds.yMin, hole.xMax, hole.yMin), _dimColor);
            AddQuad(vertexHelper, Rect.MinMaxRect(hole.xMin, hole.yMax, hole.xMax, bounds.yMax), _dimColor);

            float wave = _pulseSpeed <= 0f
                ? 1f
                : 1f - _pulseAmount + _pulseAmount * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * _pulseSpeed));
            Color frame = _highlightColor;
            frame.a *= wave;

            float width = _borderWidth;
            AddQuad(vertexHelper, Rect.MinMaxRect(hole.xMin - width, hole.yMin - width, hole.xMax + width, hole.yMin), frame);
            AddQuad(vertexHelper, Rect.MinMaxRect(hole.xMin - width, hole.yMax, hole.xMax + width, hole.yMax + width), frame);
            AddQuad(vertexHelper, Rect.MinMaxRect(hole.xMin - width, hole.yMin, hole.xMin, hole.yMax), frame);
            AddQuad(vertexHelper, Rect.MinMaxRect(hole.xMax, hole.yMin, hole.xMax + width, hole.yMax), frame);
        }

        Rect TargetRectInOverlaySpace(Rect bounds)
        {
            Target.GetWorldCorners(_worldCorners);
            Vector3 bottomLeft = rectTransform.InverseTransformPoint(_worldCorners[0]);
            Vector3 topRight = rectTransform.InverseTransformPoint(_worldCorners[2]);

            float xMin = Mathf.Clamp(bottomLeft.x - _padding.x, bounds.xMin, bounds.xMax);
            float yMin = Mathf.Clamp(bottomLeft.y - _padding.y, bounds.yMin, bounds.yMax);
            float xMax = Mathf.Clamp(topRight.x + _padding.x, bounds.xMin, bounds.xMax);
            float yMax = Mathf.Clamp(topRight.y + _padding.y, bounds.yMin, bounds.yMax);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        static void AddQuad(VertexHelper vertexHelper, Rect rect, Color color)
        {
            if (rect.width <= 0f || rect.height <= 0f || color.a <= 0f) return;

            int start = vertexHelper.currentVertCount;
            vertexHelper.AddVert(new Vector3(rect.xMin, rect.yMin), color, Vector2.zero);
            vertexHelper.AddVert(new Vector3(rect.xMin, rect.yMax), color, Vector2.up);
            vertexHelper.AddVert(new Vector3(rect.xMax, rect.yMax), color, Vector2.one);
            vertexHelper.AddVert(new Vector3(rect.xMax, rect.yMin), color, Vector2.right);
            vertexHelper.AddTriangle(start, start + 1, start + 2);
            vertexHelper.AddTriangle(start, start + 2, start + 3);
        }
    }
}
