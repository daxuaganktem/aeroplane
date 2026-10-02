using UnityEngine;
using UnityEngine.UI;

namespace MotoBrawler.Controls
{
    /// <summary>
    /// A rectangular on-screen hold area (steer left/right, throttle, brake).
    /// MobileControls polls every touch against these rectangles each frame, which gives
    /// true multi-touch and lets a thumb slide from one button to the next without lifting,
    /// something standard UI Buttons can't do.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class TouchZone : MonoBehaviour
    {
        [Tooltip("Optional image tinted while pressed.")]
        [SerializeField] private Graphic highlight;
        [SerializeField] private Color idleColor = new Color(1f, 1f, 1f, 0.18f);
        [SerializeField] private Color pressedColor = new Color(1f, 1f, 1f, 0.45f);

        public RectTransform Rect { get; private set; }
        public bool IsPressed { get; private set; }

        private void Awake()
        {
            Rect = (RectTransform)transform;
            if (!highlight) highlight = GetComponent<Graphic>();
            SetPressed(false, true);
        }

        public bool Contains(Vector2 screenPoint, Camera uiCamera)
        {
            return isActiveAndEnabled && RectTransformUtility.RectangleContainsScreenPoint(Rect, screenPoint, uiCamera);
        }

        public void SetPressed(bool pressed, bool force = false)
        {
            if (pressed == IsPressed && !force) return;
            IsPressed = pressed;
            if (highlight) highlight.color = pressed ? pressedColor : idleColor;
        }
    }
}
