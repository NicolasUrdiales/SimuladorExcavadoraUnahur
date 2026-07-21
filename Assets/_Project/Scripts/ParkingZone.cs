using UnityEngine;
using System.Collections;
using Excavator.Engine;

/// <summary>
/// Zona de estacionamiento con checklist de tres condiciones obligatorias.
/// El ejercicio se completa cuando se cumplen TODAS:
///
///   1. La excavadora esta DENTRO de la zona y completamente detenida.
///   2. La pala (bucket) esta apoyada en el piso.
///   3. El motor esta APAGADO.
///
/// Muestra un HUD de checklist en pantalla con estado en tiempo real.
/// Completar el checklist congela la simulacion y sale del modo Play.
/// </summary>
public class ParkingZone : MonoBehaviour
{
    // -------------------------------------------------------
    //  Inspector
    // -------------------------------------------------------
    [Header("UI de Completado")]
    [Tooltip("Panel que se activa al completar el ejercicio (opcional).")]
    public GameObject completionUIPanel;

    [Header("Deteccion de Parada")]
    [Tooltip("Velocidad maxima (m/s) para considerar la maquina detenida.")]
    public float maxStopSpeed = 0.05f;

    [Header("Posicion de Reposo del Brazo")]
    [Tooltip("El boom debe estar a este angulo o menor (negativo = abajo). " +
             "Ajustar segun el modelo. Por defecto: boom al minimo posible.")]
    public float restBoomThreshold = -20f;

    [Tooltip("Tolerancia angular (+/-) para stick y bucket en posicion neutra.")]
    public float restNeutralTolerance = 25f;

    [Header("Secuencia de Fin")]
    [Tooltip("Segundos que se muestra el mensaje final antes de cerrar.")]
    public float exitDelay = 4f;

    // -------------------------------------------------------
    //  Estado interno
    // -------------------------------------------------------
    ExcavatorMovement _movement;
    ExcavatorArm      _arm;
    EngineController  _engine;
    Rigidbody         _rb;

    bool _insideZone;
    bool _isCompleted;

    // Tres condiciones del checklist
    bool _condParked;    // 1. Dentro + detenida
    bool _condArm;       // 2. Pala en el piso
    bool _condEngine;    // 3. Motor apagado

    // Animacion del panel de completado
    float _completionTime;

    // -------------------------------------------------------
    //  IMGUI
    // -------------------------------------------------------
    Texture2D _white;
    GUIStyle  _titleStyle;
    GUIStyle  _itemStyle;
    GUIStyle  _bigStyle;
    GUIStyle  _hintStyle;
    bool      _guiReady;

    // -------------------------------------------------------
    //  Ciclo de vida
    // -------------------------------------------------------
    void Awake()
    {
        FixGroundPlane();
    }

    void Start()
    {
        if (completionUIPanel != null)
            completionUIPanel.SetActive(false);
    }

    void FixGroundPlane()
    {
        // Corregir el objeto "Plane" (suelo) para que tenga un collider robusto
        GameObject planeGo = GameObject.Find("Plane");
        if (planeGo != null)
        {
            // Quitar Rigidbody del plano si existe (el suelo no debe ser dinamico)
            Rigidbody rb = planeGo.GetComponent<Rigidbody>();
            if (rb != null) Destroy(rb);

            BoxCollider box = planeGo.GetComponent<BoxCollider>();
            if (box != null)
            {
                // El tamaño Y original es casi cero; darle 1.0 de espesor
                // y centrar para que la superficie superior siga en Y = 0
                box.size   = new Vector3(10f, 1f, 10f);
                box.center = new Vector3(0f, -0.5f, 0f);
                box.isTrigger = false;
            }

            MeshCollider meshCol = planeGo.GetComponent<MeshCollider>();
            if (meshCol != null)
                meshCol.enabled = false;
        }
    }

    // -------------------------------------------------------
    //  Trigger de zona
    // -------------------------------------------------------
    void OnTriggerEnter(Collider other)
    {
        if (_isCompleted || _insideZone) return;

        // Solo activar si el colisionador que entra pertenece a la excavadora
        ExcavatorMovement m = other.GetComponentInParent<ExcavatorMovement>();
        if (m == null) return;

        _insideZone = true;
        _movement   = m;

        Transform root = m.transform;

        _arm = root.GetComponent<ExcavatorArm>()
            ?? root.GetComponentInParent<ExcavatorArm>()
            ?? root.GetComponentInChildren<ExcavatorArm>();

        _engine = root.GetComponent<EngineController>()
               ?? root.GetComponentInParent<EngineController>()
               ?? root.GetComponentInChildren<EngineController>();

        _rb = root.GetComponent<Rigidbody>()
           ?? root.GetComponentInParent<Rigidbody>();
    }

    void OnTriggerExit(Collider other)
    {
        if (_isCompleted) return;
        if (other.GetComponentInParent<ExcavatorMovement>() == _movement)
            _insideZone = false;
    }

