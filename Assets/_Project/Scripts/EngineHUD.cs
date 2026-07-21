using UnityEngine;
using Excavator.Engine;

/// <summary>
/// HUD del motor de excavadora — completamente auto-contenido.
/// No requiere Canvas, prefabs ni ninguna configuracion de escena.
/// Solo agregar este componente al mismo GameObject que EngineController.
///
/// Muestra:
///   - Indicador de estado con color (Apagado / Contacto / Arrancando / Marcha)
///   - Barra de RPM
///   - Instrucciones de teclado
///   - Boton de encendido/apagado clickeable en pantalla
/// </summary>
public class EngineHUD : MonoBehaviour
{
    [Header("Referencia al Motor")]
    [Tooltip("Se auto-detecta si queda vacio.")]
    [SerializeField] EngineController engineController;

    [Header("Posicion del Panel")]
    [SerializeField] float panelX      = 16f;
    [SerializeField] float panelY      = 16f;   // desde abajo
    [SerializeField] float panelWidth  = 300f;
    [SerializeField] float panelHeight = 158f;

    // Estado cacheado (actualizado via eventos)
    float         _currentRpm;
    EngineStateId _state = EngineStateId.Off;

    // IMGUI resources
    Texture2D _white;
    GUIStyle  _boxStyle;
    GUIStyle  _titleStyle;
    GUIStyle  _hintStyle;
    GUIStyle  _btnStyle;
    bool      _guiReady;

    // -------------------------------------------------------
    //  Paleta de colores por estado
    // -------------------------------------------------------
    static Color PanelColor(EngineStateId s) => s switch
    {
        EngineStateId.Off       => new Color(0.08f, 0.08f, 0.10f, 0.93f),
        EngineStateId.ContactOn => new Color(0.06f, 0.14f, 0.28f, 0.93f),
        EngineStateId.Starting  => new Color(0.22f, 0.15f, 0.02f, 0.93f),
        EngineStateId.Running   => new Color(0.04f, 0.18f, 0.07f, 0.93f),
        _                       => new Color(0.08f, 0.08f, 0.10f, 0.93f),
    };

    static Color DotColor(EngineStateId s) => s switch
    {
        EngineStateId.Off       => new Color(0.35f, 0.35f, 0.35f),
        EngineStateId.ContactOn => new Color(0.25f, 0.55f, 1.00f),
        EngineStateId.Starting  => new Color(1.00f, 0.72f, 0.00f),
        EngineStateId.Running   => new Color(0.08f, 0.92f, 0.30f),
        _                       => Color.gray,
    };

    static string StateLabel(EngineStateId s) => s switch
    {
        EngineStateId.Off       => "MOTOR APAGADO",
        EngineStateId.ContactOn => "CONTACTO ACTIVADO",
        EngineStateId.Starting  => "ARRANCANDO...",
        EngineStateId.Running   => "MOTOR EN MARCHA",
        _                       => "---",
    };

    static string BtnLabel(EngineStateId s) => s switch
    {
        EngineStateId.Off       => "▶  ENCENDER  (C luego I)",
        EngineStateId.ContactOn => "▶  ARRANCAR  (I)",
        EngineStateId.Starting  => "   Arrancando...",
        EngineStateId.Running   => "■  APAGAR    (C)",
        _                       => "---",
    };

    // -------------------------------------------------------
    //  Ciclo de vida
    // -------------------------------------------------------
    void Awake()
    {
        if (engineController == null)
            engineController = GetComponent<EngineController>()
                            ?? GetComponentInParent<EngineController>()
                            ?? GetComponentInChildren<EngineController>();
    }

    void OnEnable()
    {
        if (engineController == null) return;
        engineController.RPMChanged   += r => _currentRpm = r;
        engineController.StateChanged += s => _state = s;
    }

    void OnDisable()
    {
        if (engineController == null) return;
        engineController.RPMChanged   -= r => _currentRpm = r;
        engineController.StateChanged -= s => _state = s;
    }

