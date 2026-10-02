using MotoBrawler.Bike;
using Unity.Cinemachine;
using UnityEngine;

namespace MotoBrawler.CameraRig
{
    /// <summary>
    /// Speed and lean effects on top of a Cinemachine 3 follow camera:
    ///  - FOV widens as speed rises (sense of speed),
    ///  - the camera rolls (Dutch) a little with the bike's visual lean.
    /// Position / follow lag is done by the CinemachineFollow + RotationComposer components.
    /// </summary>
    [RequireComponent(typeof(CinemachineCamera))]
    public class BikeCameraFX : MonoBehaviour
    {
        [SerializeField] private BikeController bike;
        [Tooltip("Optional: uses the smoothed visual lean. Falls back to the physics lean.")]
        [SerializeField] private BikeVisuals visuals;

        [Header("Field of view")]
        [SerializeField, Range(30f, 90f)] private float baseFov = 60f;
        [Tooltip("Extra FOV at top speed (deg).")]
        [SerializeField, Range(0f, 30f)] private float maxFovBoost = 10f;
        [Tooltip("Speed (fraction of top speed) where the FOV starts to widen.")]
        [SerializeField, Range(0f, 1f)] private float fovStartSpeed = 0.3f;
        [SerializeField, Range(0.5f, 10f)] private float fovSmoothing = 2.5f;

        [Header("Roll (Dutch)")]
        [Tooltip("Fraction of the bike lean the camera copies.")]
        [SerializeField, Range(0f, 1f)] private float rollFollow = 0.2f;
        [SerializeField, Range(0f, 30f)] private float maxRoll = 10f;
        [SerializeField, Range(0.5f, 15f)] private float rollSmoothing = 4f;

        private CinemachineCamera cam;
        private float fov;
        private float roll;

        private void Awake()
        {
            cam = GetComponent<CinemachineCamera>();
            if (!visuals && bike) visuals = bike.GetComponentInChildren<BikeVisuals>();
            fov = baseFov;
        }

        private void LateUpdate()
        {
            if (!bike) return;
            float dt = Time.deltaTime;

            // Ease in the FOV boost between fovStartSpeed and top speed.
            float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(fovStartSpeed, 1f, bike.Speed01));
            float targetFov = baseFov + maxFovBoost * t;
            fov = Mathf.Lerp(fov, targetFov, 1f - Mathf.Exp(-fovSmoothing * dt));

            // Lean right (+) means the bike rolls clockwise seen from behind = negative Dutch.
            float lean = visuals ? visuals.CurrentLean : bike.IdealLeanAngle;
            float targetRoll = Mathf.Clamp(-lean * rollFollow, -maxRoll, maxRoll);
            roll = Mathf.Lerp(roll, targetRoll, 1f - Mathf.Exp(-rollSmoothing * dt));

            LensSettings lens = cam.Lens;
            lens.FieldOfView = fov;
            lens.Dutch = roll;
            cam.Lens = lens;
        }
    }
}
