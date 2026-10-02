using System.Text;
using MotoBrawler.Bike;
using MotoBrawler.Controls;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MotoBrawler.DebugTools
{
    /// <summary>
    /// Text readout for tuning: speed, steering (raw → assisted), lean, assist preset,
    /// control mode, bike state and FPS. Also: R = respawn (handy when stuck).
    /// </summary>
    public class BikeDebugHUD : MonoBehaviour
    {
        [SerializeField] private BikeController bike;
        [SerializeField] private BikeVisuals visuals;
        [SerializeField] private BikeCrashHandler crashHandler;
        [SerializeField] private TMP_Text text;
        [Tooltip("Seconds between refreshes (the text rebuild allocates a little).")]
        [SerializeField] private float refreshInterval = 0.1f;

        private readonly StringBuilder sb = new StringBuilder(512);
        private float timer;
        private float smoothedDelta = 1f / 60f;

        private void Awake()
        {
            if (!text) text = GetComponent<TMP_Text>();
            if (bike)
            {
                if (!visuals) visuals = bike.GetComponentInChildren<BikeVisuals>();
                if (!crashHandler) crashHandler = bike.GetComponent<BikeCrashHandler>();
            }
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame && crashHandler) crashHandler.Respawn();

            smoothedDelta = Mathf.Lerp(smoothedDelta, Time.unscaledDeltaTime, 0.05f);
            timer += Time.unscaledDeltaTime;
            if (timer < refreshInterval || !bike || !text) return;
            timer = 0f;

            RidingAssists a = bike.Assists;
            float lean = visuals ? visuals.CurrentLean : bike.IdealLeanAngle;
            string state = bike.IsCrashed ? "<color=#ff5050>CRASHED</color>"
                : !bike.IsGrounded ? "<color=#80c0ff>AIR</color>"
                : bike.IsSkidding ? "<color=#ffb030>SKID</color>"
                : "grounded";

            sb.Clear();
            sb.Append("SPEED  ").Append(bike.SpeedKmh.ToString("0")).Append(" km/h");
            if (bike.ForwardSpeed < -0.1f) sb.Append(" (reverse)");
            sb.Append('\n');
            sb.Append("STEER  ").Append(bike.RawSteerInput.ToString("+0.00;-0.00"))
              .Append(" > ").Append(bike.SteerInput.ToString("+0.00;-0.00")).Append('\n');
            sb.Append("GAS ").Append(bike.ThrottleInput.ToString("0.0"))
              .Append("  BRAKE ").Append(bike.BrakeInput.ToString("0.0")).Append('\n');
            sb.Append("LEAN   ").Append(lean.ToString("+0.0;-0.0")).Append("°\n");
            sb.Append("ASSIST ").Append(a ? a.displayName : "none");
            if (a)
            {
                sb.Append("  <size=70%>(steer ").Append(a.steeringAssist.ToString("0.00"))
                  .Append(" stab ").Append(a.stabilityAssist.ToString("0.00"))
                  .Append(" abs ").Append(a.brakeAssist.ToString("0.00"))
                  .Append(" crash ").Append(a.crashSpeedKmh.ToString("0")).Append("km/h)</size>");
            }
            sb.Append('\n');
            sb.Append("INPUT  ").Append(ControlSettings.Steering == SteeringMode.Tilt ? "tilt" : "buttons")
              .Append(ControlSettings.AutoThrottle ? " + auto-gas" : "").Append('\n');
            sb.Append("STATE  ").Append(state).Append('\n');
            sb.Append("FPS    ").Append((1f / Mathf.Max(smoothedDelta, 0.0001f)).ToString("0"))
              .Append("   <size=70%>[P] assists  [R] respawn</size>");

            text.SetText(sb);
        }
    }
}