    // -------------------------------------------------------
    //  Logica principal
    // -------------------------------------------------------
    void Update()
    {
        if (_isCompleted) return;

        EvaluateConditions();

        if (_condParked && _condArm && _condEngine)
            TriggerCompletion();
    }

    void EvaluateConditions()
    {
        // --- Condicion 1: dentro de la zona y completamente detenida ---
        if (_insideZone && _rb != null)
        {
            float speed = Mathf.Max(
                _rb.linearVelocity.magnitude,
                _rb.angularVelocity.magnitude);
            _condParked = speed < maxStopSpeed;
        }
        else
        {
            _condParked = false;
        }

        // --- Condicion 2: pala (bucket) apoyada en el piso ---
        if (_arm != null && _arm.IsInitialized)
        {
            bool shovelOnGround = false;
            Transform bucketTrans = _arm.BucketTransform;

            if (bucketTrans != null)
            {
                // Primero intentar con el collider de la pala para precision maxima
                Collider col = bucketTrans.GetComponent<Collider>()
                            ?? bucketTrans.GetComponentInChildren<Collider>();

                if (col != null)
                {
                    // El borde inferior del collider debe estar cerca del suelo (Y=0)
                    float bottomY = col.bounds.min.y;
                    // Rango: hasta 0.30m sobre el suelo o 0.25m de penetracion
                    shovelOnGround = bottomY <= 0.30f && bottomY >= -0.25f;
                }
                else
                {
                    // Fallback: usar posicion del transform de la pala
                    shovelOnGround = bucketTrans.position.y <= 0.45f;
                }
            }

            _condArm = shovelOnGround;
        }
        else
        {
            // Sin ExcavatorArm detectado: condicion pendiente (no bloquea ni auto-completa)
            _condArm = false;
        }

        // --- Condicion 3: motor completamente apagado ---
        if (_engine != null)
            _condEngine = _engine.State == EngineStateId.Off;
        else
            _condEngine = false;
    }

    // -------------------------------------------------------
    //  Completado del ejercicio
    // -------------------------------------------------------
    void TriggerCompletion()
    {
        _isCompleted    = true;
        _completionTime = Time.time;

        // Congelar la maquina
        if (_movement != null) _movement.enabled = false;
        if (_arm      != null) _arm.enabled      = false;

        if (_rb != null)
        {
            _rb.linearVelocity  = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.isKinematic     = true;
        }

        if (completionUIPanel != null)
            completionUIPanel.SetActive(true);

        StartCoroutine(ExitSequence());
    }

