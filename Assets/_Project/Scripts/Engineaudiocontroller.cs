using UnityEngine;

namespace Excavator.Engine
{
    [RequireComponent(typeof(EngineController))]
    public class EngineAudioController : MonoBehaviour
    {
        [SerializeField] EngineController engine;
        [SerializeField] AudioSource starterSound;
        [SerializeField] AudioSource idleLoop;
        [SerializeField] AudioSource revLoop;

        void OnEnable()
        {
            if (engine == null) return;
            engine.RPMChanged    += HandleRPMChanged;
            engine.StarterBegan  += HandleStarterBegan;
            engine.EngineStarted += HandleEngineStarted;
            engine.StateChanged  += HandleStateChanged;
        }

        void OnDisable()
        {
            if (engine == null) return;
            engine.RPMChanged    -= HandleRPMChanged;
            engine.StarterBegan  -= HandleStarterBegan;
            engine.EngineStarted -= HandleEngineStarted;
            engine.StateChanged  -= HandleStateChanged;
        }

        void HandleStarterBegan() => starterSound?.Play();

        void HandleEngineStarted()
        {
            if (idleLoop != null) { idleLoop.loop = true; idleLoop.Play(); }
            if (revLoop  != null) { revLoop.loop  = true; revLoop.Play();  }
        }

        void HandleRPMChanged(float rpm)
        {
            if (engine.Config == null) return;
            var cfg = engine.Config;
            float t = Mathf.InverseLerp(cfg.idleRpm, cfg.maxRpm, rpm); // idleRPM/maxRPM → idleRpm/maxRpm

            if (idleLoop != null)
            {
                idleLoop.volume = 1f - t;
                idleLoop.pitch  = 1f + t * cfg.idlePitchRange;
            }
            if (revLoop != null)
            {
                revLoop.volume = t;
                revLoop.pitch  = 0.9f + t * cfg.revPitchRange;
            }
        }

        void HandleStateChanged(EngineStateId state)
        {
            if (state == EngineStateId.Off)
            {
                idleLoop?.Stop();
                revLoop?.Stop();
            }
        }
    }
}