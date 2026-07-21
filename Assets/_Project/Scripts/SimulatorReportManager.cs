using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using Excavator.Engine;
using Excavator.Reporting;

/// <summary>
/// Gestor independiente de informes de evaluacion para el simulador industrial.
/// Administra los datos del operador, renderiza la ventana modal de reporte tecnico
/// y ejecuta la exportacion de documentos PDF sin alterar los componentes existentes.
/// </summary>
public class SimulatorReportManager : MonoBehaviour
{
    public static SimulatorReportManager Instance { get; private set; }

    [Header("Datos del Operador")]
    public string operatorName = "Juan Pérez";
    public string operatorId = "OP-2026-884";

    [Header("Referencias (Auto-detectadas si quedan vacias)")]
    public PenaltyTracker penaltyTracker;
    public ParkingZone parkingZone;
    public EngineController engineController;

    [Header("Teclas de Acceso Directo")]
    [Tooltip("Tecla para abrir/cerrar el Informe de Evaluación en pantalla (Por defecto: Tecla P).")]
    public Key reportKey = Key.P;

    // Estado Interno
    private bool _isWindowOpen = false;
    private string _exportMessage = "";
    private float _exportMessageTimer = 0f;
    private string _lastExportPath = "";
    private bool _hasTriggeredCompletionReport = false;
    private System.Reflection.FieldInfo _isCompletedField;

    // IMGUI
    private Texture2D _white;
    private GUIStyle _winHeaderStyle;
    private GUIStyle _labelBoldStyle;
    private GUIStyle _tableHeaderStyle;
    private GUIStyle _tableRowStyle;
    private GUIStyle _buttonStyle;
    private Vector2 _scrollPos;
    private bool _guiReady;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        AutoFindReferences();
        _isCompletedField = typeof(ParkingZone).GetField("_isCompleted", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    }

    private void AutoFindReferences()
    {
        if (penaltyTracker == null)
            penaltyTracker = GetComponent<PenaltyTracker>()
                          ?? GetComponentInParent<PenaltyTracker>()
                          ?? GetComponentInChildren<PenaltyTracker>()
                          ?? FindAnyObjectByType<PenaltyTracker>();

        if (parkingZone == null)
            parkingZone = FindAnyObjectByType<ParkingZone>();

        if (engineController == null)
            engineController = GetComponent<EngineController>()
                            ?? GetComponentInParent<EngineController>()
                            ?? GetComponentInChildren<EngineController>()
                            ?? FindAnyObjectByType<EngineController>();
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && kb[reportKey].wasPressedThisFrame)
        {
            ToggleReportWindow();
        }

        // Auto-abrir informe al completar el ejercicio de estacionamiento
        if (!_hasTriggeredCompletionReport && parkingZone != null && _isCompletedField != null)
        {
            object val = _isCompletedField.GetValue(parkingZone);
            if (val is bool isCompleted && isCompleted)
            {
                _hasTriggeredCompletionReport = true;
                OpenReportWindow();
            }
        }

