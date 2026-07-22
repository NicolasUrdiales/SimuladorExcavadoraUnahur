using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Excavator.Reporting;

/// <summary>
/// Proxy de colision para redirigir eventos desde colisionadores hijos hacia PenaltyTracker.
/// </summary>
public class PenaltyCollisionProxy : MonoBehaviour
{
    public PenaltyTracker tracker;

    void OnCollisionEnter(Collision collision)
    {
        if (tracker != null) tracker.ProcessCollision(collision);
    }

    void OnTriggerEnter(Collider other)
    {
        if (tracker != null) tracker.ProcessTrigger(other);
    }
}

/// <summary>
/// Componente independiente para rastreo de colisiones y penalizaciones operativas.
/// No modifica la logica de movimiento ni de motor existente.
/// Registra impactos contra barreras, canalizadores y estructuras, categoriza la gravedad
/// de las faltas y mantiene el puntaje de la sesion en tiempo real.
/// </summary>
public class PenaltyTracker : MonoBehaviour
{
    [Header("Configuracion de Penalizaciones")]
    [Tooltip("Puntaje inicial del operador al iniciar el simulador.")]
    public int initialScore = 100;

    [Tooltip("Umbral de velocidad minima (m/s) para registrar una colision.")]
    public float minVelocityThreshold = 0.01f;

    [Tooltip("Umbral de velocidad (m/s) para clasificar una colision como IMPACTO SEVERO.")]
    public float severeVelocityThreshold = 1.20f;

    [Tooltip("Tiempo minimo (segundos) entre colisiones registradas con el mismo objeto.")]
    public float collisionCooldownSeconds = 1.0f;

    [Header("Descuentos de Puntos por Categoria")]
    public int pointsDeductionBarrier = 15;      // Barreras rigidas de hormigon / plastico
    public int pointsDeductionChannelizer = 5;   // Conos y canalizadores viales
    public int pointsDeductionGroundSlam = 10;   // Golpe brusco de pala / chasis contra suelo
    public int pointsDeductionStructure = 10;    // Otras estructuras u objetos
    public int pointsDeductionSevereBonus = 10;  // Extra por alta velocidad (> 1.2 m/s)

    [Header("Visualizacion HUD en vivo")]
    public bool showLiveHud = true;
    public float hudX = 16f;        // Desde la derecha
    public float hudY = 16f;        // Desde arriba
    public float hudWidth = 240f;
    public float hudHeight = 65f;

    // Eventos
    public event Action<PenaltyEventData> PenaltyAdded;
    public event Action<int> ScoreChanged;

    // Estado Interno
    private int _currentScore;
    private float _sessionStartTime;
    private List<PenaltyEventData> _penalties = new List<PenaltyEventData>();
    private Dictionary<int, float> _lastObjectCollisionTime = new Dictionary<int, float>();

    // Notification toast
    private string _lastNotificationText = "";
    private float _lastNotificationTimer = 0f;

    // IMGUI
    private Texture2D _white;
    private GUIStyle _hudBoxStyle;
    private GUIStyle _hudTitleStyle;
    private GUIStyle _hudScoreStyle;
    private GUIStyle _toastStyle;
    private bool _guiReady;

    public int CurrentScore => _currentScore;
    public int TotalCollisions => _penalties.Count;
    public int TotalDeductions => initialScore - _currentScore;
    public List<PenaltyEventData> Penalties => _penalties;

    void Awake()
    {
        _currentScore = initialScore;
        _sessionStartTime = Time.time;
    }