    // -------------------------------------------------------
    //  IMGUI
    // -------------------------------------------------------
    void OnGUI()
    {
        EnsureGUI();

        float screenH = Screen.height;
        float px      = panelX;
        float py      = screenH - panelY - panelHeight;
        float pw      = panelWidth;
        float ph      = panelHeight;

        // ---- Fondo del panel ----
        DrawRect(new Rect(px, py, pw, ph), PanelColor(_state));

        // Borde superior naranja
        DrawRect(new Rect(px, py, pw, 3f), new Color(1f, 0.55f, 0f));

        float ix  = px + 12f;   // inner x
        float iy  = py + 10f;   // inner y cursor
        float iw  = pw - 24f;   // inner width

        // ---- Dot de estado (parpadea al arrancar) ----
        float dotAlpha = _state == EngineStateId.Starting
            ? Mathf.PingPong(Time.time * 4f, 1f)
            : 1f;
        Color dot = DotColor(_state);
        dot.a = dotAlpha;
        DrawRect(new Rect(ix, iy + 4f, 11f, 11f), dot);

        // ---- Texto de estado ----
        _titleStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(ix + 16f, iy, iw - 16f, 22f), StateLabel(_state), _titleStyle);
        iy += 26f;

        // Separador
        DrawRect(new Rect(ix, iy, iw, 1f), new Color(0.3f, 0.3f, 0.3f));
        iy += 6f;

        // ---- Barra de RPM ----
        float maxRpm  = engineController?.Config?.maxRpm ?? 2500f;
        float rpmFrac = Mathf.Clamp01(engineController != null ? _currentRpm / maxRpm : 0f);
        float barW    = iw - 58f;
        float barH    = 13f;

        DrawRect(new Rect(ix, iy + 2f, barW, barH), new Color(0.12f, 0.12f, 0.12f));

        if (rpmFrac > 0.005f)
        {
            // Color verde → amarillo → rojo segun carga
            Color rpmCol = Color.Lerp(
                new Color(0.10f, 0.88f, 0.32f),
                new Color(1.00f, 0.28f, 0.10f),
                rpmFrac);
            DrawRect(new Rect(ix, iy + 2f, barW * rpmFrac, barH), rpmCol);
        }

        _hintStyle.normal.textColor = Color.white;
        _hintStyle.fontSize = 11;
        _hintStyle.fontStyle = FontStyle.Bold;
        GUI.Label(new Rect(ix + barW + 4f, iy, 58f, 20f), $"{_currentRpm:0} rpm", _hintStyle);
        _hintStyle.fontSize = 10;
        _hintStyle.fontStyle = FontStyle.Normal;
        iy += barH + 10f;

        // ---- Instrucciones de teclado ----
        _hintStyle.normal.textColor = new Color(0.72f, 0.72f, 0.72f);
        GUI.Label(new Rect(ix, iy, iw, 18f), "C = Contacto   |   I = Arrancar (mantener)", _hintStyle);
        iy += 22f;

        GUI.Label(new Rect(ix, iy, iw, 18f), "W/S = Mover   A/D = Girar   Q/E/R/F/T/G/Y/H = Brazo", _hintStyle);
        iy += 26f;

        // ---- Boton de encendido/apagado ----
        bool canClick = _state is EngineStateId.Off or EngineStateId.ContactOn or EngineStateId.Running;
        GUI.enabled = canClick;

        Color btnCol = _state == EngineStateId.Running
            ? new Color(0.70f, 0.12f, 0.12f)
            : new Color(0.80f, 0.42f, 0.00f);
        DrawRect(new Rect(ix, iy, iw, 28f), btnCol);

        if (GUI.Button(new Rect(ix, iy, iw, 28f), BtnLabel(_state), _btnStyle))
            HandleButtonClick();

        GUI.enabled = true;
    }

    void HandleButtonClick()
    {
        if (engineController == null) return;
        switch (_state)
        {
            case EngineStateId.Off:
                engineController.ChangeState(new ContactOnState());
                break;
            case EngineStateId.ContactOn:
                engineController.ChangeState(new StartingState());
                break;
            case EngineStateId.Running:
                engineController.ChangeState(new OffState());
                break;
        }
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

        _boxStyle = new GUIStyle(GUI.skin.box);

        _titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 13,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
        };
        _titleStyle.normal.textColor = Color.white;

        _hintStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 10,
            alignment = TextAnchor.MiddleLeft,
        };
        _hintStyle.normal.textColor = new Color(0.75f, 0.75f, 0.75f);

        _btnStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize  = 12,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
        };
        _btnStyle.normal.textColor  = Color.white;
        _btnStyle.normal.background = _white;

        _guiReady = true;
    }
}
