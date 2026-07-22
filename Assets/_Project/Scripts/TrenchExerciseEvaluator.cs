using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Excavator.Trench
{
    public enum TrenchExerciseState
    {
        NotStarted,
        InProgress,
        Completed,
        Failed
    }

    /// <summary>
    /// Evaluador independiente de reglas de ejercicio de excavacion de zanja.
    /// Separa estrictamente la evaluacion del control de la maquina y de la vista UI.
    /// Administra estados del ejercicio, temporizador, deteccion de errores y reinicio.
    /// </summary>
    public class TrenchExerciseEvaluator : MonoBehaviour
    {
        [Header("Referencias (Auto-detectadas)")]
        public TrenchExerciseConfig config;
        public TrenchExcavationTracker tracker;

        // Estado del Ejercicio
        public TrenchExerciseState State { get; private set; } = TrenchExerciseState.NotStarted;
        public float ElapsedTime { get; private set; } = 0f;
        public float CompletionPercentage { get; private set; } = 0f;

        // Banderas de Cumplimiento Dimensional
        public bool IsLengthValid { get; private set; }
        public bool IsWidthValid { get; private set; }
        public bool IsDepthValid { get; private set; }

        // Banderas de Deteccion de Errores / Faltas
        public bool HasOutOfBoundsFault { get; private set; }
        public bool HasOverExcavationFault { get; private set; }
        public bool HasTimeoutFault { get; private set; }

        // Eventos
        public event Action<TrenchExerciseState> StateChanged;
        public event Action ExerciseCompleted;
        public event Action ExerciseRestarted;

        void Awake()
        {
            AutoFindReferences();
        }

        private void AutoFindReferences()
        {
            if (config == null)
                config = GetComponent<TrenchExerciseConfig>()
                      ?? GetComponentInParent<TrenchExerciseConfig>()
                      ?? FindAnyObjectByType<TrenchExerciseConfig>();

            if (tracker == null)
                tracker = GetComponent<TrenchExcavationTracker>()
                       ?? GetComponentInParent<TrenchExcavationTracker>()
                       ?? FindAnyObjectByType<TrenchExcavationTracker>();
        }

        void Update()
        {
            if (config == null || tracker == null) AutoFindReferences();

            // Tecla de acceso directo para reiniciar el ejercicio (Tecla K por defecto)
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.kKey.wasPressedThisFrame)
            {
                RestartExercise();
                return;
            }

            if (State == TrenchExerciseState.Completed || State == TrenchExerciseState.Failed)
                return;

            // Iniciar ejercicio al detectar las primeras paleadas de tierra
            if (State == TrenchExerciseState.NotStarted)
            {
                if (tracker != null && tracker.HasDiggingStarted)
                {
                    ChangeState(TrenchExerciseState.InProgress);
                }
            }

            if (State == TrenchExerciseState.InProgress)
            {
                ElapsedTime += Time.deltaTime;

                EvaluateRules();
            }
        }

        private void EvaluateRules()
        {
            if (config == null || tracker == null) return;

            // 1. Validacion Dimensional
            float lenErr = Mathf.Abs(tracker.MeasuredLength - config.targetLength);
            float widthErr = Mathf.Abs(tracker.MeasuredWidth - config.targetWidth);
            float depthErr = Mathf.Abs(tracker.AverageDepth - config.targetDepth);

            IsLengthValid = lenErr <= config.toleranceMeters;
            IsWidthValid = widthErr <= config.toleranceMeters;
            IsDepthValid = depthErr <= config.toleranceMeters;

            // 2. Deteccion de Faltas y Errores
            HasOutOfBoundsFault = tracker.OutOfBoundsDugVolume > 0.08f;
            HasOverExcavationFault = tracker.OverExcavatedVolume > 0.08f;

            if (config.maxTimeSeconds > 0f && ElapsedTime > config.maxTimeSeconds)
            {
                HasTimeoutFault = true;
            }

            // 3. Calculo de Porcentaje de Avance Global (0-100%)
            float targetVol = config.targetLength * config.targetWidth * config.targetDepth;
            float dugRatio = targetVol > 0f ? Mathf.Clamp01(tracker.TotalDugVolume / targetVol) : 0f;

            float dimFactor = (IsLengthValid ? 0.33f : 0.15f) +
                              (IsWidthValid ? 0.33f : 0.15f) +
                              (IsDepthValid ? 0.34f : 0.15f);

            CompletionPercentage = Mathf.Clamp(dugRatio * dimFactor * 100f, 0f, 100f);

            // 4. Evaluacion de Condicion de Completado
            if (dugRatio >= (config.minVolumeCompletionPercent / 100f) && IsLengthValid && IsWidthValid && IsDepthValid && !HasOutOfBoundsFault)
            {
                CompletionPercentage = 100f;
                ChangeState(TrenchExerciseState.Completed);
                ExerciseCompleted?.Invoke();
            }
            else if (HasTimeoutFault)
            {
                ChangeState(TrenchExerciseState.Failed);
            }
        }

        public void RestartExercise()
        {
            ElapsedTime = 0f;
            CompletionPercentage = 0f;
            IsLengthValid = false;
            IsWidthValid = false;
            IsDepthValid = false;
            HasOutOfBoundsFault = false;
            HasOverExcavationFault = false;
            HasTimeoutFault = false;

            if (tracker != null)
            {
                tracker.ResetTrackerState();
            }

            ChangeState(TrenchExerciseState.NotStarted);
            ExerciseRestarted?.Invoke();
            Debug.Log("[TrenchExerciseEvaluator] Ejercicio de zanja reiniciado correctamente.");
        }

        private void ChangeState(TrenchExerciseState next)
        {
            State = next;
            StateChanged?.Invoke(State);
        }
    }
}
