using System;
using UnityEngine;

namespace Excavator.Engine
{
    public class EngineController : MonoBehaviour
    {
        [SerializeField] EngineConfig config;

        [Tooltip("Debe implementar IIgnitionInput (ej: KeyboardIgnitionInput). " +
                 "Si queda vacio se busca automaticamente en el GameObject.")]
        [SerializeField] MonoBehaviour ignitionInputSource;

        [Tooltip("Debe implementar IThrottleInput (ej: ExcavatorMovement). " +
                 "Si queda vacio se busca automaticamente en el GameObject.")]
        [SerializeField] MonoBehaviour throttleInputSource;

        [Tooltip("Opcional. Debe implementar IEngineLoadSource. Si queda vacio se usa carga cero.")]
        [SerializeField] MonoBehaviour loadInputSource;

        IIgnitionInput    ignition;
        IThrottleInput    throttle;
        IEngineLoadSource load;
        IEngineState      currentState;

        // -------------------------------------------------------
        //  Propiedades publicas
        // -------------------------------------------------------
        public EngineConfig         Config     => config;
        public EngineSimulationCore Simulation { get; private set; }
        public float                CurrentRpm => Simulation?.CurrentRpm ?? 0f;
        public EngineStateId        State      => currentState?.Id ?? EngineStateId.Off;

        // -------------------------------------------------------
        //  Eventos
        // -------------------------------------------------------
        public event Action<float>         RPMChanged;
        public event Action<EngineStateId> StateChanged;
        public event Action                StarterBegan;
        public event Action                EngineStarted;

        // -------------------------------------------------------
        //  Ciclo de vida
        // -------------------------------------------------------
        void Awake()
        {
            // Intentar cast desde campos serializados
            ignition = ignitionInputSource as IIgnitionInput;
            throttle = throttleInputSource as IThrottleInput;
            load     = loadInputSource     as IEngineLoadSource ?? new NullLoadSource();

            // Auto-buscar en el mismo GameObject si no se asignaron
            if (ignition == null)
                ignition = GetComponent<IIgnitionInput>()
                        ?? GetComponentInChildren<IIgnitionInput>();

            if (throttle == null)
                throttle = GetComponent<IThrottleInput>()
                        ?? GetComponentInChildren<IThrottleInput>()
                        ?? GetComponentInParent<IThrottleInput>();

            if (ignition == null)
                Debug.LogWarning("[EngineController] No se encontro IIgnitionInput. " +
                                 "Asigna KeyboardIgnitionInput o similar.", this);

            if (throttle == null)
                Debug.LogWarning("[EngineController] No se encontro IThrottleInput. " +
                                 "Las RPM no subiran con el movimiento.", this);

            // Crear config por defecto si no se asigno ninguna
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<EngineConfig>();
                Debug.LogWarning("[EngineController] No se asigno EngineConfig. " +
                                 "Usando valores por defecto (idle=800, max=2500 rpm).", this);
            }

            Simulation = new EngineSimulationCore(
                config.idleRpm,
                config.maxRpm,
                config.baseResponseRate,
                normalizedRpm => config.torqueCurve.Evaluate(normalizedRpm)
            );
        }

        void OnEnable()
        {
            if (ignition == null) return;
            ignition.ContactToggled += HandleContactToggled;
            ignition.StarterEngaged += HandleStarterEngaged;
            ChangeState(new OffState());
        }

        void OnDisable()
        {
            if (ignition == null) return;
            ignition.ContactToggled -= HandleContactToggled;
            ignition.StarterEngaged -= HandleStarterEngaged;
        }

        void Update()
        {
            ignition?.Tick();
        }

        void FixedUpdate()
        {
            if (currentState == null) return;
            currentState.Tick(this, Time.fixedDeltaTime);
            RPMChanged?.Invoke(CurrentRpm);
        }

        // -------------------------------------------------------
        //  Handlers de ignicion
        // -------------------------------------------------------
        void HandleContactToggled()
        {
            switch (State)
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
            if (State == EngineStateId.ContactOn)
                ChangeState(new StartingState());
        }

        // -------------------------------------------------------
        //  API publica
        // -------------------------------------------------------
        public void ChangeState(IEngineState next)
        {
            currentState?.Exit(this);
            currentState = next;
            StateChanged?.Invoke(currentState.Id);
            currentState.Enter(this);
        }

        /// <summary>Throttle 0-1 — solo cuando el motor esta Running.</summary>
        public float ReadThrottle() =>
            State == EngineStateId.Running ? throttle?.ReadThrottle() ?? 0f : 0f;

        public float ReadLoad() => load?.ReadLoad01() ?? 0f;

        public void RaiseStarterBegan()  => StarterBegan?.Invoke();
        public void RaiseEngineStarted() => EngineStarted?.Invoke();
    }
}