using UnityEngine;

namespace MotoBrawler.Core
{
    /// <summary>
    /// Mobile runtime settings: 60 fps target, landscape only, screen never sleeps.
    /// Put one in every gameplay scene (or a bootstrap scene).
    /// </summary>
    public class FrameRateSettings : MonoBehaviour
    {
        [SerializeField] private int targetFrameRate = 60;
        [SerializeField] private bool landscapeOnly = true;
        [SerializeField] private bool neverSleep = true;

        private void Awake()
        {
            // Mobile ignores vSyncCount and defaults to 30 fps unless targetFrameRate is set.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = targetFrameRate;

            if (neverSleep) Screen.sleepTimeout = SleepTimeout.NeverSleep;

            if (landscapeOnly)
            {
                Screen.autorotateToPortrait = false;
                Screen.autorotateToPortraitUpsideDown = false;
                Screen.autorotateToLandscapeLeft = true;
                Screen.autorotateToLandscapeRight = true;
                Screen.orientation = ScreenOrientation.AutoRotation;
            }
        }
    }
}
