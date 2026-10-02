using System;
using UnityEngine;

namespace MotoBrawler.Controls
{
    public enum SteeringMode
    {
        Buttons = 0,
        Tilt = 1
    }

    /// <summary>
    /// Player control preferences, saved in PlayerPrefs. Static so the mobile UI, the input
    /// reader and (later) a settings menu all share one source of truth. Values are cached
    /// in memory because PlayerPrefs reads are slow on Android and we read them every frame.
    /// </summary>
    public static class ControlSettings
    {
        private const string SteeringKey = "moto.steeringMode";
        private const string AutoThrottleKey = "moto.autoThrottle";
        private const string TiltAngleKey = "moto.tiltAngle";
        private const string TiltZeroKey = "moto.tiltZero";

        private static bool loaded;
        private static SteeringMode steering;
        private static bool autoThrottle;
        private static float tiltFullLockAngle;
        private static float tiltZeroAngle;

        /// <summary>Raised whenever any setting changes.</summary>
        public static event Action Changed;

        public static SteeringMode Steering
        {
            get { Load(); return steering; }
            set { Load(); steering = value; PlayerPrefs.SetInt(SteeringKey, (int)value); Save(); }
        }

        /// <summary>Throttle is always on unless braking (casual play).</summary>
        public static bool AutoThrottle
        {
            get { Load(); return autoThrottle; }
            set { Load(); autoThrottle = value; PlayerPrefs.SetInt(AutoThrottleKey, value ? 1 : 0); Save(); }
        }

        /// <summary>Device roll (deg) that gives full steering lock. Lower = more sensitive.</summary>
        public static float TiltFullLockAngle
        {
            get { Load(); return tiltFullLockAngle; }
            set
            {
                Load();
                tiltFullLockAngle = Mathf.Clamp(value, 5f, 60f);
                PlayerPrefs.SetFloat(TiltAngleKey, tiltFullLockAngle);
                Save();
            }
        }

        /// <summary>Device roll (deg) treated as "straight ahead" (set by Calibrate).</summary>
        public static float TiltZeroAngle
        {
            get { Load(); return tiltZeroAngle; }
            set { Load(); tiltZeroAngle = value; PlayerPrefs.SetFloat(TiltZeroKey, value); Save(); }
        }

        private static void Load()
        {
            if (loaded) return;
            loaded = true;
            steering = (SteeringMode)PlayerPrefs.GetInt(SteeringKey, (int)SteeringMode.Buttons);
            autoThrottle = PlayerPrefs.GetInt(AutoThrottleKey, 0) == 1;
            tiltFullLockAngle = PlayerPrefs.GetFloat(TiltAngleKey, 25f);
            tiltZeroAngle = PlayerPrefs.GetFloat(TiltZeroKey, 0f);
        }

        private static void Save()
        {
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        // Domain reload may be disabled (Enter Play Mode Options): reset the cache each play.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            loaded = false;
            Changed = null;
        }
    }
}
