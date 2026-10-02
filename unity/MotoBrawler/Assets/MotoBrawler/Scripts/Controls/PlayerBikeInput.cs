using MotoBrawler.Bike;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MotoBrawler.Controls
{
    /// <summary>
    /// Human rider: merges keyboard / gamepad (Input System) with the on-screen mobile
    /// controls and hands the result to the BikeController as a BikeInputState.
    ///
    /// Keyboard: A/D or ←/→ steer, W or ↑ throttle, Space / S / ↓ brake (hold to reverse).
    /// Gamepad:  left stick steer, right trigger throttle, left trigger brake.
    ///
    /// Actions are built in code so there is no .inputactions asset to set up; swap in
    /// InputActionReferences later if you want rebinding.
    /// </summary>
    public class PlayerBikeInput : MonoBehaviour, IBikeInputProvider
    {
        [Tooltip("Optional. Found automatically in the scene if left empty.")]
        [SerializeField] private MobileControls mobileControls;

        private InputAction steerAction;
        private InputAction throttleAction;
        private InputAction brakeAction;

        private void Awake()
        {
            if (!mobileControls) mobileControls = FindFirstObjectByType<MobileControls>();

            steerAction = new InputAction("Steer", InputActionType.Value, expectedControlType: "Axis");
            steerAction.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/a")
                .With("Positive", "<Keyboard>/d");
            steerAction.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/leftArrow")
                .With("Positive", "<Keyboard>/rightArrow");
            steerAction.AddBinding("<Gamepad>/leftStick/x");

            throttleAction = new InputAction("Throttle", InputActionType.Value, expectedControlType: "Axis");
            throttleAction.AddBinding("<Keyboard>/w");
            throttleAction.AddBinding("<Keyboard>/upArrow");
            throttleAction.AddBinding("<Gamepad>/rightTrigger");

            brakeAction = new InputAction("Brake", InputActionType.Value, expectedControlType: "Axis");
            brakeAction.AddBinding("<Keyboard>/space");
            brakeAction.AddBinding("<Keyboard>/s");
            brakeAction.AddBinding("<Keyboard>/downArrow");
            brakeAction.AddBinding("<Gamepad>/leftTrigger");
        }

        private void OnEnable()
        {
            steerAction.Enable();
            throttleAction.Enable();
            brakeAction.Enable();
        }

        private void OnDisable()
        {
            steerAction.Disable();
            throttleAction.Disable();
            brakeAction.Disable();
        }

        private void OnDestroy()
        {
            steerAction.Dispose();
            throttleAction.Dispose();
            brakeAction.Dispose();
        }

        public BikeInputState ReadInput()
        {
            if (!isActiveAndEnabled) return BikeInputState.None;

            float steer = steerAction.ReadValue<float>();
            float throttle = throttleAction.ReadValue<float>();
            float brake = brakeAction.ReadValue<float>();

            if (mobileControls && mobileControls.IsActive)
            {
                // Keyboard/gamepad steering wins when used; otherwise touch / tilt.
                if (Mathf.Abs(steer) < 0.01f) steer = mobileControls.Steer;
                throttle = Mathf.Max(throttle, mobileControls.Throttle);
                brake = Mathf.Max(brake, mobileControls.Brake);
            }

            // Casual mode: always on the gas unless braking.
            if (ControlSettings.AutoThrottle && brake < 0.1f)
                throttle = 1f;

            return new BikeInputState { steer = steer, throttle = throttle, brake = brake };
        }
    }
}
