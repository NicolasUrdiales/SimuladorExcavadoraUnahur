using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Control del brazo articulado de la excavadora (Bull Dozer 2).
///
/// Jerarquia del modelo (rotaciones locales del FBX):
///   Bull Dozer 2 (root)  → identity
///   ├── Body.003 (cabina) → -90° X  → local Z = world Up, local X = world Right
///   ├── Main_Forks (boom) → -90° X  → mismas convenciones que Body.003
///   │   └── Secondary Forks (stick) → identity relativo a Main_Forks
///   │       └── Secondary_Supporter (bucket) → identity
///
/// Logica de ejes:
///   - Cabina gira alrededor de world Y (vertical). Usamos Space.World.
///   - Boom sube/baja: se rota alrededor del eje local Z de Main_Forks
///     (local Z = world Y cuando la cabina no giró → pero como Main_Forks
///      tiene -90°X, su local Z es world UP, no sirve para sube/baja).
///     El eje correcto para sube/baja del boom es el eje HORIZONTAL perpendicular
///     al frente de la excavadora. Dado que el frente es X (rojo), ese eje es Z (azul).
///     En el local space de Main_Forks (-90°X): world Z = local -Y.
///     → Rotamos alrededor de local -Y = Euler(0, angle, 0) con signo correcto.
///
/// Controles:
///   Q / E  → Girar cabina
///   R / F  → Boom sube / baja
///   T / G  → Stick extiende / retrae
///   Y / H  → Bucket abre / cierra
/// </summary>
public class ExcavatorArm : MonoBehaviour
{
    [Header("Velocidades (grados/s)")]
    public float cabinRotationSpeed = 40f;
    public float boomRotationSpeed  = 25f;
    public float stickRotationSpeed = 30f;
    public float bucketRotationSpeed = 40f;

    [Header("Limites Boom (grados)")]
    public float boomMinAngle  = -30f;
    public float boomMaxAngle  =  50f;

    [Header("Limites Stick (grados)")]
    public float stickMinAngle = -70f;
    public float stickMaxAngle =  70f;

    [Header("Limites Bucket (grados)")]
    public float bucketMinAngle = -100f;
    public float bucketMaxAngle =  100f;

    [Header("Suavizado")]
    public float inputSmoothing = 10f;

    // ---- partes detectadas ----
    private Transform cabin;
    private Transform boom;
    private Transform stick;
    private Transform bucket;

    // Rotaciones locales originales (guardadas DESPUES del reparenting)
    private Quaternion boomOriginalLocalRot;
    private Quaternion stickOriginalLocalRot;
    private Quaternion bucketOriginalLocalRot;

    // Angulos acumulados
    private float boomAngle;
    private float stickAngle;
    private float bucketAngle;

    // Inputs suavizados
    private float inCabin;
    private float inBoom;
    private float inStick;
    private float inBucket;

    private bool initialized;

    // ---------------------------------------------------------------
    void Start()
    {
        Transform root = GetRoot();

        // Buscar partes
        cabin  = DeepFind(root, "Body.003");
        boom   = DeepFind(root, "Main_Forks");
        stick  = DeepFind(root, "Secondary Forks");
        bucket = DeepFind(root, "Secondary_Supporter");
        if (bucket == null) bucket = DeepFind(root, "Plane.003");

        // Validar
        if (cabin  == null) { Debug.LogWarning("[ExcavatorArm] Body.003 no encontrado.");       }
        if (boom   == null) { Debug.LogWarning("[ExcavatorArm] Main_Forks no encontrado.");     return; }
        if (stick  == null) { Debug.LogWarning("[ExcavatorArm] Secondary Forks no encontrado."); }
        if (bucket == null) { Debug.LogWarning("[ExcavatorArm] Bucket no encontrado.");          }

        // Emparentar el boom a la cabina para que rote junto con ella.
        // Se hace ANTES de guardar las rotaciones originales.
        if (cabin != null && boom.parent != cabin)
        {
            boom.SetParent(cabin, worldPositionStays: true);
            Debug.Log("[ExcavatorArm] Boom emparentado a la cabina.");
        }

        // Guardar rotaciones locales DESPUES del reparenting.
        boomOriginalLocalRot   = boom.localRotation;
        if (stick  != null) stickOriginalLocalRot  = stick.localRotation;
        if (bucket != null) bucketOriginalLocalRot = bucket.localRotation;

        initialized = true;
        Debug.Log("[ExcavatorArm] Inicializado correctamente.");
    }

