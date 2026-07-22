using UnityEngine;

namespace Excavator.Trench
{
    /// <summary>
    /// Motor de medicion espacial y rastreo de excavacion.
    /// Muestra la profundidad celda por celda de ITrenchTerrainSource para medir en tiempo real
    /// largo, ancho, profundidad promedio, profundidad maxima, volumen fuera de zona y sobre-excavacion.
    /// </summary>
    public class TrenchExcavationTracker : MonoBehaviour
    {
        [Header("Referencias (Auto-detectadas)")]
        public MonoBehaviour terrainSourceComponent;
        public TrenchExerciseConfig config;

        private ITrenchTerrainSource _terrainSource;

        // Metricas Medidas en Tiempo Real
        public float MeasuredLength { get; private set; }
        public float MeasuredWidth { get; private set; }
        public float AverageDepth { get; private set; }
        public float MaxDepth { get; private set; }
        public float TotalDugVolume { get; private set; }
        public float OutOfBoundsDugVolume { get; private set; }
        public float OverExcavatedVolume { get; private set; }
        public int TotalDugCells { get; private set; }
        public bool HasDiggingStarted { get; private set; }

        void Awake()
        {
            AutoFindReferences();
        }

        private void AutoFindReferences()
        {
            if (terrainSourceComponent != null)
                _terrainSource = terrainSourceComponent as ITrenchTerrainSource;

            if (_terrainSource == null)
            {
                var comp = GetComponent<ITrenchTerrainSource>()
                        ?? GetComponentInChildren<ITrenchTerrainSource>()
                        ?? FindAnyObjectByType<DeformableTrenchArea>();
                _terrainSource = comp;
            }

            if (config == null)
                config = GetComponent<TrenchExerciseConfig>()
                      ?? GetComponentInParent<TrenchExerciseConfig>()
                      ?? FindAnyObjectByType<TrenchExerciseConfig>();
        }

        void Update()
        {
            if (_terrainSource == null) AutoFindReferences();
            if (_terrainSource == null) return;

            AnalyzeExcavationSpatialData();
        }

        /// <summary>
        /// Analiza la grilla de celdas de ITrenchTerrainSource y calcula largo, ancho, profundidad y fallas.
        /// </summary>
        public void AnalyzeExcavationSpatialData()
        {
            int cols = _terrainSource.GridCols;
            int rows = _terrainSource.GridRows;
            if (cols <= 0 || rows <= 0) return;

            float cellW = _terrainSource.AreaWidth / Mathf.Max(1, cols - 1);
            float cellL = _terrainSource.AreaLength / Mathf.Max(1, rows - 1);
            float cellArea = cellW * cellL;

            int minColDug = cols;
            int maxColDug = -1;
            int minRowDug = rows;
            int maxRowDug = -1;

            float depthSum = 0f;
            float maxD = 0f;
            float totalVol = 0f;
            float outOfBoundsVol = 0f;
            float overDugVol = 0f;
            int dugCount = 0;

            float targetMaxDepth = config != null ? config.MaxAllowedDepth : 1.4f;

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    float d = _terrainSource.GetDepthAt(c, r);
                    if (d > 0.05f) // Umbral minimo de presencia de excavacion
                    {
                        dugCount++;
                        depthSum += d;
                        totalVol += d * cellArea;

                        if (d > maxD) maxD = d;

                        if (c < minColDug) minColDug = c;
                        if (c > maxColDug) maxColDug = c;
                        if (r < minRowDug) minRowDug = r;
                        if (r > maxRowDug) maxRowDug = r;

                        // Evaluacion de Fuera de Limite (Bordes exteriores de la grilla)
                        if (c == 0 || c == cols - 1 || r == 0 || r == rows - 1)
                        {
                            outOfBoundsVol += d * cellArea;
                        }

                        // Evaluacion de Sobre-excavacion exceso de profundidad
                        if (d > targetMaxDepth)
                        {
                            overDugVol += (d - targetMaxDepth) * cellArea;
                        }
                    }
                }
            }

            TotalDugCells = dugCount;
            HasDiggingStarted = dugCount > 3;

            if (dugCount > 0)
            {
                AverageDepth = depthSum / dugCount;
                MaxDepth = maxD;

                // Calculo de extension medida de largo y ancho (en metros)
                int colSpan = Mathf.Max(0, maxColDug - minColDug + 1);
                int rowSpan = Mathf.Max(0, maxRowDug - minRowDug + 1);

                MeasuredLength = colSpan * cellW;
                MeasuredWidth = rowSpan * cellL;
            }
            else
            {
                AverageDepth = 0f;
                MaxDepth = 0f;
                MeasuredLength = 0f;
                MeasuredWidth = 0f;
            }

            TotalDugVolume = totalVol;
            OutOfBoundsDugVolume = outOfBoundsVol;
            OverExcavatedVolume = overDugVol;
        }

        public void ResetTrackerState()
        {
            MeasuredLength = 0f;
            MeasuredWidth = 0f;
            AverageDepth = 0f;
            MaxDepth = 0f;
            TotalDugVolume = 0f;
            OutOfBoundsDugVolume = 0f;
            OverExcavatedVolume = 0f;
            TotalDugCells = 0;
            HasDiggingStarted = false;

            if (_terrainSource != null)
            {
                _terrainSource.ResetTerrain();
            }
        }
    }
}
