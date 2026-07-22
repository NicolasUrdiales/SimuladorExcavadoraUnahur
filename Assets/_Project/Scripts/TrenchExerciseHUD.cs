using UnityEngine;

namespace Excavator.Trench
{
    /// <summary>
    /// Vista de interfaz de usuario IMGUI pura para el ejercicio de excavacion de zanja.
    /// Desacoplada de la logica de evaluacion; lee los datos de TrenchExerciseConfig,
    /// TrenchExcavationTracker y TrenchExerciseEvaluator.
    /// </summary>
    public class TrenchExerciseHUD : MonoBehaviour
    {
        [Header("Referencias (Auto-detectadas)")]
        public TrenchExerciseConfig config;
        public TrenchExcavationTracker tracker;
        public TrenchExerciseEvaluator evaluator;

        [Header("Visibilidad")]
        public bool showHUD = true;
        public float hudWidth = 340f;
        public float hudX = 16f; // Distancia desde la izquierda
        public float hudY = 16f; // Distancia desde arriba

        // IMGUI
        private Texture2D _white;
        private GUIStyle _headerStyle;
        private GUIStyle _labelBoldStyle;
        private GUIStyle _metricStyle;
        private GUIStyle _badgeStyle;
        private GUIStyle _hintStyle;
        private bool _guiReady;

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

            if (evaluator == null)
                evaluator = GetComponent<TrenchExerciseEvaluator>()
                         ?? GetComponentInParent<TrenchExerciseEvaluator>()
                         ?? FindAnyObjectByType<TrenchExerciseEvaluator>();
        }

        void OnGUI()
        {
            if (!showHUD) return;
            EnsureGUI();

            if (config == null || tracker == null || evaluator == null) AutoFindReferences();
            if (config == null || tracker == null || evaluator == null) return;

            float px = hudX;
            float py = hudY;
            float pw = hudWidth;
            float ph = 195f;

            // Fondo Panel Principal IMGUI (#0A0E17)
            DrawRect(new Rect(px, py, pw, ph), new Color(0.04f, 0.06f, 0.10f, 0.94f));

            // Borde Superior segun estado del ejercicio
            Color stateColor = evaluator.State switch
            {
                TrenchExerciseState.Completed => new Color(0.10f, 0.88f, 0.30f),
                TrenchExerciseState.Failed => new Color(0.90f, 0.20f, 0.20f),
                TrenchExerciseState.InProgress => new Color(0.20f, 0.65f, 0.95f),
                _ => new Color(0.55f, 0.58f, 0.62f)
            };
            DrawRect(new Rect(px, py, pw, 3f), stateColor);

            float iy = py + 8f;
            float ix = px + 12f;

            // Header Titulo
            string stateTxt = evaluator.State switch
            {
                TrenchExerciseState.Completed => "✔ EXCAVACIÓN COMPLETADA",
                TrenchExerciseState.Failed => "❌ EJERCICIO FALLIDO (TIEMPO)",
                TrenchExerciseState.InProgress => "EJERCICIO DE ZANJA EN PROGRESO",
                _ => "EJERCICIO: EXCAVACIÓN DE ZANJA"
            };

            _headerStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(ix, iy, pw - 24f, 18f), stateTxt, _headerStyle);
            iy += 22f;

            DrawRect(new Rect(ix, iy, pw - 24f, 1f), new Color(0.20f, 0.24f, 0.30f));
            iy += 6f;

            // Metrica 1: Largo (Objetivo vs Medido)
            DrawMetricRow(ix, iy, pw - 24f, "Largo Objetivo:", $"{config.targetLength:F1}m", $"Medido: {tracker.MeasuredLength:F1}m", evaluator.IsLengthValid);
            iy += 24f;

            // Metrica 2: Ancho (Objetivo vs Medido)
            DrawMetricRow(ix, iy, pw - 24f, "Ancho Objetivo:", $"{config.targetWidth:F1}m", $"Medido: {tracker.MeasuredWidth:F1}m", evaluator.IsWidthValid);
            iy += 24f;

            // Metrica 3: Profundidad (Objetivo vs Promedio)
            DrawMetricRow(ix, iy, pw - 24f, "Profundidad:", $"{config.targetDepth:F2}m", $"Prom: {tracker.AverageDepth:F2}m (Max: {tracker.MaxDepth:F2}m)", evaluator.IsDepthValid);
            iy += 24f;

            // Metrica 4: Tiempo
            int mins = Mathf.FloorToInt(evaluator.ElapsedTime / 60f);
            int secs = Mathf.FloorToInt(evaluator.ElapsedTime % 60f);
            string timeTxt = config.maxTimeSeconds > 0f
                ? $"{mins:D2}:{secs:D2} / {Mathf.FloorToInt(config.maxTimeSeconds / 60f):D2}:00"
                : $"{mins:D2}:{secs:D2}";
            DrawMetricRow(ix, iy, pw - 24f, "Tiempo Sesión:", timeTxt, $"Avance: {evaluator.CompletionPercentage:F1}%", true);
            iy += 26f;

            // Banderas de Alarma / Advertencia
            if (evaluator.HasOutOfBoundsFault)
            {
                DrawRect(new Rect(ix, iy, pw - 24f, 18f), new Color(0.85f, 0.15f, 0.10f, 0.85f));
                _badgeStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(ix + 6f, iy + 1f, pw - 36f, 16f), "⚠️ ALERTA: EXCAVACIÓN FUERA DE LÍMITES", _badgeStyle);
                iy += 20f;
            }
            else if (evaluator.HasOverExcavationFault)
            {
                DrawRect(new Rect(ix, iy, pw - 24f, 18f), new Color(0.95f, 0.55f, 0.10f, 0.85f));
                _badgeStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(ix + 6f, iy + 1f, pw - 36f, 16f), "⚠️ ADVERTENCIA: EXCESO DE PROFUNDIDAD", _badgeStyle);
                iy += 20f;
            }

            // Atajo de Reinicio
            _hintStyle.normal.textColor = new Color(0.65f, 0.68f, 0.72f);
            GUI.Label(new Rect(ix, py + ph - 20f, pw - 24f, 16f), "Presione Tecla K para reiniciar la excavación", _hintStyle);
        }

        private void DrawMetricRow(float x, float y, float w, string title, string targetVal, string measuredVal, bool isValid)
        {
            _labelBoldStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(x, y, 110f, 18f), title, _labelBoldStyle);

            GUIStyle valStyle = new GUIStyle(_metricStyle);
            valStyle.normal.textColor = isValid ? new Color(0.10f, 0.85f, 0.35f) : new Color(0.95f, 0.70f, 0.20f);

            GUI.Label(new Rect(x + 110f, y, 80f, 18f), targetVal, _labelBoldStyle);
            GUI.Label(new Rect(x + 180f, y, w - 180f, 18f), measuredVal, valStyle);
        }

        private void DrawRect(Rect r, Color c)
        {
            Color prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, _white);
            GUI.color = prev;
        }

        private void EnsureGUI()
        {
            if (_guiReady) return;

            _white = new Texture2D(1, 1);
            _white.SetPixel(0, 0, Color.white);
            _white.Apply();

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };

            _labelBoldStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };

            _metricStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleLeft
            };

            _badgeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 9,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };

            _hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 9,
                alignment = TextAnchor.MiddleLeft
            };

            _guiReady = true;
        }
    }
}
