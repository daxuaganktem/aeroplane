using UnityEngine;

namespace MotoBrawler.Bike
{
    /// <summary>
    /// Purely cosmetic: lean, fork steering, wheel spin, suspension travel, a little pitch
    /// under acceleration/braking, and a "fallen over" pose while crashed.
    /// Nothing here touches physics, so the collider never leans.
    ///
    /// Expected hierarchy (see README):
    ///   Visual (this component)
    ///     LeanPivot            at ground level, between the wheels
    ///       Body, Rider ...
    ///       SteerPivot         at the steering head, local Y = steering axis
    ///         FrontFork (mesh)
    ///         FrontWheelSpin   at the front axle, local X = axle
    ///           FrontWheel (mesh)
    ///       RearWheelSpin      at the rear axle, local X = axle
    ///         RearWheel (mesh)
    /// </summary>
    public class BikeVisuals : MonoBehaviour
    {
        [SerializeField] private BikeController bike;

        [Header("Pivots")]
        [Tooltip("Rotates around its local Z to lean. Place at ground level under the bike centre.")]
        [SerializeField] private Transform leanPivot;
        [Tooltip("Rotates around its local Y (tilted to match the fork rake).")]
        [SerializeField] private Transform steerPivot;
        [Tooltip("Rotates around its local X. Place exactly at the front axle centre.")]
        [SerializeField] private Transform frontWheelSpin;
        [Tooltip("Rotates around its local X. Place exactly at the rear axle centre.")]
        [SerializeField] private Transform rearWheelSpin;
        [Tooltip("Optional: rider root under LeanPivot. Gets a little extra lean (hanging off).")]
        [SerializeField] private Transform rider;

        [Header("Extras")]
        [Tooltip("Move the wheel meshes with the suspension.")]
        [SerializeField] private bool animateSuspension = true;
        [Tooltip("Extra rider lean as a fraction of the bike lean.")]
        [SerializeField, Range(0f, 0.5f)] private float riderExtraLean = 0.15f;
        [Tooltip("Body pitch per m/s² of acceleration (deg). Nose up on throttle, dive on brakes.")]
        [SerializeField, Range(0f, 1f)] private float pitchPerAccel = 0.25f;
        [SerializeField, Range(0f, 10f)] private float maxPitch = 4f;
        [Tooltip("Lean angle of the 'fallen over' pose while crashed (deg).")]
        [SerializeField, Range(0f, 90f)] private float crashedLean = 80f;

        /// <summary>Current visual lean in degrees (+ = leaning right). Read by camera / HUD.</summary>
        public float CurrentLean { get; private set; }
        public BikeController Bike => bike;

        private Quaternion leanBaseRot, steerBaseRot, frontSpinBaseRot, rearSpinBaseRot, riderBaseRot;
        private Vector3 frontSpinBasePos, rearSpinBasePos;
        private float frontSpinAngle, rearSpinAngle;
        private float currentFork, currentPitch;
        private float frontSuspension, rearSuspension;

        private void Awake()
        {
            if (!bike) bike = GetComponentInParent<BikeController>();

            // Remember the authored poses; all animation is applied on top of them, so the
            // imported meshes can keep whatever rotation the FBX/glTF gave them.
            if (leanPivot) leanBaseRot = leanPivot.localRotation;
            if (steerPivot) steerBaseRot = steerPivot.localRotation;
            if (rider) riderBaseRot = rider.localRotation;
            if (frontWheelSpin)
            {
                frontSpinBaseRot = frontWheelSpin.localRotation;
                frontSpinBasePos = frontWheelSpin.localPosition;
            }
            if (rearWheelSpin)
            {
                rearSpinBaseRot = rearWheelSpin.localRotation;
                rearSpinBasePos = rearWheelSpin.localPosition;
            }
        }

        // LateUpdate: runs after the interpolated Rigidbody pose is written for this frame.
        private void LateUpdate()
        {
            if (!bike || !bike.Stats) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            BikeStats stats = bike.Stats;

            UpdateLean(stats, dt);
            UpdateSteering(dt);
            UpdateWheels(stats, dt);
            if (animateSuspension) UpdateSuspension(stats, dt);
        }

