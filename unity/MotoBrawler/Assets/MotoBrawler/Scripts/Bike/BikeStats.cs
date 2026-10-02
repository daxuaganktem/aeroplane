using UnityEngine;

namespace MotoBrawler.Bike
{
    /// <summary>
    /// Every tuning value that describes ONE bike: engine, brakes, steering, grip,
    /// suspension, visual lean and geometry. Make one asset per bike model
    /// (Create > MotoBrawler > Bike Stats) and plug it into a BikeController.
    ///
    /// Units: metres, seconds, kilograms, degrees. Speeds the designer types are
    /// in km/h; the controller reads the m/s properties at the bottom.
    /// </summary>
    [CreateAssetMenu(fileName = "BikeStats", menuName = "MotoBrawler/Bike Stats", order = 0)]
    public class BikeStats : ScriptableObject
    {
        [Header("Engine")]
        [Tooltip("Top speed on flat ground with full throttle (km/h).")]
        [Range(40f, 350f)] public float topSpeedKmh = 180f;

        [Tooltip("Peak forward acceleration in m/s² (9.81 = 1 g).")]
        [Range(1f, 20f)] public float acceleration = 8.5f;

        [Tooltip("Engine pull vs. speed. X = speed / top speed (0..1), Y = fraction of 'acceleration'. " +
                 "Must fall to 0 at X = 1 so the bike settles at top speed.")]
        public AnimationCurve accelerationCurve = new AnimationCurve(
            new Keyframe(0f, 1f, 0f, 0f),
            new Keyframe(0.5f, 0.8f, -0.6f, -0.6f),
            new Keyframe(0.85f, 0.35f, -1.8f, -1.8f),
            new Keyframe(1f, 0f, -2.5f, 0f));

        [Tooltip("Top speed when reversing (hold brake while stopped).")]
        [Range(0f, 30f)] public float reverseSpeedKmh = 12f;

        [Tooltip("Acceleration when reversing (m/s²).")]
        [Range(0.5f, 10f)] public float reverseAcceleration = 3f;

        [Header("Braking & drag")]
        [Tooltip("Deceleration at full brake in m/s². Compare with grip × 9.81: higher than that and the " +
                 "wheels lock unless Brake Assist steps in.")]
        [Range(2f, 20f)] public float brakeDeceleration = 10f;

        [Tooltip("Constant slow-down while coasting (m/s²). Tyres + drivetrain.")]
        [Range(0f, 3f)] public float rollingResistance = 0.5f;

        [Tooltip("Air drag while coasting: deceleration = airDrag × speed² (speed in m/s).")]
        [Range(0f, 0.01f)] public float airDrag = 0.0012f;

        [Header("Steering")]
        [Tooltip("Tightest turn radius at walking speed (m). Smaller = tighter U-turns.")]
        [Range(1.5f, 15f)] public float minTurnRadius = 3.5f;

        [Tooltip("Sideways acceleration (in g) the steering asks for at full lock once moving fast. " +
                 "This is what makes steering gentler at high speed.")]
        [Range(0.3f, 3f)] public float maxCorneringG = 1.35f;

        [Tooltip("How fast the yaw rate catches up with the steering request (rad/s²). Higher = snappier.")]
        [Range(1f, 30f)] public float steerResponse = 9f;

        [Tooltip("Fraction of normal steering available in the air (mid-air correction).")]
        [Range(0f, 1f)] public float airSteerFactor = 0.25f;

        [Header("Grip")]
        [Tooltip("Tyre grip in g. Total braking + cornering force the tyres can give before sliding.")]
        [Range(0.3f, 3f)] public float grip = 1.15f;

        [Tooltip("How quickly the tyres kill sideways slip (1/s). Higher = more 'on rails'.")]
        [Range(1f, 40f)] public float lateralStiffness = 12f;

        [Tooltip("Grip multiplier while the wheels are locked/skidding.")]
        [Range(0.05f, 1f)] public float skidGripFactor = 0.35f;

        [Header("Suspension (per wheel)")]
        [Tooltip("Suspension travel above the rest position (m).")]
        [Range(0.05f, 0.4f)] public float suspensionTravel = 0.18f;

        [Tooltip("Spring natural frequency in Hz. 1.5 = soft and floaty, 3 = stiff and sporty.")]
        [Range(0.5f, 5f)] public float springFrequency = 2.2f;

        [Tooltip("Damping ratio. 0.3 = bouncy, 1 = no overshoot.")]
        [Range(0.05f, 1.5f)] public float dampingRatio = 0.55f;

        [Header("Air")]
        [Tooltip("Gravity multiplier while airborne. >1 gives snappier arcade jumps.")]
        [Range(1f, 4f)] public float airGravityMultiplier = 1.6f;

        [Tooltip("How fast the bike rotates to land level while airborne (1/s).")]
        [Range(0.5f, 15f)] public float airAlignSpeed = 3.5f;

        [Header("Visual lean")]
        [Tooltip("Maximum visual lean angle in degrees.")]
        [Range(0f, 65f)] public float maxLeanAngle = 48f;

        [Tooltip("How fast the visual lean follows its target (1/s).")]
        [Range(1f, 25f)] public float leanSpeed = 7f;

        [Tooltip("Extra lean straight from the steering input (0..1 of max lean), for instant feedback " +
                 "before the physics lean builds up.")]
        [Range(0f, 0.6f)] public float leanFromInput = 0.15f;

        [Header("Mass & geometry")]
        [Tooltip("Bike + rider mass (kg). Matters in collisions (later: combat shoves, AI bumps).")]
        [Range(80f, 600f)] public float mass = 230f;

        [Tooltip("Distance between front and rear axle (m).")]
        [Range(0.8f, 2.2f)] public float wheelbase = 1.4f;

        [Tooltip("Wheel radius (m). Drives the visual wheel spin rate.")]
        [Range(0.15f, 0.6f)] public float wheelRadius = 0.32f;

        [Tooltip("Largest visual handlebar/fork rotation (degrees).")]
        [Range(5f, 60f)] public float maxForkAngle = 32f;

        // ----- Derived values (m/s) used by code -----
        public float TopSpeed => topSpeedKmh / 3.6f;
        public float ReverseSpeed => reverseSpeedKmh / 3.6f;
    }
}