    // ---------------------------------------------------------------
    void Update()
    {
        if (!initialized) return;
        ReadInput();
        ApplyCabin();
        ApplyBoom();
        ApplyStick();
        ApplyBucket();
    }

    // ---------------------------------------------------------------
    private void ReadInput()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        float dt = Time.deltaTime;
        float s  = inputSmoothing * dt;

        inCabin  = Mathf.Lerp(inCabin,  (kb.eKey.isPressed ? 1f : kb.qKey.isPressed ? -1f : 0f), s);
        inBoom   = Mathf.Lerp(inBoom,   (kb.rKey.isPressed ? 1f : kb.fKey.isPressed ? -1f : 0f), s);
        inStick  = Mathf.Lerp(inStick,  (kb.tKey.isPressed ? 1f : kb.gKey.isPressed ? -1f : 0f), s);
        inBucket = Mathf.Lerp(inBucket, (kb.yKey.isPressed ? 1f : kb.hKey.isPressed ? -1f : 0f), s);
    }

    // ---------------------------------------------------------------
    /// Cabina: gira en torno al eje Y del mundo.
    private void ApplyCabin()
    {
        if (cabin == null) return;
        cabin.Rotate(Vector3.up, inCabin * cabinRotationSpeed * Time.deltaTime, Space.World);
    }

    // ---------------------------------------------------------------
    /// Boom: sube / baja.
    /// Main_Forks tiene -90° en X, así que:
    ///   local X  → world X  (eje lateral → NO sirve para sube/baja con frente en X)
    ///   local Y  → world -Z
    ///   local Z  → world Y
    /// El eje de sube/baja del boom (perpendicular al frente X y al up Y) es world Z.
    /// World Z en el local space de Main_Forks = local -Y.
    /// Por eso usamos Euler(0, angle, 0) con signo negativo para subir.
    private void ApplyBoom()
    {
        if (boom == null) return;
        boomAngle = Mathf.Clamp(boomAngle + inBoom * boomRotationSpeed * Time.deltaTime,
                                boomMinAngle, boomMaxAngle);
        boom.localRotation = boomOriginalLocalRot * Quaternion.Euler(0f, -boomAngle, 0f);
    }

    // ---------------------------------------------------------------
    /// Stick: extiende / retrae. Mismo eje que el boom.
    private void ApplyStick()
    {
        if (stick == null) return;
        stickAngle = Mathf.Clamp(stickAngle + inStick * stickRotationSpeed * Time.deltaTime,
                                 stickMinAngle, stickMaxAngle);
        stick.localRotation = stickOriginalLocalRot * Quaternion.Euler(0f, -stickAngle, 0f);
    }

    // ---------------------------------------------------------------
    /// Bucket: abre / cierra. Mismo eje.
    private void ApplyBucket()
    {
        if (bucket == null) return;
        bucketAngle = Mathf.Clamp(bucketAngle + inBucket * bucketRotationSpeed * Time.deltaTime,
                                  bucketMinAngle, bucketMaxAngle);
        bucket.localRotation = bucketOriginalLocalRot * Quaternion.Euler(0f, -bucketAngle, 0f);
    }

    // ---------------------------------------------------------------
    /// Sube por la jerarquía buscando el objeto raíz de la excavadora.
    private Transform GetRoot()
    {
        Transform cur  = transform;
        Transform best = transform;
        while (cur != null)
        {
            if (cur.name.Contains("Bull Dozer") ||
                cur.name.Contains("Excavator")  ||
                cur.GetComponent<Rigidbody>() != null)
            {
                best = cur;
            }
            cur = cur.parent;
        }
        return best;
    }

    private Transform DeepFind(Transform parent, string targetName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == targetName) return child;
            Transform found = DeepFind(child, targetName);
            if (found != null) return found;
        }
        return null;
    }
}