        private void UpdateLean(BikeStats stats, float dt)
        {
            float target;
            float speed = stats.leanSpeed;

            if (bike.IsCrashed)
            {
                // Fall to the side we were leaning toward.
                target = CurrentLean >= 0f ? crashedLean : -crashedLean;
                speed *= 0.8f;
            }
            else if (!bike.IsGrounded)
            {
                target = 0f;            // straighten up in the air
                speed *= 0.5f;
            }
            else
            {
                // Physics-plausible lean + a bit straight from the input for instant feedback.
                // The input part fades out at walking pace so the bike doesn't lean while stopped.
                float inputLean = bike.SteerInput * stats.leanFromInput * stats.maxLeanAngle *
                                  Mathf.Clamp01(Mathf.Abs(bike.ForwardSpeed) / 5f);
                target = Mathf.Clamp(bike.IdealLeanAngle + inputLean, -stats.maxLeanAngle, stats.maxLeanAngle);
            }

            CurrentLean = Mathf.Lerp(CurrentLean, target, 1f - Mathf.Exp(-speed * dt));

            // Pitch from engine/brake acceleration.
            float accel = bike.LongitudinalAccel;
            float targetPitch = bike.IsGrounded && !bike.IsCrashed
                ? Mathf.Clamp(-accel * pitchPerAccel, -maxPitch, maxPitch)
                : 0f;
            currentPitch = Mathf.Lerp(currentPitch, targetPitch, 1f - Mathf.Exp(-6f * dt));

            // Unity: +Z rotation tilts left, so lean right = negative Z. -X pitch = nose up.
            if (leanPivot)
                leanPivot.localRotation = leanBaseRot * Quaternion.Euler(currentPitch, 0f, -CurrentLean);

            if (rider)
                rider.localRotation = riderBaseRot * Quaternion.Euler(0f, 0f, -CurrentLean * riderExtraLean);
        }

        private void UpdateSteering(float dt)
        {
            float target = bike.IsCrashed ? currentFork : bike.ForkAngle;
            currentFork = Mathf.Lerp(currentFork, target, 1f - Mathf.Exp(-15f * dt));
            if (steerPivot)
                steerPivot.localRotation = steerBaseRot * Quaternion.Euler(0f, currentFork, 0f);
        }

        private void UpdateWheels(BikeStats stats, float dt)
        {
            if (bike.IsCrashed) return;

            // Angular speed = v / r. Both wheels spin with road speed; a locked rear wheel stops.
            float degPerSecond = bike.ForwardSpeed / stats.wheelRadius * Mathf.Rad2Deg;
            frontSpinAngle = Mathf.Repeat(frontSpinAngle + degPerSecond * dt, 360f);
            if (!bike.IsSkidding)
                rearSpinAngle = Mathf.Repeat(rearSpinAngle + degPerSecond * dt, 360f);

            if (frontWheelSpin) frontWheelSpin.localRotation = frontSpinBaseRot * Quaternion.Euler(frontSpinAngle, 0f, 0f);
            if (rearWheelSpin) rearWheelSpin.localRotation = rearSpinBaseRot * Quaternion.Euler(rearSpinAngle, 0f, 0f);
        }

        private void UpdateSuspension(BikeStats stats, float dt)
        {
            float frontTarget = WheelOffset(bike.FrontContact, stats);
            float rearTarget = WheelOffset(bike.RearContact, stats);
            float k = 1f - Mathf.Exp(-25f * dt);
            frontSuspension = Mathf.Lerp(frontSuspension, frontTarget, k);
            rearSuspension = Mathf.Lerp(rearSuspension, rearTarget, k);

            // Offset along the leaned bike's up so it looks right mid-corner.
            Vector3 up = leanPivot ? leanPivot.up : transform.up;
            if (frontWheelSpin && frontWheelSpin.parent)
                frontWheelSpin.position = frontWheelSpin.parent.TransformPoint(frontSpinBasePos) + up * frontSuspension;
            if (rearWheelSpin && rearWheelSpin.parent)
                rearWheelSpin.position = rearWheelSpin.parent.TransformPoint(rearSpinBasePos) + up * rearSuspension;
        }

        private static float WheelOffset(BikeController.WheelContact wheel, BikeStats stats)
        {
            // In the air the wheels hang a little below their rest position.
            if (!wheel.grounded) return -0.05f;
            return Mathf.Clamp(wheel.compression, -0.05f, stats.suspensionTravel);
        }
    }
}
