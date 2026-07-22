using UnityEngine;

namespace Excavator.Trench
{
    /// <summary>
    /// Modelo de datos y configuracion parametrica para ejercicios de excavacion de zanja.
    /// Define dimensiones objetivo, tolerancias y limites de tiempo.
    /// </summary>
    public class TrenchExerciseConfig : MonoBehaviour
    {
        [Header("Dimensiones Objetivo (Metros)")]
        [Tooltip("Largo objetivo de la zanja a excavar en metros (eje X).")]
        public float targetLength = 6.0f;

        [Tooltip("Ancho objetivo de la zanja a excavar en metros (eje Z).")]
        public float targetWidth = 3.0f;

        [Tooltip("Profundidad objetivo deseada en metros.")]
        public float targetDepth = 1.20f;

        [Header("Tolerancias y Margenes Permitidos")]
        [Tooltip("Tolerancia dimensional permitida (+/- metros) para validar largo, ancho y profundidad.")]
        public float toleranceMeters = 0.20f;

        [Tooltip("Margen de advertencia por excavar fuera de la zona delimitada permitida (metros).")]
        public float allowedOutOfBoundsMargin = 0.40f;

        [Header("Limite de Tiempo")]
        [Tooltip("Tiempo maximo permitido para completar la zanja en segundos. 0 = Sin limite de tiempo.")]
        public float maxTimeSeconds = 300f; // 5 minutos por defecto

        [Header("Criterios de Aprobacion")]
        [Tooltip("Porcentaje minimo de volumen excavado para considerar la zanja completa (0-100%).")]
        public float minVolumeCompletionPercent = 90.0f;

        public float MaxAllowedDepth => targetDepth + toleranceMeters;
        public float MinAllowedDepth => Mathf.Max(0.10f, targetDepth - toleranceMeters);
    }
}