    IEnumerator ExitSequence()
    {
        yield return new WaitForSeconds(exitDelay);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // -------------------------------------------------------
    //  HUD (IMGUI - sin Canvas)
    // -------------------------------------------------------
    void OnGUI()
    {
        EnsureGUI();

        if (_isCompleted)
        {
            DrawCompletionScreen();
            return;
        }

        DrawChecklist();
    }

    // ---- Checklist siempre visible -------------------------
    void DrawChecklist()
    {
        const float PW = 300f;
        const float PH = 160f;
        float px = (Screen.width  - PW) * 0.5f;
        float py = 18f;

        // Fondo semitransparente
        DrawRect(new Rect(px, py, PW, PH), new Color(0.04f, 0.05f, 0.07f, 0.92f));

        // Borde superior: verde si dentro de la zona, gris si fuera
        Color borderCol = _insideZone
            ? new Color(0.10f, 0.85f, 0.30f)
            : new Color(0.30f, 0.30f, 0.30f);
        DrawRect(new Rect(px, py, PW, 3f), borderCol);

        // Titulo
        string titleTxt = _insideZone
            ? "CHECKLIST — ESTACIONAMIENTO"
            : "OBJETIVO: LLEGAR A LA ZONA VERDE";
        GUI.Label(new Rect(px + 10, py + 8, PW - 20, 22), titleTxt, _titleStyle);
        DrawRect(new Rect(px + 10, py + 32, PW - 20, 1), new Color(0.22f, 0.22f, 0.25f));

        float iy = py + 42f;

        // Item 1 — Estacionar y detener
        string label1 = _insideZone
            ? (_condParked ? "Maquina detenida ✓" : "Detener la maquina dentro de la zona...")
            : "Llevar la excavadora a la zona verde";
        DrawCheckItem(px + 10, iy, PW - 20, label1, _condParked && _insideZone);
        iy += 34f;

        // Item 2 — Pala apoyada en el piso
        string armExtra = "";
        if (_insideZone && _arm != null && _arm.IsInitialized && _arm.BucketTransform != null)
        {
            Transform bt  = _arm.BucketTransform;
            Collider  col = bt.GetComponent<Collider>() ?? bt.GetComponentInChildren<Collider>();
            float     alt = col != null ? col.bounds.min.y : bt.position.y;
            armExtra = _condArm
                ? $" (alt: {alt:F2}m ✓)"
                : $" (alt: {alt:F2}m — bajar con F/G/H)";
        }
        DrawCheckItem(px + 10, iy, PW - 20, "Apoyar la pala en el piso" + armExtra, _condArm);
        iy += 34f;

        // Item 3 — Motor apagado
        DrawCheckItem(px + 10, iy, PW - 20, "Apagar el motor  (tecla C)", _condEngine);
        iy += 34f;

        // Resumen
        if (_insideZone)
        {
            int done = (_condParked ? 1 : 0) + (_condArm ? 1 : 0) + (_condEngine ? 1 : 0);
            _hintStyle.normal.textColor = done == 3
                ? new Color(0.1f, 0.9f, 0.3f)
                : new Color(0.55f, 0.55f, 0.58f);
            GUI.Label(new Rect(px + 10, iy, PW - 20, 20),
                done == 3 ? "¡Completando ejercicio!" : $"{done}/3 condiciones cumplidas",
                _hintStyle);
        }
    }

    void DrawCheckItem(float x, float y, float w, string label, bool done)
    {
        Color boxCol = done
            ? new Color(0.08f, 0.85f, 0.28f)
            : new Color(0.25f, 0.25f, 0.28f);
        DrawRect(new Rect(x, y + 5f, 16f, 16f), boxCol);

        if (done)
        {
            _itemStyle.normal.textColor = new Color(0.05f, 0.05f, 0.05f);
            _itemStyle.fontStyle        = FontStyle.Bold;
            _itemStyle.fontSize         = 11;
            GUI.Label(new Rect(x + 1, y + 2, 16, 18), "✔", _itemStyle);
        }

        _itemStyle.normal.textColor = done ? Color.white : new Color(0.60f, 0.60f, 0.62f);
        _itemStyle.fontStyle        = done ? FontStyle.Bold : FontStyle.Normal;
        _itemStyle.fontSize         = 11;
        GUI.Label(new Rect(x + 22f, y + 3f, w - 22f, 22f), label, _itemStyle);
    }

    // ---- Pantalla de completado ----------------------------
    void DrawCompletionScreen()
    {
        float elapsed = Time.time - _completionTime;

        // Overlay oscuro con fade
        float fadeIn = Mathf.Clamp01(elapsed / 0.6f);
        DrawRect(new Rect(0, 0, Screen.width, Screen.height),
                 new Color(0f, 0f, 0f, 0.62f * fadeIn));

        if (fadeIn < 0.4f) return;

        const float PW = 480f;
        const float PH = 200f;
        float px = (Screen.width  - PW) * 0.5f;
        float py = (Screen.height - PH) * 0.5f;

        DrawRect(new Rect(px, py, PW, PH), new Color(0.04f, 0.16f, 0.06f, 0.96f));
        DrawRect(new Rect(px, py,       PW, 4f), new Color(0.1f, 0.95f, 0.30f));
        DrawRect(new Rect(px, py+PH-4f, PW, 4f), new Color(0.1f, 0.95f, 0.30f));

        float titleAlpha = Mathf.Clamp01((elapsed - 0.4f) / 0.5f);
        _bigStyle.normal.textColor = new Color(0.1f, 0.95f, 0.30f, titleAlpha);
        GUI.Label(new Rect(px, py + 24f, PW, 54f), "✔  EJERCICIO COMPLETADO", _bigStyle);

        float subAlpha = Mathf.Clamp01((elapsed - 0.9f) / 0.5f);
        _titleStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f, subAlpha);
        _titleStyle.alignment        = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(px, py + 88f, PW, 28f),
                  "Maquina detenida · Pala en el piso · Motor apagado", _titleStyle);

        float countdown  = Mathf.Max(0f, exitDelay - elapsed);
        float timerAlpha = Mathf.Clamp01((elapsed - 1.2f) / 0.4f);
        _hintStyle.normal.textColor = new Color(0.65f, 0.65f, 0.65f, timerAlpha);
        _hintStyle.alignment        = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(px, py + 148f, PW, 24f),
                  $"Cerrando en {countdown:0.0} s...", _hintStyle);

        _titleStyle.alignment = TextAnchor.MiddleLeft;
        _hintStyle.alignment  = TextAnchor.MiddleLeft;
    }

    // -------------------------------------------------------
    //  Helpers de dibujo
    // -------------------------------------------------------
    void DrawRect(Rect r, Color c)
    {
        Color prev = GUI.color;
        GUI.color  = c;
        GUI.DrawTexture(r, _white);
        GUI.color  = prev;
    }

    void EnsureGUI()
    {
        if (_guiReady) return;

        _white = new Texture2D(1, 1);
        _white.SetPixel(0, 0, Color.white);
        _white.Apply();

        _titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 12,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
        };
        _titleStyle.normal.textColor = Color.white;

        _itemStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 11,
            alignment = TextAnchor.MiddleLeft,
        };
        _itemStyle.normal.textColor = Color.white;

        _bigStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 32,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
        };
        _bigStyle.normal.textColor = new Color(0.1f, 0.95f, 0.3f);

        _hintStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 10,
            alignment = TextAnchor.MiddleLeft,
        };
        _hintStyle.normal.textColor = new Color(0.65f, 0.65f, 0.65f);

        _guiReady = true;
    }
}
