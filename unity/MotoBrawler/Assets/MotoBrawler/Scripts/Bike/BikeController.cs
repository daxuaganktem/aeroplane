using System;
using UnityEngine;

namespace MotoBrawler.Bike
{
    /// <summary>
    /// Arcade motorcycle physics on ONE Rigidbody.
    ///
    /// How it works, every physics step:
    ///  1. Read a BikeInputState from whatever IBikeInputProvider rides this bike.
    ///  2. Raycast down from each wheel probe: spring + damper forces hold the bike up,
    ///     the hit normals tell us the slope.
    ///  3. Throttle / brake / drag push the bike along the ground plane.
    ///  4. Tyre grip removes sideways slip, limited by a "friction circle" (braking and
    ///     cornering share the same grip). Exceeding it = skid.
    ///  5. Rotation is written directly: yaw comes from steering (speed sensitive), pitch
    ///     and roll are pulled toward the ground normal (or toward level in the air).
    ///     The physics body never leans or tips over; lean is visual only (BikeVisuals).
    ///
    /// Nothing in here knows about keyboards, touch, cameras or UI, so AI riders, combat and
    /// different bikes can reuse it unchanged.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DefaultExecutionOrder(-50)]
    public class BikeController : MonoBehaviour
    {
        /// <summary>Result of one wheel's ground raycast.</summary>
        public struct WheelContact
        {
            public bool grounded;
            public Vector3 point;
            public Vector3 normal;
            /// <summary>Distance from the top of the suspension to the wheel centre.</summary>
            public float springLength;
            /// <summary>Metres the wheel is pushed up from its rest position (negative = drooping).</summary>
            public float compression;
            public Collider collider;
        }

        [Header("Data")]
        [SerializeField] private BikeStats stats;
        [SerializeField] private RidingAssists assists;

        [Header("Wheel probes (empties at each axle centre, at rest height)")]
        [SerializeField] private Transform frontWheelProbe;
        [SerializeField] private Transform rearWheelProbe;

        [Header("Ground detection")]
        [Tooltip("Layers the wheels can ride on. Must NOT include the bike's own layer.")]
        [SerializeField] private LayerMask groundMask = ~0;
        [Tooltip("Extra reach below the rest position that still counts as touching the ground (m).")]
        [SerializeField] private float groundedTolerance = 0.08f;
        [Tooltip("How far below the bike to look for the landing surface while airborne (m).")]
        [SerializeField] private float landingProbeDistance = 20f;

        [Header("Optional")]
        [Tooltip("Optional centre-of-mass marker. Empty = Unity computes it from the colliders.")]
        [SerializeField] private Transform centerOfMass;

        // ----- Runtime state -----
        private Rigidbody rb;
        private IBikeInputProvider inputProvider;
        private BikeInputState rawInput;
        private WheelContact front;
        private WheelContact rear;
        private float steer;              // assisted steering, -1..1
        private float yawRate;            // rad/s around the bike's up axis, + = turning right
        private float reverseHoldTimer;
        private bool wasGrounded;
        private bool controlsEnabled = true;
        private Vector3 groundNormal = Vector3.up;

        // ----- Events (for FX, audio, camera shake, combat later) -----
        /// <summary>Fired on touchdown with the vertical impact speed (m/s).</summary>
        public event Action<float> Landed;
        /// <summary>Fired when the crash state changes (true = crashed).</summary>
        public event Action<bool> CrashStateChanged;

        // ----- Read-only state for visuals, camera, HUD, AI and combat -----
        public BikeStats Stats => stats;
        public RidingAssists Assists => assists;
        public Rigidbody Body => rb;

        /// <summary>Signed speed along the bike's heading (m/s). Negative = reversing.</summary>
        public float ForwardSpeed { get; private set; }
        public float SpeedKmh => Mathf.Abs(ForwardSpeed) * 3.6f;
        public float Speed01 => stats ? Mathf.Clamp01(Mathf.Abs(ForwardSpeed) / stats.TopSpeed) : 0f;

        public float RawSteerInput => rawInput.steer;
        /// <summary>Steering after the steering assist (-1..1).</summary>
        public float SteerInput => steer;
        public float ThrottleInput => rawInput.throttle;
        public float BrakeInput => rawInput.brake;

        public float YawRate => yawRate;
        /// <summary>Acceleration along the heading from engine/brakes/drag this step (m/s²).</summary>
        public float LongitudinalAccel { get; private set; }
        /// <summary>Physically plausible lean for the current turn (deg, + = right). Visual only.</summary>
        public float IdealLeanAngle { get; private set; }
        /// <summary>Handlebar/fork angle for the visuals (deg, + = right).</summary>
        public float ForkAngle { get; private set; }