        if (_exportMessageTimer > 0f)
            _exportMessageTimer -= Time.deltaTime;
    }

    public void ToggleReportWindow()
    {
        _isWindowOpen = !_isWindowOpen;
        if (_isWindowOpen)
        {
            AutoFindReferences();
        }
    }

    public void OpenReportWindow()
    {
        _isWindowOpen = true;
        AutoFindReferences();
    }

    public void CloseReportWindow()
    {
        _isWindowOpen = false;
    }

    // ------------------------------------------------------------------
    // CONSTRUCCIÓN DE DATOS DE EVALUACIÓN
    // ------------------------------------------------------------------
    public EvaluationReportData BuildReportData()
    {
        AutoFindReferences();

        EvaluationReportData data = new EvaluationReportData();
        data.operatorName = string.IsNullOrEmpty(operatorName) ? "Operador no registrado" : operatorName;
        data.operatorId = string.IsNullOrEmpty(operatorId) ? "OP-2026-001" : operatorId;
        data.evaluationDate = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
        data.scenarioName = "Escenario 1 — Circuito de Maniobras y Estacionamiento";
        data.totalSessionTime = Time.timeSinceLevelLoad;

        int mins = Mathf.FloorToInt(data.totalSessionTime / 60f);
        int secs = Mathf.FloorToInt(data.totalSessionTime % 60f);
        data.sessionTimeFormatted = $"{mins:D2}:{secs:D2}";

        if (penaltyTracker != null)
        {
            data.initialScore = penaltyTracker.initialScore;
            data.finalScore = penaltyTracker.CurrentScore;
            data.totalCollisions = penaltyTracker.TotalCollisions;
            data.totalDeductions = penaltyTracker.TotalDeductions;
            data.penalties = new List<PenaltyEventData>(penaltyTracker.Penalties);

            float maxV = 0f;
            foreach (var p in data.penalties)
                if (p.impactSpeed > maxV) maxV = p.impactSpeed;
            data.maxImpactSpeed = maxV;
        }

        // Estado del Dictamen
        if (data.finalScore >= 75)
        {
            data.statusText = "APROBADO — APTO PARA OPERACIÓN REAL";
            data.statusColor = new Color(0.1f, 0.85f, 0.3f);
        }
        else if (data.finalScore >= 50)
        {
            data.statusText = "APROBADO CON OBSERVACIONES — REQUIERE REFUERZO";
            data.statusColor = new Color(1.0f, 0.65f, 0.0f);
        }
        else
        {
            data.statusText = "REPROBADO — REQUIERE RE-ENTRENAMIENTO OBLIGATORIO";
            data.statusColor = new Color(0.9f, 0.2f, 0.2f);
        }

        // Checklist de apago si existe ParkingZone
        if (engineController != null)
        {
            data.condEngineOff = engineController.State == EngineStateId.Off;
        }

        return data;
    }

    // ------------------------------------------------------------------
    // EXPORTACIÓN PDF CON APERTURA AUTOMÁTICA
    // ------------------------------------------------------------------
    public void ExportPdfReport()
    {
        EvaluationReportData data = BuildReportData();
        string pdfPath = PdfReportGenerator.GeneratePdfReport(data);

        if (!string.IsNullOrEmpty(pdfPath))
        {
            _lastExportPath = pdfPath;
            _exportMessage = $"✔ REPORTE PDF GENERADO: {Path.GetFileName(pdfPath)}";
            _exportMessageTimer = 8.0f;

            // Abrir la carpeta contenedora o el archivo en Windows
            try
            {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{pdfPath.Replace('/', '\\')}\"");
#endif
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SimulatorReportManager] No se pudo abrir la carpeta en explorer: {ex.Message}");
            }
        }
        else
        {
            _exportMessage = "❌ Error al generar el archivo PDF.";
            _exportMessageTimer = 5.0f;
        }
    }

    // ------------------------------------------------------------------
    // INTERFAZ DE USUARIO IMGUI (VENTANA MODAL REPORTE INDUSTRIAL)
    // ------------------------------------------------------------------
    void OnGUI()
    {
        if (!_isWindowOpen) return;
        EnsureGUI();

        float windowW = Mathf.Min(780f, Screen.width - 40f);
        float windowH = Mathf.Min(580f, Screen.height - 40f);
        float windowX = (Screen.width - windowW) * 0.5f;
        float windowY = (Screen.height - windowH) * 0.5f;

        // Fondo Overlay Oscuro
        DrawRect(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.65f));

        // Fondo Ventana Principal (#0E1726)
        DrawRect(new Rect(windowX, windowY, windowW, windowH), new Color(0.06f, 0.09f, 0.15f, 0.98f));
        // Encabezado Superior Azul Industrial
        DrawRect(new Rect(windowX, windowY, windowW, 45f), new Color(0.08f, 0.18f, 0.32f, 1f));
        DrawRect(new Rect(windowX, windowY + 45f, windowW, 3f), new Color(1.0f, 0.55f, 0.0f, 1f));

        // Borde Exterior Ventana
        DrawRectBorder(new Rect(windowX, windowY, windowW, windowH), new Color(0.25f, 0.35f, 0.50f), 2f);

        // --- Titulo Encabezado ---
        _winHeaderStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(windowX + 16f, windowY + 10f, windowW - 100f, 26f),
                  "INFORME TÉCNICO DE EVALUACIÓN DE OPERADOR — EXCAVADORA HIDRÁULICA", _winHeaderStyle);

        // Boton Cerrar [X]
        if (GUI.Button(new Rect(windowX + windowW - 38f, windowY + 8f, 30f, 28f), "✖", _buttonStyle))
        {
            CloseReportWindow();
        }

        float contentX = windowX + 16f;
        float contentY = windowY + 56f;
        float contentW = windowW - 32f;

        EvaluationReportData data = BuildReportData();

        // --- FICHA DEL OPERADOR ---
        GUI.Label(new Rect(contentX, contentY, 130f, 22f), "Nombre Conductor:", _labelBoldStyle);
        operatorName = GUI.TextField(new Rect(contentX + 130f, contentY, 200f, 22f), operatorName);

        GUI.Label(new Rect(contentX + 350f, contentY, 90f, 22f), "Legajo / DNI:", _labelBoldStyle);
        operatorId = GUI.TextField(new Rect(contentX + 440f, contentY, 150f, 22f), operatorId);

        contentY += 30f;

        // --- CARD DE PUNTAJE Y DICTAMEN ---
        float cardH = 55f;
        DrawRect(new Rect(contentX, contentY, contentW, cardH), new Color(0.12f, 0.15f, 0.22f, 0.9f));
        DrawRect(new Rect(contentX, contentY, 6f, cardH), data.statusColor);

        GUIStyle scoreBig = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };
        scoreBig.normal.textColor = data.statusColor;
        GUI.Label(new Rect(contentX + 16f, contentY + 6f, 180f, 28f), $"{data.finalScore} / 100 PTS", scoreBig);

        GUIStyle dictamenStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };
        dictamenStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(contentX + 200f, contentY + 8f, contentW - 210f, 20f), $"DICTAMEN: {data.statusText}", dictamenStyle);

        GUIStyle detailStyle = new GUIStyle(GUI.skin.label) { fontSize = 10 };
        detailStyle.normal.textColor = new Color(0.75f, 0.78f, 0.82f);
        GUI.Label(new Rect(contentX + 200f, contentY + 30f, contentW - 210f, 18f),
                  $"Tiempo total: {data.sessionTimeFormatted}  |  Colisiones: {data.totalCollisions}  |  Descuento: -{data.totalDeductions} pts", detailStyle);

        contentY += cardH + 12f;

        // --- TABLA DE INFRACCIONES (SCROLLVIEW) ---
        GUI.Label(new Rect(contentX, contentY, contentW, 20f), "REGISTRO DE COLISIONES E INFRACCIONES REGISTRADAS EN ENTORNO:", _labelBoldStyle);
        contentY += 22f;

        float tableH = windowH - (contentY - windowY) - 75f;
        Rect scrollArea = new Rect(contentX, contentY, contentW, tableH);
        Rect viewRect = new Rect(0, 0, contentW - 20f, Mathf.Max(tableH, (data.penalties.Count + 1) * 22f + 10f));

        DrawRect(scrollArea, new Color(0.04f, 0.06f, 0.10f, 0.9f));

        _scrollPos = GUI.BeginScrollView(scrollArea, _scrollPos, viewRect);

        // Header Tabla IMGUI
        float ty = 0f;
        DrawRect(new Rect(0, ty, viewRect.width, 22f), new Color(0.15f, 0.20f, 0.30f));

        _tableHeaderStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(5, ty + 2, 25, 20), "#", _tableHeaderStyle);
        GUI.Label(new Rect(35, ty + 2, 60, 20), "Tiempo", _tableHeaderStyle);
        GUI.Label(new Rect(100, ty + 2, 180, 20), "Elemento Impactado", _tableHeaderStyle);
        GUI.Label(new Rect(285, ty + 2, 230, 20), "Categoría de Falta", _tableHeaderStyle);
        GUI.Label(new Rect(520, ty + 2, 80, 20), "Velocidad", _tableHeaderStyle);
        GUI.Label(new Rect(605, ty + 2, 90, 20), "Penalización", _tableHeaderStyle);

        ty += 24f;

        if (data.penalties.Count == 0)
        {
            GUIStyle cleanStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Italic };
            cleanStyle.normal.textColor = new Color(0.2f, 0.85f, 0.4f);
            GUI.Label(new Rect(10, ty + 10, viewRect.width - 20, 20), "✔ Sin infracciones ni colisiones registradas. Operación limpia.", cleanStyle);
        }
        else
        {
            for (int i = 0; i < data.penalties.Count; i++)
            {
                var p = data.penalties[i];
                Color bgCol = (i % 2 == 0) ? new Color(0.08f, 0.11f, 0.16f) : new Color(0.06f, 0.08f, 0.13f);
                DrawRect(new Rect(0, ty, viewRect.width, 20f), bgCol);

                _tableRowStyle.normal.textColor = new Color(0.88f, 0.90f, 0.92f);
                GUI.Label(new Rect(5, ty + 2, 25, 18), (i + 1).ToString(), _tableRowStyle);
                GUI.Label(new Rect(35, ty + 2, 60, 18), p.timeFormatted, _tableRowStyle);
                GUI.Label(new Rect(100, ty + 2, 180, 18), p.objectName, _tableRowStyle);
                GUI.Label(new Rect(285, ty + 2, 230, 18), p.penaltyCategory, _tableRowStyle);
                GUI.Label(new Rect(520, ty + 2, 80, 18), $"{p.impactSpeed:F2} m/s", _tableRowStyle);

                GUIStyle penStyle = new GUIStyle(_tableRowStyle);
                penStyle.fontStyle = FontStyle.Bold;
                penStyle.normal.textColor = p.scoreDeduction >= 15 ? new Color(1.0f, 0.3f, 0.3f) : new Color(1.0f, 0.65f, 0.1f);
                GUI.Label(new Rect(605, ty + 2, 90, 18), $"-{p.scoreDeduction} pts", penStyle);

                ty += 22f;
            }
        }

        GUI.EndScrollView();

        // --- BOTONES INFERIORES Y ACCIONES ---
        float footerY = windowY + windowH - 44f;

        // Boton Exportar PDF
        Color prevColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.0f, 0.55f, 0.85f);

        if (GUI.Button(new Rect(contentX, footerY, 260f, 32f), "📁 EXPORTAR REPORTE EN PDF", _buttonStyle))
        {
            ExportPdfReport();
        }
        GUI.backgroundColor = prevColor;

        // Mensaje de notificacion de exportacion
        if (_exportMessageTimer > 0f)
        {
            GUIStyle msgStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold };
            msgStyle.normal.textColor = new Color(0.2f, 0.9f, 0.4f);
            GUI.Label(new Rect(contentX + 275f, footerY + 6f, contentW - 380f, 24f), _exportMessage, msgStyle);
        }

        // Boton Cerrar
        if (GUI.Button(new Rect(windowX + windowW - 100f, footerY, 84f, 32f), "CERRAR", _buttonStyle))
        {
            CloseReportWindow();
        }
    }

    private void DrawRect(Rect r, Color c)
    {
        Color prev = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, _white);
        GUI.color = prev;
    }

    private void DrawRectBorder(Rect r, Color c, float width)
    {
        DrawRect(new Rect(r.x, r.y, r.width, width), c);
        DrawRect(new Rect(r.x, r.y + r.height - width, r.width, width), c);
        DrawRect(new Rect(r.x, r.y, width, r.height), c);
        DrawRect(new Rect(r.x + r.width - width, r.y, width, r.height), c);
    }

    private void EnsureGUI()
    {
        if (_guiReady) return;

        _white = new Texture2D(1, 1);
        _white.SetPixel(0, 0, Color.white);
        _white.Apply();

        _winHeaderStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };

        _labelBoldStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };
        _labelBoldStyle.normal.textColor = Color.white;

        _tableHeaderStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 10,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };

        _tableRowStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 10,
            alignment = TextAnchor.MiddleLeft
        };

        _buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 11,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        _buttonStyle.normal.textColor = Color.white;

        _guiReady = true;
    }
}
