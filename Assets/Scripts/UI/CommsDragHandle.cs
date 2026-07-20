using UnityEngine;
using UnityEngine.EventSystems;

namespace MadFact
{
    /// <summary>Lets the player reposition a dialogue window by dragging its title bar.</summary>
    public sealed class CommsDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        public RectTransform Target { get; set; }

        RectTransform _parent;

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (Target == null) return;
            _parent = Target.parent as RectTransform;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (Target == null || _parent == null) return;
            Canvas canvas = Target.GetComponentInParent<Canvas>();
            float scale = canvas != null ? Mathf.Max(.001f, canvas.scaleFactor) : 1f;
            Target.anchoredPosition = ClampToParent(
                Target.anchoredPosition + eventData.delta / scale);
        }

        Vector2 ClampToParent(Vector2 position)
        {
            Rect bounds = _parent.rect;
            Rect box = Target.rect;
            Vector2 pivot = Target.pivot;
            Vector2 anchor = new Vector2(
                Mathf.Lerp(bounds.xMin, bounds.xMax, Target.anchorMin.x),
                Mathf.Lerp(bounds.yMin, bounds.yMax, Target.anchorMin.y));

            float minX = bounds.xMin - anchor.x + box.width * pivot.x;
            float maxX = bounds.xMax - anchor.x - box.width * (1f - pivot.x);
            float minY = bounds.yMin - anchor.y + box.height * pivot.y;
            float maxY = bounds.yMax - anchor.y - box.height * (1f - pivot.y);

            // Small Game views can be narrower than the dialogue. Keep its center
            // reachable instead of producing an invalid clamp range.
            float x = minX <= maxX ? Mathf.Clamp(position.x, minX, maxX) : bounds.center.x;
            float y = minY <= maxY ? Mathf.Clamp(position.y, minY, maxY) : bounds.center.y;
            return new Vector2(x, y);
        }
    }
}