        public bool IsGrounded => front.grounded || rear.grounded;
        public bool IsSkidding { get; private set; }
        public bool IsCrashed { get; private set; }
        public WheelContact FrontContact => front;
        public WheelContact RearContact => rear;
        public Vector3 GroundNormal => groundNormal;

        /// <summary>Velocity at the start of the current physics step, i.e. before any collision
        /// resolved this step. Use it to measure impact speed in OnCollisionEnter.</summary>
        public Vector3 VelocityBeforeStep { get; private set; }

        private static float Gravity => Physics.gravity.magnitude;

        // =====================================================================
        // Unity lifecycle
        // =====================================================================

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();

            // Only take an input provider from this object if none was injected already.
            if (inputProvider == null)
                inputProvider = GetComponent<IBikeInputProvider>();

            if (!stats)
            {
                Debug.LogError($"{name}: BikeController needs a BikeStats asset.", this);
                enabled = false;
                return;
            }
            if (!frontWheelProbe || !rearWheelProbe)
            {
                Debug.LogError($"{name}: assign both wheel probes on the BikeController.", this);
                enabled = false;
                return;
            }

            ConfigureRigidbody();
        }

        private void ConfigureRigidbody()
        {
            rb.mass = stats.mass;
            rb.useGravity = true;
            rb.linearDamping = 0f;           // we do our own drag
            rb.angularDamping = 0f;          // we write angular velocity ourselves
            rb.maxAngularVelocity = 20f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;              // smooth camera
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;      // no wall tunnelling
            if (centerOfMass)
                rb.centerOfMass = transform.InverseTransformPoint(centerOfMass.position);
        }

        private void FixedUpdate()
        {
            if (IsCrashed) return;

            float dt = Time.fixedDeltaTime;
            VelocityBeforeStep = rb.linearVelocity;

            ReadInput();
            ProbeWheel(frontWheelProbe, ref front);
            ProbeWheel(rearWheelProbe, ref rear);

            bool grounded = IsGrounded;
            groundNormal = GetGroundNormal();

            // Heading frame on the ground plane.
            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, groundNormal).normalized;
            Vector3 right = Vector3.Cross(groundNormal, fwd);
            Vector3 velocity = rb.linearVelocity;
            ForwardSpeed = Vector3.Dot(velocity, fwd);
            float lateralSpeed = Vector3.Dot(velocity, right);

            if (grounded && !wasGrounded) HandleLanding();

            UpdateSteering(dt, grounded);

            ApplySuspension(frontWheelProbe, front);
            ApplySuspension(rearWheelProbe, rear);

            if (grounded)
            {
                float longitudinalAccel = ApplyDrive(dt, fwd);
                ApplyGrip(dt, right, lateralSpeed, longitudinalAccel);
            }
            else
            {
                IsSkidding = false;
                LongitudinalAccel = 0f;
                // Extra gravity in the air for punchier arcade jumps.
                rb.AddForce(Physics.gravity * (stats.airGravityMultiplier - 1f), ForceMode.Acceleration);
            }

            ApplyRotation(grounded);
            UpdateVisualHelpers(grounded);

