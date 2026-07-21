using System;

namespace Excavator.Engine
{
    public class EngineSimulationCore
    {
        readonly float idleRpm;
        readonly float maxRpm;
        readonly float baseResponseRate;
        readonly Func<float, float> torqueAtNormalizedRpm;

        public float CurrentRpm { get; private set; }

        public EngineSimulationCore(float idleRpm, float maxRpm, float baseResponseRate, Func<float, float> torqueAtNormalizedRpm)
        {
            this.idleRpm = idleRpm;
            this.maxRpm = Math.Max(maxRpm, idleRpm + 1f);
            this.baseResponseRate = Math.Max(baseResponseRate, 0.01f);
            this.torqueAtNormalizedRpm = torqueAtNormalizedRpm ?? (_ => 1f);
        }

        public void Reset()
        {
            CurrentRpm = 0f;
        }

        public void ForceRpm(float rpm) => CurrentRpm = Clamp(rpm, 0f, maxRpm);

        public void Step(float throttle01, float load01, float dt)
        {
            throttle01 = Clamp01(throttle01);
            load01     = Clamp01(load01);

            float targetRpm = idleRpm + (maxRpm - idleRpm) * throttle01;
            float normalizedRpm = Clamp01((CurrentRpm - idleRpm) / (maxRpm - idleRpm));
            float availableTorque = Clamp01(torqueAtNormalizedRpm(normalizedRpm));




            float effectiveRate = baseResponseRate * Math.Max(availableTorque, 0.05f) * (1f - load01 * 0.85f);


            float alpha = 1f - (float)Math.Exp(-effectiveRate * dt);

            CurrentRpm += (targetRpm - CurrentRpm) * alpha;
            CurrentRpm = Clamp(CurrentRpm, 0f, maxRpm);
        }

        static float Clamp01(float v) => Clamp(v,0f,1f);
        static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
        
    }
}