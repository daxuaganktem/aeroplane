using UnityEngine;

namespace MotoBrawler.Bike
{
    /// <summary>
    /// Player-facing driving aids, kept separate from BikeStats so the same bike can be
    /// ridden in "Arcade" or "Realistic" mode. Create one asset per preset
    /// (Create > MotoBrawler > Riding Assists) or use the context-menu items on the asset
    /// ("Load Arcade Preset" / "Load Realistic Preset").
    ///
    /// Every slider is 0 = no help, 1 = maximum help, except crash speed.
    /// </summary>
    [CreateAssetMenu(fileName = "RidingAssists", menuName = "MotoBrawler/Riding Assists", order = 1)]
    public class RidingAssists : ScriptableObject
    {
        [Tooltip("Name shown in the debug HUD and on the preset button.")]
        public string displayName = "Arcade";

        [Tooltip("Smooths steering input, softens it at high speed and never asks for more turn " +
                 "than the tyres can hold. 0 = raw input.")]
        [Range(0f, 1f)] public float steeringAssist = 0.8f;

        [Tooltip("How strongly the bike self-corrects: holds its line (kills sideways slides), " +
                 "levels on bumps and in the air, softens landings.")]
        [Range(0f, 1f)] public float stabilityAssist = 0.9f;

        [Tooltip("ABS: reduces brake force when it would lock the wheels (e.g. braking mid-corner). " +
                 "0 = wheels lock and skid.")]
        [Range(0f, 1f)] public float brakeAssist = 1f;

        [Tooltip("Crash tolerance: head-on impact speed (km/h) needed to crash. Higher = more forgiving.")]
        [Range(10f, 200f)] public float crashSpeedKmh = 75f;

        public float CrashSpeed => crashSpeedKmh / 3.6f;

        [ContextMenu("Load Arcade Preset")]
        public void LoadArcadePreset()
        {
            displayName = "Arcade";
            steeringAssist = 0.8f;
            stabilityAssist = 0.9f;
            brakeAssist = 1f;
            crashSpeedKmh = 75f;
        }

        [ContextMenu("Load Realistic Preset")]
        public void LoadRealisticPreset()
        {
            displayName = "Realistic";
            steeringAssist = 0.15f;
            stabilityAssist = 0.2f;
            brakeAssist = 0f;
            crashSpeedKmh = 30f;
        }
    }
}
