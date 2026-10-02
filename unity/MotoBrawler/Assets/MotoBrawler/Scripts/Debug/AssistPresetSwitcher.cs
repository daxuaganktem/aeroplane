using MotoBrawler.Bike;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MotoBrawler.DebugTools
{
    /// <summary>
    /// Cycles the bike through RidingAssists presets (Arcade / Realistic / your own) with
    /// the P key or an on-screen button, and remembers the choice.
    /// </summary>
    public class AssistPresetSwitcher : MonoBehaviour
    {
        private const string PrefKey = "moto.assistPreset";

        [SerializeField] private BikeController bike;
        [SerializeField] private RidingAssists[] presets;

        [Header("Optional UI")]
        [SerializeField] private Button cycleButton;
        [SerializeField] private TMP_Text buttonLabel;

        private int index;

        public RidingAssists Current => presets != null && presets.Length > 0 ? presets[index] : null;

        private void Awake()
        {
            if (cycleButton) cycleButton.onClick.AddListener(Next);
        }

        private void Start()
        {
            if (presets == null || presets.Length == 0) return;
            index = Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, 0), 0, presets.Length - 1);
            Apply();
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.pKey.wasPressedThisFrame) Next();
        }

        public void Next()
        {
            if (presets == null || presets.Length == 0) return;
            index = (index + 1) % presets.Length;
            PlayerPrefs.SetInt(PrefKey, index);
            Apply();
        }

        private void Apply()
        {
            RidingAssists preset = presets[index];
            if (bike) bike.SetAssists(preset);
            if (buttonLabel) buttonLabel.text = preset ? $"Assists: {preset.displayName}" : "Assists: none";
        }
    }
}