            wasGrounded = grounded;
        }

        // =====================================================================
        // Input + steering
        // =====================================================================

        private void ReadInput()
        {
            rawInput = controlsEnabled && inputProvider != null ? inputProvider.ReadInput() : BikeInputState.None;
            rawInput.steer = Mathf.Clamp(rawInput.steer, -1f, 1f);
            rawInput.throttle = Mathf.Clamp01(rawInput.throttle);
            rawInput.brake = Mathf.Clamp01(rawInput.brake);
        }

        private void UpdateSteering(float dt, bool grounded)
        {
            float assist = assists ? assists.steeringAssist : 0f;
            float speed01 = Speed01;
            float target = rawInput.steer;

            // Steering assist 1: soften the input at high speed (max 40% less at top speed).
            target *= Mathf.Lerp(1f, 0.6f, assist * speed01);

            // Steering assist 2: smooth the input. Raw = 14 units/s (≈0.07 s to full lock),
            // full assist = 3.5 units/s. Returning to centre is a bit quicker than turning in.
            float rate = Mathf.Lerp(14f, 3.5f, assist);
            bool returning = Mathf.Abs(target) < Mathf.Abs(steer) || Mathf.Sign(target) != Mathf.Sign(steer);
            steer = Mathf.MoveTowards(steer, target, rate * (returning ? 1.6f : 1f) * dt);

            // Speed-sensitive turn rate: limited by the tightest radius at low speed and by
            // the cornering g at high speed (yaw = v / R, lateral accel = v * yaw).
            float v = Mathf.Abs(ForwardSpeed);
            float maxYaw = MaxYawRate(v);

            // Steering assist 3: never ask for more turn than the tyres can hold.
            float gripYaw = EffectiveGrip() * Gravity * 0.95f / Mathf.Max(v, 0.1f);
            maxYaw = Mathf.Lerp(maxYaw, Mathf.Min(maxYaw, gripYaw), assist);

            float direction = ForwardSpeed >= 0f ? 1f : -1f;   // steering inverts when reversing
            float desiredYaw = steer * maxYaw * direction;
            if (!grounded) desiredYaw *= stats.airSteerFactor;

            yawRate = Mathf.MoveTowards(yawRate, desiredYaw, stats.steerResponse * dt);
        }

        /// <summary>Largest yaw rate (rad/s) the steering can request at speed v (m/s).</summary>
        private float MaxYawRate(float v)
        {
            float lowSpeedLimit = v / stats.minTurnRadius;
            float highSpeedLimit = stats.maxCorneringG * Gravity / Mathf.Max(v, 0.1f);
            return Mathf.Min(lowSpeedLimit, highSpeedLimit);
        }

        /// <summary>Tyre grip in g, plus the stability assist's bonus (up to +50%).</summary>
        private float EffectiveGrip()
        {
            float stability = assists ? assists.stabilityAssist : 0f;
            return stats.grip * (1f + 0.5f * stability);
        }

        // =====================================================================
        // Ground detection + suspension
        // =====================================================================

        private void ProbeWheel(Transform probe, ref WheelContact wheel)
        {
            Vector3 up = transform.up;
            // The ray starts at the top of the suspension travel, so the wheel can be pushed
            // up by 'suspensionTravel' before the ray starts inside the ground.
            Vector3 origin = probe.position + up * stats.suspensionTravel;
            float rayLength = stats.suspensionTravel + stats.wheelRadius + groundedTolerance;

            if (Physics.Raycast(origin, -up, out RaycastHit hit, rayLength, groundMask, QueryTriggerInteraction.Ignore))
            {
                wheel.grounded = true;
                wheel.point = hit.point;
                wheel.normal = hit.normal;
                wheel.springLength = hit.distance - stats.wheelRadius;
                wheel.compression = stats.suspensionTravel - wheel.springLength;
                wheel.collider = hit.collider;
            }
            else
            {
                wheel.grounded = false;
                wheel.normal = Vector3.up;
                wheel.springLength = stats.suspensionTravel + groundedTolerance;
                wheel.compression = -groundedTolerance;
                wheel.collider = null;
            }
        }

        private Vector3 GetGroundNormal()
        {
            if (front.grounded && rear.grounded) return (front.normal + rear.normal).normalized;
            if (rear.grounded) return rear.normal;
            if (front.grounded) return front.normal;
            return Vector3.up;
        }

        /// <summary>
        /// Spring-damper per wheel. The spring is preloaded with half the bike's weight so the
        /// wheel sits exactly at its probe when at rest (no sag to model around).
        /// Forces are applied at the centre of mass: pitch is handled by ApplyRotation, which
        /// keeps the bike stable on an arcade budget.
        /// </summary>
        private void ApplySuspension(Transform probe, WheelContact wheel)
        {
            if (!wheel.grounded) return;

            Vector3 up = transform.up;
            float wheelMass = rb.mass * 0.5f;
            float omega = 2f * Mathf.PI * stats.springFrequency;
            float k = wheelMass * omega * omega;                     // spring rate (N/m)
            float c = 2f * stats.dampingRatio * wheelMass * omega;   // damping (N·s/m)

            float force = wheelMass * Gravity + k * wheel.compression;

            // Bump stop: much stiffer in the last 20% of travel so hard landings don't bottom out.
            float bumpStart = stats.suspensionTravel * 0.8f;
            if (wheel.compression > bumpStart)
                force += 4f * k * (wheel.compression - bumpStart);

            float verticalSpeed = Vector3.Dot(rb.GetPointVelocity(probe.position), up);
            force -= c * verticalSpeed;

            if (force > 0f)
                rb.AddForce(up * force, ForceMode.Force);
        }

        private void HandleLanding()
        {
            Vector3 v = rb.linearVelocity;
            float into = Vector3.Dot(v, groundNormal);
            if (into >= 0f) return;

            // Stability assist soaks up part of the vertical landing speed (softer, no bounce).
            float stability = assists ? assists.stabilityAssist : 0f;
            float absorb = Mathf.Lerp(0.3f, 0.85f, stability);
            rb.linearVelocity = v - groundNormal * (into * absorb);

            Landed?.Invoke(-into);
        }

        // =====================================================================
        // Drive: throttle, brakes, reverse, drag
        // =====================================================================

        /// <returns>The longitudinal acceleration used (m/s²), for the friction circle.</returns>
        private float ApplyDrive(float dt, Vector3 fwd)
        {
            float throttle = rawInput.throttle;
            float brake = rawInput.brake;
            float speed = ForwardSpeed;
            float accel = 0f;
            IsSkidding = false;

            bool driveWheelDown = rear.grounded;   // rear-wheel drive
            bool braking = brake > 0.05f;

            if (braking && speed > 0.5f)
            {
                // --- Forward braking, sharing grip with cornering (friction circle) ---
                reverseHoldTimer = 0f;
                float budget = EffectiveGrip() * Gravity;
                float lateral = Mathf.Abs(speed * yawRate);
                float available = Mathf.Sqrt(Mathf.Max(budget * budget - lateral * lateral, 0f));
                float demand = brake * stats.brakeDeceleration;

                float applied = demand;
                if (demand > available)
                {
                    // Brake assist (ABS) backs off to what the tyres can take.
                    float brakeAssist = assists ? assists.brakeAssist : 0f;
                    applied = Mathf.Lerp(demand, available, brakeAssist);
                }

                IsSkidding = applied > available + 0.01f;
                if (IsSkidding) applied *= 0.8f;   // a locked tyre stops worse than a rolling one

                accel = -Mathf.Min(applied, speed / dt);   // never brake past zero
            }
            else if (braking && throttle < 0.1f && driveWheelDown)
            {
                // --- Stopped / nearly stopped: hold, then reverse after a short delay ---
                reverseHoldTimer += dt;
                if (reverseHoldTimer < 0.35f)
                    accel = -speed / dt * 0.5f;                       // hold the bike still
                else if (speed > -stats.ReverseSpeed)
                    accel = -stats.reverseAcceleration * brake;
            }
            else
            {
                reverseHoldTimer = 0f;
                if (throttle > 0.01f && driveWheelDown)
                {
                    if (speed < -0.3f)
                    {
                        // Throttle while rolling backwards = brake the reverse.
                        accel = Mathf.Min(stats.brakeDeceleration * throttle, -speed / dt);
                    }
                    else
                    {
                        float speed01 = Mathf.Clamp01(speed / stats.TopSpeed);
                        accel = throttle * stats.acceleration * stats.accelerationCurve.Evaluate(speed01);
                    }
                }
                else if (throttle <= 0.01f)
                {
                    // --- Coasting: rolling resistance + air drag, never reversing direction ---
                    float resist = stats.rollingResistance + stats.airDrag * speed * speed;
                    accel -= Mathf.Sign(speed) * Mathf.Min(resist, Mathf.Abs(speed) / dt);
                }
            }

            // Downhill overspeed: gently pull back to top speed.
            if (speed > stats.TopSpeed)
                accel -= (speed - stats.TopSpeed) * 2f;

            rb.AddForce(fwd * accel, ForceMode.Acceleration);
            LongitudinalAccel = accel;
            return accel;
        }

        // =====================================================================
        // Grip: removes sideways slip (this is what makes the bike follow its heading)
        // =====================================================================

        private void ApplyGrip(float dt, Vector3 right, float lateralSpeed, float longitudinalAccel)
        {
            float budget = EffectiveGrip() * Gravity;
            // Whatever braking/accelerating used is not available for cornering.
            float lateralBudget = Mathf.Sqrt(Mathf.Max(budget * budget - longitudinalAccel * longitudinalAccel, 0f));
            if (IsSkidding) lateralBudget *= stats.skidGripFactor;

            float wanted = -lateralSpeed * stats.lateralStiffness;
            float noOvershoot = Mathf.Abs(lateralSpeed) / dt;
            wanted = Mathf.Clamp(wanted, -noOvershoot, noOvershoot);
            float lateralAccel = Mathf.Clamp(wanted, -lateralBudget, lateralBudget);

            rb.AddForce(right * lateralAccel, ForceMode.Acceleration);
        }

        // =====================================================================
        // Rotation: yaw from steering, pitch/roll locked to the ground (or level in the air)
        // =====================================================================

        private void ApplyRotation(bool grounded)
        {
            float stability = assists ? assists.stabilityAssist : 0f;
            Vector3 targetUp;
            float alignRate;

            if (grounded)
            {
                targetUp = groundNormal;
                alignRate = Mathf.Lerp(5f, 14f, stability);
            }
            else
            {
                // Look for where we'll land and level the bike to it.
                targetUp = Vector3.up;
                if (Physics.Raycast(rb.position, Vector3.down, out RaycastHit hit, landingProbeDistance,
                        groundMask, QueryTriggerInteraction.Ignore))
                    targetUp = hit.normal;
                alignRate = stats.airAlignSpeed * Mathf.Lerp(0.6f, 1.4f, stability);
            }

            Vector3 alignVelocity = Vector3.zero;
            Quaternion delta = Quaternion.FromToRotation(transform.up, targetUp);
            delta.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;
            if (Mathf.Abs(angle) > 0.01f && !float.IsNaN(axis.x) && axis.sqrMagnitude > 0.5f)
                alignVelocity = axis.normalized * (angle * Mathf.Deg2Rad * alignRate);

            // Written directly: the bike can never tip over, and collisions can't spin it.
            rb.angularVelocity = alignVelocity + transform.up * yawRate;
        }

        private void UpdateVisualHelpers(bool grounded)
        {
            // Lean that balances the turn: tan(lean) = lateral accel / g.
            float lateralAccel = ForwardSpeed * yawRate;
            IdealLeanAngle = grounded ? Mathf.Atan2(lateralAccel, Gravity) * Mathf.Rad2Deg : 0f;

            // Fork angle that matches the current turn radius (big at low speed, tiny at high speed).
            float v = Mathf.Abs(ForwardSpeed);
            float radius = Mathf.Max(stats.minTurnRadius, v * v / (stats.maxCorneringG * Gravity));
            float fullLock = Mathf.Atan(stats.wheelbase / radius) * Mathf.Rad2Deg;
            ForkAngle = Mathf.Clamp(steer * fullLock, -stats.maxForkAngle, stats.maxForkAngle);
        }

        // =====================================================================
        // Public API (respawn, AI, combat, bike/preset swapping)
        // =====================================================================

        public void SetInputProvider(IBikeInputProvider provider) => inputProvider = provider;

        public void SetAssists(RidingAssists newAssists) => assists = newAssists;

        public void SetStats(BikeStats newStats)
        {
            if (!newStats) return;
            stats = newStats;
            if (rb) ConfigureRigidbody();
        }

        /// <summary>Ignore input (cut-scenes, countdown, knocked off...). The bike keeps rolling.</summary>
        public void SetControlsEnabled(bool value) => controlsEnabled = value;

        /// <summary>Freeze the bike in place (true) or hand it back to physics (false).</summary>
        public void SetCrashed(bool crashed)
        {
            if (IsCrashed == crashed) return;
            IsCrashed = crashed;

            if (crashed)
            {
                if (!rb.isKinematic)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                rb.isKinematic = true;
                ResetMotionState();
            }
            else
            {
                rb.isKinematic = false;
            }

            CrashStateChanged?.Invoke(crashed);
        }

        /// <summary>Move the bike instantly (respawn, checkpoints) and stop it.</summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            rb.position = position;
            rb.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            ResetMotionState();
        }

        private void ResetMotionState()
        {
            steer = 0f;
            yawRate = 0f;
            ForwardSpeed = 0f;
            IdealLeanAngle = 0f;
            ForkAngle = 0f;
            IsSkidding = false;
            LongitudinalAccel = 0f;
            wasGrounded = false;
            reverseHoldTimer = 0f;
            VelocityBeforeStep = Vector3.zero;
        }

        // =====================================================================
        // Debug drawing
        // =====================================================================

        private void OnDrawGizmosSelected()
        {
            if (!stats) return;
            DrawProbe(frontWheelProbe, Application.isPlaying ? front : default);
            DrawProbe(rearWheelProbe, Application.isPlaying ? rear : default);
        }

        private void DrawProbe(Transform probe, WheelContact wheel)
        {
            if (!probe) return;
            Vector3 up = transform.up;
            Vector3 origin = probe.position + up * stats.suspensionTravel;
            float length = stats.suspensionTravel + stats.wheelRadius + groundedTolerance;
            Gizmos.color = wheel.grounded ? Color.green : Color.yellow;
            Gizmos.DrawLine(origin, origin - up * length);
            Gizmos.DrawWireSphere(probe.position, stats.wheelRadius);
            if (wheel.grounded)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(wheel.point, wheel.point + wheel.normal * 0.5f);
            }
        }
    }
}
