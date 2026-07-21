using UnityEngine;

namespace Excavator.Engine
{
    public enum EngineStateId
    {
        Off,
        ContactOn,
        Starting,
        Running,
        Stopping
    }

    public interface IEngineState
    {
        EngineStateId Id { get; }
        void Enter(EngineController ctx);
        void Tick(EngineController ctx, float dt);
        void Exit(EngineController ctx);
    }

    // -------------------------------------------------------
    //  Estados concretos
    // -------------------------------------------------------

    public class OffState : IEngineState
    {
        public EngineStateId Id => EngineStateId.Off;
        public void Enter(EngineController ctx) { }           // firma corregida (sin dt)
        public void Tick(EngineController ctx, float dt) { }  // metodo faltante agregado
        public void Exit(EngineController ctx) { }
    }

    public class ContactOnState : IEngineState
    {
        public EngineStateId Id => EngineStateId.ContactOn;
        public void Enter(EngineController ctx) { }
        public void Tick(EngineController ctx, float dt) { }
        public void Exit(EngineController ctx) { }
    }

    public class StartingState : IEngineState
    {
        public EngineStateId Id => EngineStateId.Starting;
        float timer;

        public void Enter(EngineController ctx)
        {
            timer = 0f;
            ctx.RaiseStarterBegan();
        }

        public void Tick(EngineController ctx, float dt)
        {
            timer += dt;
            float t = Mathf.Clamp01(timer / ctx.Config.startingDuration);
            ctx.Simulation.ForceRpm(Mathf.Lerp(0f, ctx.Config.idleRpm, t));

            if (timer >= ctx.Config.startingDuration)
                ctx.ChangeState(new RunningState());
        }

        public void Exit(EngineController ctx) { }
    }

    public class RunningState : IEngineState
    {
        public EngineStateId Id => EngineStateId.Running;

        public void Enter(EngineController ctx) => ctx.RaiseEngineStarted();

        public void Tick(EngineController ctx, float dt)
        {
            float throttle = ctx.ReadThrottle();
            float load     = ctx.ReadLoad();
            ctx.Simulation.Step(throttle, load, dt);
        }

        public void Exit(EngineController ctx) => ctx.Simulation.Reset(); // Reset() con mayuscula
    }
}