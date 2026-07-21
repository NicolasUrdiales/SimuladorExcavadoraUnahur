using UnityEngine;

namespace Excavator.Engine
{
    public class EngineVibrationController : MonoBehaviour
    {
        [SerializeField] EngineController engine;
        [SerializeField] Transform cameraPivot;

        Vector3 basePosition;
        float   currentIntensity;

        void Awake()
        {
            if (cameraPivot != null)
                basePosition = cameraPivot.localPosition;
        }

        void OnEnable()
        {
            if (engine != null) engine.RPMChanged += HandleRPMChanged;
        }

        void OnDisable()
        {
            if (engine != null) engine.RPMChanged -= HandleRPMChanged;
        }

        void HandleRPMChanged(float rpm)
        {
            if (engine.Config == null) return;
            currentIntensity = rpm / engine.Config.maxRpm; // maxRPM → maxRpm
        }

        void Update()
        {
            if (cameraPivot == null || engine == null || engine.Config == null) return;

            var   cfg    = engine.Config;
            float noiseX = Mathf.PerlinNoise(Time.time * cfg.shakeFrequency, 0f)       - 0.5f;
            float noiseY = Mathf.PerlinNoise(0f,                              Time.time * cfg.shakeFrequency) - 0.5f;

            cameraPivot.localPosition = basePosition + new Vector3(
                noiseX * cfg.shakeAmplitude * currentIntensity,
                noiseY * cfg.shakeAmplitude * currentIntensity,
                0f);
        }
    }
}