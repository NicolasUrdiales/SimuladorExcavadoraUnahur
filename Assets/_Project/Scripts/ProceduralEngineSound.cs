using UnityEngine;
using Excavator.Engine;

/// <summary>
/// Sonido de motor PROCEDURAL — no requiere ningun clip de audio.
/// Genera una onda sintetica con armonicos que simula el sonido de un motor diesel
/// de excavadora. El tono y volumen varian segun las RPM actuales.
///
/// Solo necesita un AudioSource en el mismo GameObject (se crea automaticamente).
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class ProceduralEngineSound : MonoBehaviour
{
    [Header("Referencia al Motor")]
    [Tooltip("Se auto-detecta si queda vacio.")]
    [SerializeField] EngineController engineController;

    [Header("Parametros de Audio")]
    [Range(0f, 1f)]
    [Tooltip("Volumen maximo del motor.")]
    public float masterVolume = 0.45f;

    [Range(0.5f, 4f)]
    [Tooltip("Multiplicador de frecuencia fundamental. Valores mayores = tono mas agudo.")]
    public float pitchMultiplier = 1.5f;

    // --- Audio thread state (volatile para sincronizacion entre hilos) ---
    volatile float _smoothRpmThread;  // leido en audio thread
    volatile bool  _activeThread;     // leido en audio thread
    volatile float _volumeThread;     // leido en audio thread

    // --- Unity thread state ---
    float _targetRpm;
    float _smoothRpm;
    bool  _isActive;

    AudioSource _src;
    int         _sampleRate;
    float       _phase;

    // -------------------------------------------------------
    //  Ciclo de vida
    // -------------------------------------------------------
    void Awake()
    {
        if (engineController == null)
            engineController = GetComponent<EngineController>()
                            ?? GetComponentInParent<EngineController>()
                            ?? GetComponentInChildren<EngineController>();

        _sampleRate = AudioSettings.outputSampleRate;
        _src        = GetComponent<AudioSource>();

        // Clip de silencio para que AudioSource.Play() funcione
        // OnAudioFilterRead se llama mientras el source este reproduciendo,
        // independientemente de si el clip tiene contenido.
        float[] silence  = new float[_sampleRate];
        AudioClip loopClip = AudioClip.Create("EngineLoop", _sampleRate, 1, _sampleRate, false);
        loopClip.SetData(silence, 0);

        _src.clip         = loopClip;
        _src.loop         = true;
        _src.spatialBlend = 0f;   // 2D — suena igual en todo punto de la escena
        _src.volume       = 1f;   // el volumen real lo controla OnAudioFilterRead
        _src.playOnAwake  = false;
        _src.dopplerLevel = 0f;
    }

    void OnEnable()
    {
        if (engineController == null) return;
        engineController.RPMChanged   += HandleRpmChanged;
        engineController.StateChanged += HandleStateChanged;
    }

    void OnDisable()
    {
        if (engineController == null) return;
        engineController.RPMChanged   -= HandleRpmChanged;
        engineController.StateChanged -= HandleStateChanged;
        SetActive(false);
    }

    void HandleRpmChanged(float rpm)
    {
        _targetRpm = rpm;
    }

    void HandleStateChanged(EngineStateId state)
    {
        bool shouldRun = state is EngineStateId.Starting or EngineStateId.Running;
        SetActive(shouldRun);
    }

    void SetActive(bool active)
    {
        _isActive = active;
        if (active && !_src.isPlaying)
            _src.Play();
        else if (!active && _src.isPlaying)
            _src.Stop();
    }

    // -------------------------------------------------------
    //  Update: suavizar RPM y publicar al hilo de audio
    // -------------------------------------------------------
    void Update()
    {
        // Suavizar RPM para evitar clicks en el audio (frecuencia estable)
        float lerpSpeed = _isActive ? 3f : 8f;   // apagado = cae mas rapido
        _smoothRpm = Mathf.Lerp(_smoothRpm, _isActive ? _targetRpm : 0f, lerpSpeed * Time.deltaTime);

        // Volumen: fade in al arrancar, fade out al apagar
        float targetVol = _isActive ? masterVolume : 0f;
        float fadespeed = _isActive ? 2f : 4f;
        _volumeThread = Mathf.Lerp(_volumeThread, targetVol, fadespeed * Time.deltaTime);

        // Publicar al hilo de audio (volatile write)
        _smoothRpmThread = _smoothRpm;
        _activeThread    = _isActive || _volumeThread > 0.005f; // cola de fade-out
    }

    // -------------------------------------------------------
    //  Sintesis procedural (hilo de audio)
    //
    //  Motor diesel de 4 tiempos:
    //    - Frecuencia fundamental = RPM/60 * (cilindros/2)
    //    - Series de armonicos que dan el caracter diesel
    //    - Sub-armonico para el "golpeteo" grave
    // -------------------------------------------------------
    void OnAudioFilterRead(float[] data, int channels)
    {
        if (!_activeThread)
        {
            System.Array.Clear(data, 0, data.Length);
            return;
        }

        float rpm     = Mathf.Max(_smoothRpmThread, 80f);
        float vol     = _volumeThread;
        float freq    = rpm / 60f * pitchMultiplier; // Hz fundamental
        float step    = freq / _sampleRate;

        for (int i = 0; i < data.Length; i += channels)
        {
            float tau = _phase * Mathf.PI * 2f;

            // Serie armonica: fundamental + armonicos + sub-armonico (diesel thud)
            float s  = Mathf.Sin(tau)        * 0.38f;  // fundamental
            s += Mathf.Sin(tau * 2f)         * 0.25f;  // 2° armonico
            s += Mathf.Sin(tau * 3f)         * 0.14f;  // 3° armonico
            s += Mathf.Sin(tau * 4f)         * 0.08f;  // 4° armonico
            s += Mathf.Sin(tau * 0.5f)       * 0.22f;  // sub-armonico (thud)

            // Leve saturacion para dar "grit" de diesel
            s = Mathf.Clamp(s * 1.15f, -1f, 1f);
            s *= vol;

            for (int c = 0; c < channels; c++)
                data[i + c] = s;

            _phase += step;
            if (_phase >= 1f) _phase -= 1f;
        }
    }
}
