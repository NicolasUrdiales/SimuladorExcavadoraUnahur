using System;
using UnityEngine;


namespace Excavator.Engine
{
    public class EngineController: MonoBehaviour
    {
        [SerializeField] EngineConfig config;

        [Tooltip("Debe implementar IIgnitionInput")]
        [SerializeField] MonoBehaviour ignitionInputSource;



        [Tooltip("Debe implementar IThrottleInput")]
        [SerializeField] MonoBehaviour throttleInputSource;

        [Tooltip("Opcional. Debe implementar IEngineLoadSource. Si queda vacio se usa NullLoadSource")]
        [SerializeField] MonoBehaviour loadInputSource;

        IIgnitionInput ignition;
        IThrottleInput throttle;
        IEngineLoadSource load;

        IEngineState currentState;


        public EngineConfig Config => config;
        public EngineSimulationCore Simulation {get; private set;}
        public float CurrentRpm => Simulation.CurrentRpm;

        public EngineState State => currentState?.State ?? EngineState.Off;


        public event Action<float> RPMChanged;
        public event Action<EngineStateId> StateChanged;

        public event Action StarterBegan;

        public event Action EngineStarted;



        void Awake()
        {
            ignition = ignitionInputSource as IIgnitionInput;
            throttle = throttleInputSurce as IThrottleInput;
            load = loadInputSource as IEngineLoadSource ?? new NullLoadSource();


            if (ignition == null) Debug.LogError($"{nameof(ignitionInputSource)}  debe implementar IIgnitionInput", this);

            if(throttle == null) Debug.LogError($"{nameof(throttleInputSource)} debe implementar IThrottleInput", this);

            Simulation = new EngineSimulationCore(
                config.idleRpm,
                config.maxRpm,
                config.baseResponseRate,
                normalizedRpm => config.torqueCurve.Evaluate(normalizedRpm)
            );

        }


        void OnEnable()
        {
            ignition.ContactToggled += HandleContactToggled;
            ignition.StarterEngaged += HandleStartedEngaged;
            ChangeState(new OffState());
        }


        void OnDisable()
        {
            ignition.ContactToggled -= HandleContactToggled;
            ignition.StartedEngaged -= HandleStarterEngaged;
        }
        void Update() => ignition.Tick();

        void FixedUpdate()
        {
            currentState.Tick(this, Time.fixedDeltaTime);
            RPMCharged?.Invoke(CurrentRPM);
        }

          void HandleContactToggled()
        {
            switch (CurrentStateId)
            {
                case EngineStateId.Off:
                    ChangeState(new ContactOnState());
                    break;
                case EngineStateId.ContactOn:
                case EngineStateId.Running:
                    ChangeState(new OffState());
                    break;
            }
        }
 
        void HandleStarterEngaged()
        {
            if (CurrentStateId == EngineStateId.ContactOn)
                ChangeState(new StartingState());
        }
 
        public void ChangeState(IEngineState next)
        {
            currentState?.Exit(this);
            currentState = next;
            StateChanged?.Invoke(currentState.Id);
            currentState.Enter(this);
        }
 
        public float ReadThrottle() =>
            CurrentStateId == EngineStateId.Running ? throttle.ReadThrottle() : 0f;
 
        public float ReadLoad() => load.ReadLoad01();
 
        public void RaiseStarterBegan() => StarterBegan?.Invoke();
        public void RaiseEngineStarted() => EngineStarted?.Invoke();
    }

}