    void Start()
    {
        // Auto-instalar Proxies en todos los colisionadores de la excavadora
        Transform root = transform.root;
        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        foreach (var col in colliders)
        {
            if (col.gameObject != gameObject && col.GetComponent<PenaltyCollisionProxy>() == null)
            {
                var proxy = col.gameObject.AddComponent<PenaltyCollisionProxy>();
                proxy.tracker = this;
            }
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        ProcessCollision(collision);
    }

    void OnTriggerEnter(Collider other)
    {
        ProcessTrigger(other);
    }

    public void ProcessCollision(Collision collision)
    {
        if (collision == null || collision.gameObject == null) return;
        float speed = collision.relativeVelocity.magnitude;
        if (speed < 0.05f) speed = 0.15f; // Asegurar velocidad efectiva minima si hay contacto fisico
        ProcessHitGameObject(collision.gameObject, speed);
    }

    public void ProcessTrigger(Collider other)
    {
        if (other == null || other.gameObject == null) return;
        ProcessHitGameObject(other.gameObject, 0.30f);
    }

    /// <summary>
    /// Evaluacion principal de contacto con objetos del entorno.
    /// </summary>
    public void ProcessHitGameObject(GameObject hitGo, float impactSpeed)
    {
        if (hitGo == null) return;

        // Ignorar la propia excavadora, zonas de estacionamiento y objetos neutros
        if (hitGo.GetComponent<ParkingZone>() != null || hitGo.GetComponentInParent<ParkingZone>() != null)
            return;

        string nameLower = hitGo.name.ToLower();
        if (nameLower.Contains("excavator") || nameLower.Contains("shadow") ||
            nameLower.Contains("camera") || nameLower.Contains("parkzone") ||
            nameLower.Contains("parkingzone") || nameLower.Contains("completion") ||
            nameLower.Contains("canvas") || nameLower.Contains("volume") || nameLower.Contains("light"))
            return;

        if (nameLower.Contains("plane") || nameLower.Contains("terrain") || nameLower.Contains("ground"))
        {
            // El suelo solo penaliza si el golpe es brusco (> 1.2 m/s)
            if (impactSpeed < 1.2f) return;
        }

        int instanceId = hitGo.GetHashCode();
        float now = Time.time;

        // Verificar cooldown por objeto
        if (_lastObjectCollisionTime.TryGetValue(instanceId, out float lastTime))
        {
            if (now - lastTime < collisionCooldownSeconds) return;
        }
        _lastObjectCollisionTime[instanceId] = now;

        // Categorizar la colision segun el nombre o tipo de objeto
        string objName = hitGo.name;
        string category = "Objeto / Estructura del Entorno";
        string severity = "Media";
        int baseDeduction = pointsDeductionStructure;

        if (nameLower.Contains("barrier") || nameLower.Contains("block") || nameLower.Contains("vintprog") || nameLower.Contains("pedestrian") || nameLower.Contains("plastic"))
        {
            category = "Barrera de Seguridad / Valla de Obra";
            baseDeduction = pointsDeductionBarrier;
            severity = "Grave";
        }
        else if (nameLower.Contains("channelizing") || nameLower.Contains("cone") || nameLower.Contains("canalizador") || nameLower.Contains("traffic"))
        {
            category = "Canalizador / Señalización Vial";
            baseDeduction = pointsDeductionChannelizer;
            severity = "Leve";
        }
        else if (nameLower.Contains("building") || nameLower.Contains("tower") || nameLower.Contains("structure") || nameLower.Contains("scaffolding") || nameLower.Contains("pipe"))
        {
            category = "Estructura de Edificación / Obra";
            baseDeduction = pointsDeductionStructure;
            severity = "Grave";
        }
        else if (nameLower.Contains("plane") || nameLower.Contains("terrain") || nameLower.Contains("ground"))
        {
            category = "Impacto Brusco contra Suelo";
            baseDeduction = pointsDeductionGroundSlam;
            severity = "Media";
        }

        // Bonus por impacto severo
        int finalDeduction = baseDeduction;
        if (impactSpeed >= severeVelocityThreshold)
        {
            finalDeduction += pointsDeductionSevereBonus;
            severity = "Grave";
            category += " (Impacto Severo)";
        }

        // Aplicar penalización
        _currentScore = Mathf.Max(0, _currentScore - finalDeduction);

        float elapsed = now - _sessionStartTime;
        int mins = Mathf.FloorToInt(elapsed / 60f);
        int secs = Mathf.FloorToInt(elapsed % 60f);

        PenaltyEventData pEvent = new PenaltyEventData
        {
            timestampSeconds = elapsed,
            timeFormatted = $"{mins:D2}:{secs:D2}",
            objectName = CleanObjectName(objName),
            penaltyCategory = category,
            impactSpeed = impactSpeed,
            scoreDeduction = finalDeduction,
            severityLevel = severity
        };

        _penalties.Add(pEvent);

        // Notificacion en pantalla
        _lastNotificationText = $"⚠️ COLISIÓN: {pEvent.objectName} (-{finalDeduction} pts)";
        _lastNotificationTimer = 3.5f;

        Debug.LogWarning($"[PenaltyTracker] Infracción: {pEvent.penaltyCategory} | Objeto: {pEvent.objectName} | Vel: {impactSpeed:F2}m/s | -{finalDeduction}pts | Puntaje Restante: {_currentScore}");

        PenaltyAdded?.Invoke(pEvent);
        ScoreChanged?.Invoke(_currentScore);
    }

    private string CleanObjectName(string rawName)
    {
        int idx = rawName.IndexOf('(');
        if (idx > 0) rawName = rawName.Substring(0, idx).Trim();
        return rawName.Replace('_', ' ');
    }

    void Update()
    {
        if (_lastNotificationTimer > 0f)
            _lastNotificationTimer -= Time.deltaTime;
    }

    // -------------------------------------------------------
    // IMGUI HUD EN VIVO
    // -------------------------------------------------------
    void OnGUI()
    {
        if (!showLiveHud) return;
        EnsureGUI();

        float screenW = Screen.width;
        float px = screenW - hudX - hudWidth;
        float py = hudY;

        // Fondo del panel superior derecho
        DrawRect(new Rect(px, py, hudWidth, hudHeight), new Color(0.06f, 0.08f, 0.12f, 0.88f));

        // Borde lateral segun estado del puntaje
        Color borderColor = _currentScore >= 75
            ? new Color(0.10f, 0.85f, 0.30f)
            : (_currentScore >= 50 ? new Color(1.0f, 0.65f, 0.0f) : new Color(0.9f, 0.2f, 0.2f));

        DrawRect(new Rect(px, py, 4f, hudHeight), borderColor);

        float ix = px + 12f;
        float iy = py + 8f;

        _hudTitleStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(ix, iy, hudWidth - 20f, 18f), "EVALUACIÓN DE CONDUCTOR", _hudTitleStyle);
        iy += 20f;

        _hudScoreStyle.normal.textColor = borderColor;
        GUI.Label(new Rect(ix, iy, hudWidth - 20f, 22f), $"PUNTAJE: {_currentScore} / {initialScore} pts", _hudScoreStyle);
        iy += 18f;

        GUIStyle subStyle = new GUIStyle(GUI.skin.label) { fontSize = 10 };
        subStyle.normal.textColor = new Color(0.75f, 0.75f, 0.78f);
        GUI.Label(new Rect(ix, iy, hudWidth - 20f, 16f), $"Colisiones: {_penalties.Count}  |  Tecla P: Reporte", subStyle);

        // Toast de notificacion de impacto
        if (_lastNotificationTimer > 0f)
        {
            float alpha = Mathf.Clamp01(_lastNotificationTimer);
            _toastStyle.normal.textColor = new Color(1.0f, 0.3f, 0.2f, alpha);

            float toastW = 340f;
            float toastH = 28f;
            float toastX = (screenW - toastW) * 0.5f;
            float toastY = 85f;

            DrawRect(new Rect(toastX, toastY, toastW, toastH), new Color(0.12f, 0.02f, 0.02f, 0.90f * alpha));
            DrawRect(new Rect(toastX, toastY, toastW, 2f), new Color(1.0f, 0.2f, 0.2f, alpha));
            GUI.Label(new Rect(toastX, toastY + 4f, toastW, toastH - 4f), _lastNotificationText, _toastStyle);
        }
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

        _hudTitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };

        _hudScoreStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };

        _toastStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };

        _guiReady = true;
    }
}
