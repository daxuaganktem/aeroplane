using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MotoBrawler.Controls
{
    /// <summary>
    /// On-screen mobile controls: steer-left / steer-right hold zones OR phone tilt,
    /// a throttle zone (right) and a brake zone (left), plus buttons to switch steering
    /// mode, toggle auto-throttle and calibrate tilt. Exposes Steer/Throttle/Brake values;
    /// PlayerBikeInput merges them with keyboard/gamepad.
    ///
    /// Put this on the controls Canvas. Works with a mouse in the Editor for quick tests.
    /// </summary>
    public class MobileControls : MonoBehaviour
    {
        [Header("Hold zones")]
        [SerializeField] private TouchZone steerLeftZone;
        [SerializeField] private TouchZone steerRightZone;
        [SerializeField] private TouchZone throttleZone;
        [SerializeField] private TouchZone brakeZone;
        [Tooltip("Parent of the two steer zones; hidden in tilt mode.")]
        [SerializeField] private GameObject steerButtonsGroup;

        [Header("Setting buttons (optional)")]
        [SerializeField] private Button steeringModeButton;
        [SerializeField] private TMP_Text steeringModeLabel;
        [SerializeField] private Button autoThrottleButton;
        [SerializeField] private TMP_Text autoThrottleLabel;
        [SerializeField] private Button calibrateTiltButton;

        [Header("Visibility")]
        [Tooltip("Everything to hide on desktop builds when 'Show On Desktop' is off.")]
        [SerializeField] private GameObject controlsRoot;
        [Tooltip("Show the touch controls in the Editor / on desktop (to test with the mouse).")]
        [SerializeField] private bool showOnDesktop = true;

        [Header("Tilt")]
        [Tooltip("Ignore device roll smaller than this (deg).")]
        [SerializeField, Range(0f, 10f)] private float tiltDeadZone = 2f;
        [Tooltip("Smoothing of the tilt reading (1/s). Higher = more responsive, more jitter.")]
        [SerializeField, Range(2f, 40f)] private float tiltSmoothing = 18f;

        /// <summary>-1..1</summary>
        public float Steer { get; private set; }
        public float Throttle { get; private set; }
        public float Brake { get; private set; }
        /// <summary>True when the touch controls are shown and should be read.</summary>
        public bool IsActive { get; private set; }

        private Canvas canvas;
        private float rawTiltAngle;     // device roll in degrees (before calibration)
        private float smoothedTiltSteer;

        private void Awake()
        {
            canvas = GetComponentInParent<Canvas>();
            IsActive = Application.isMobilePlatform || showOnDesktop;
            if (controlsRoot) controlsRoot.SetActive(IsActive);

            // Report sensor values relative to the current screen orientation (landscape left/right).
            InputSystem.settings.compensateForScreenOrientation = true;

            if (steeringModeButton) steeringModeButton.onClick.AddListener(ToggleSteeringMode);
            if (autoThrottleButton) autoThrottleButton.onClick.AddListener(ToggleAutoThrottle);
            if (calibrateTiltButton) calibrateTiltButton.onClick.AddListener(CalibrateTilt);
        }

        private void OnEnable()
        {
            ControlSettings.Changed += ApplySettings;
            ApplySettings();
        }

        private void OnDisable()
        {
            ControlSettings.Changed -= ApplySettings;
            SetAccelerometerEnabled(false);
            Steer = Throttle = Brake = 0f;
        }

        // ---------------- Button callbacks (public so they can also be wired in the Inspector) ----

        public void ToggleSteeringMode()
        {
            ControlSettings.Steering = ControlSettings.Steering == SteeringMode.Buttons
                ? SteeringMode.Tilt
                : SteeringMode.Buttons;
        }

        public void ToggleAutoThrottle() => ControlSettings.AutoThrottle = !ControlSettings.AutoThrottle;

        /// <summary>Treat the way the phone is held right now as "straight ahead".</summary>
        public void CalibrateTilt() => ControlSettings.TiltZeroAngle = rawTiltAngle;

        private void ApplySettings()
        {
            bool tilt = ControlSettings.Steering == SteeringMode.Tilt;
            if (steerButtonsGroup) steerButtonsGroup.SetActive(!tilt);
            if (calibrateTiltButton) calibrateTiltButton.gameObject.SetActive(tilt);
            SetAccelerometerEnabled(tilt && IsActive);

            if (steeringModeLabel) steeringModeLabel.text = tilt ? "Steer: TILT" : "Steer: BUTTONS";
            if (autoThrottleLabel) autoThrottleLabel.text = ControlSettings.AutoThrottle ? "Auto-gas: ON" : "Auto-gas: OFF";
        }

        private static void SetAccelerometerEnabled(bool value)
        {
            // Sensors cost battery: only run the accelerometer while tilt steering is used.
            Accelerometer acc = Accelerometer.current;
            if (acc == null) return;
            if (value && !acc.enabled) InputSystem.EnableDevice(acc);
            else if (!value && acc.enabled) InputSystem.DisableDevice(acc);
        }

        // ---------------- Per-frame polling ----------------

        private void Update()
        {
            if (!IsActive)
            {
                Steer = Throttle = Brake = 0f;
                return;
            }

            PollZones(out bool left, out bool right, out bool gas, out bool brake);

            Throttle = gas ? 1f : 0f;
            Brake = brake ? 1f : 0f;

            if (ControlSettings.Steering == SteeringMode.Tilt)
                Steer = ReadTiltSteer();
            else
                Steer = (right ? 1f : 0f) - (left ? 1f : 0f);
        }

        private void PollZones(out bool left, out bool right, out bool gas, out bool brake)
        {
            left = right = gas = brake = false;
            Camera uiCamera = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                var touches = touchscreen.touches;
                for (int i = 0; i < touches.Count; i++)
                {
                    if (!touches[i].press.isPressed) continue;
                    Vector2 p = touches[i].position.ReadValue();
                    TestPoint(p, uiCamera, ref left, ref right, ref gas, ref brake);
                }
            }

            // Mouse = one finger, for testing in the Editor.
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed)
                TestPoint(mouse.position.ReadValue(), uiCamera, ref left, ref right, ref gas, ref brake);

            if (steerLeftZone) steerLeftZone.SetPressed(left);
            if (steerRightZone) steerRightZone.SetPressed(right);
            if (throttleZone) throttleZone.SetPressed(gas);
            if (brakeZone) brakeZone.SetPressed(brake);
        }

        private void TestPoint(Vector2 p, Camera cam, ref bool left, ref bool right, ref bool gas, ref bool brake)
        {
            if (steerLeftZone && steerLeftZone.Contains(p, cam)) left = true;
            if (steerRightZone && steerRightZone.Contains(p, cam)) right = true;
            if (throttleZone && throttleZone.Contains(p, cam)) gas = true;
            if (brakeZone && brakeZone.Contains(p, cam)) brake = true;
        }

        private float ReadTiltSteer()
        {
            Accelerometer acc = Accelerometer.current;
            if (acc == null || !acc.enabled) return 0f;

            // Gravity in screen space: x = right, y = up. Turning the phone like a steering
            // wheel moves gravity between -y and ±x, so the roll angle is atan2(x, -y).
            Vector3 g = acc.acceleration.ReadValue();
            float planar = new Vector2(g.x, g.y).magnitude;
            if (planar > 0.2f)   // phone lying flat = no reliable roll, keep the last value
                rawTiltAngle = Mathf.Atan2(g.x, -g.y) * Mathf.Rad2Deg;

            float angle = Mathf.DeltaAngle(ControlSettings.TiltZeroAngle, rawTiltAngle);
            angle = Mathf.Sign(angle) * Mathf.Max(Mathf.Abs(angle) - tiltDeadZone, 0f);
            float target = Mathf.Clamp(angle / ControlSettings.TiltFullLockAngle, -1f, 1f);

            smoothedTiltSteer = Mathf.Lerp(smoothedTiltSteer, target, 1f - Mathf.Exp(-tiltSmoothing * Time.deltaTime));
            return smoothedTiltSteer;
        }
    }
}
