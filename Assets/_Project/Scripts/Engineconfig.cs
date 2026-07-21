using UnityEngine;

namespace Excavator.Engine
{
    [CreateAssetMenu(fileName = "EngineConfig", menuName = "Excavator/Engine Config")]
    public class EngineConfig : ScriptableObject
    {
        [Header("RPM")]
        public float idleRpm = 800f;
        public float maxRpm  = 2500f;

        [Header("Respuesta del motor")]
        public float baseResponseRate = 3f;

        public AnimationCurve torqueCurve = new AnimationCurve(
            new Keyframe(0f,   0.6f),
            new Keyframe(0.4f, 1f),
            new Keyframe(1f,   0.75f)
        );

        [Header("Arranque")]
        public float startingDuration = 1.2f;

        [Header("Vibracion")]
        public float shakeAmplitude = 0.01f;
        public float shakeFrequency = 25f;

        [Header("Audio")]
        [Range(0f, 1f)] public float idlePitchRange = 0.1f;
        [Range(0f, 1f)] public float revPitchRange  = 0.3f;

        void OnValidate()
        {
            if (maxRpm <= idleRpm)
                maxRpm = idleRpm + 100f;

            baseResponseRate  = Mathf.Max(baseResponseRate, 0.1f);
            startingDuration  = Mathf.Max(startingDuration, 0.1f);
        }
    }
}