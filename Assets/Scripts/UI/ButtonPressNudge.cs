using UnityEngine;
using UnityEngine.EventSystems;

namespace MadFact
{
    /// <summary>Nudges a button label down a pixel while held, for chunky tactility.</summary>
    public class ButtonPressNudge : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public RectTransform label;
        Vector2 _home;
        bool _init;

        void Ensure()
        {
            if (!_init && label)
            {
                _home = label.anchoredPosition;
                _init = true;
            }
        }

        public void OnPointerDown(PointerEventData e)
        {
            Ensure();
            if (label) label.anchoredPosition = _home + new Vector2(1, -1);
        }

        public void OnPointerUp(PointerEventData e)
        {
            Ensure();
            if (label) label.anchoredPosition = _home;
        }
    }
}
