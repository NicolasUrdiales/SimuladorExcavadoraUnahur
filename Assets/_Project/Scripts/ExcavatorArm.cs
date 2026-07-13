using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Control del brazo articulado de la excavadora.
/// Se adapta automaticamente si se coloca en el root o en un hijo.
/// </summary>
public class ExcavatorArm : MonoBehaviour
{
    [Header("Velocidades de Rotacion (grados/s)")]
    public float cabinRotationSpeed = 30f;
    public float boomRotationSpeed = 20f;
    public float stickRotationSpeed = 25f;
    public float bucketRotationSpeed = 35f;

    [Header("Limites del Boom")]
    public float boomMinAngle = -45f;
    public float boomMaxAngle = 45f;

    [Header("Limites del Stick")]
    public float stickMinAngle = -60f;
    public float stickMaxAngle = 60f;

    [Header("Limites del Bucket")]
    public float bucketMinAngle = -90f;
    public float bucketMaxAngle = 90f;

    [Header("Suavizado")]
    public float smoothing = 8f;

    // Partes auto-detectadas
    private Transform cabin;
    private Transform boom;
    private Transform stick;
    private Transform bucket;

    // Inputs suavizados
    private float cabinInput;
    private float boomInput;
    private float stickInput;
    private float bucketInput;

    // Angulos acumulados
    private float currentBoomAngle;
    private float currentStickAngle;
    private float currentBucketAngle;

    // Rotaciones originales del modelo (para preservarlas)
    private Quaternion boomOriginalRot;
    private Quaternion stickOriginalRot;
    private Quaternion bucketOriginalRot;

    // Flag de inicializacion
    private bool initialized = false;

    void Start()
    {
        AutoDetectParts();
    }

    private void AutoDetectParts()
    {
        Transform root = GetExcavatorRoot();

        // Body.003 = cabina/torreta que gira
        cabin = FindChildRecursive(root, "Body.003");
        if (cabin != null)
            Debug.Log("[ExcavatorArm] Cabina encontrada: " + cabin.name);
        else
            Debug.LogWarning("[ExcavatorArm] No se encontro 'Body.003' como cabina.");

        // Main_Forks = boom (brazo principal)
        boom = FindChildRecursive(root, "Main_Forks");
        if (boom != null)
        {
            boomOriginalRot = boom.localRotation;
            Debug.Log("[ExcavatorArm] Boom encontrado: " + boom.name);
        }
        else
            Debug.LogWarning("[ExcavatorArm] No se encontro 'Main_Forks' como boom.");

        // Secondary Forks = stick (balancin)
        stick = FindChildRecursive(root, "Secondary Forks");
        if (stick != null)
        {
            stickOriginalRot = stick.localRotation;
            Debug.Log("[ExcavatorArm] Stick encontrado: " + stick.name);
        }
        else
            Debug.LogWarning("[ExcavatorArm] No se encontro 'Secondary Forks' como stick.");

        // Secondary_Supporter = bucket (pala/cuchara)
        bucket = FindChildRecursive(root, "Secondary_Supporter");
        if (bucket == null)
        {
            // Fallback: buscar Plane.003 que puede ser la pala
            bucket = FindChildRecursive(root, "Plane.003");
        }
        if (bucket != null)
        {
            bucketOriginalRot = bucket.localRotation;
            Debug.Log("[ExcavatorArm] Bucket encontrado: " + bucket.name);
        }
        else
            Debug.LogWarning("[ExcavatorArm] No se encontro bucket (pala).");

        // CRITICAL: Emparentar el brazo (boom) a la cabina para que roten juntos
        if (boom != null && cabin != null && boom.parent != cabin)
        {
            boom.SetParent(cabin, true);
            Debug.Log("[ExcavatorArm] Emparentando el brazo (boom) a la cabina para que roten juntos.");
        }

        initialized = true;
    }

    void Update()
    {
        if (!initialized) return;

        ReadInput();
        MoveCabin();
        MoveBoom();
        MoveStick();
        MoveBucket();
    }

    private void ReadInput()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        // Cabina: Q/E
        float rawCabin = 0f;
        if (kb.eKey.isPressed) rawCabin = 1f;
        else if (kb.qKey.isPressed) rawCabin = -1f;

        // Boom: R (subir) / F (bajar)
        float rawBoom = 0f;
        if (kb.rKey.isPressed) rawBoom = 1f;
        else if (kb.fKey.isPressed) rawBoom = -1f;

        // Stick: T (extender) / G (retraer)
        float rawStick = 0f;
        if (kb.tKey.isPressed) rawStick = 1f;
        else if (kb.gKey.isPressed) rawStick = -1f;

        // Bucket: Y (abrir) / H (cerrar)
        float rawBucket = 0f;
        if (kb.yKey.isPressed) rawBucket = 1f;
        else if (kb.hKey.isPressed) rawBucket = -1f;

        // Suavizar inputs
        cabinInput = Mathf.Lerp(cabinInput, rawCabin, smoothing * Time.deltaTime);
        boomInput = Mathf.Lerp(boomInput, rawBoom, smoothing * Time.deltaTime);
        stickInput = Mathf.Lerp(stickInput, rawStick, smoothing * Time.deltaTime);
        bucketInput = Mathf.Lerp(bucketInput, rawBucket, smoothing * Time.deltaTime);
    }

    private void MoveCabin()
    {
        if (cabin == null) return;
        float rotAmount = cabinInput * cabinRotationSpeed * Time.deltaTime;
        cabin.Rotate(Vector3.up, rotAmount, Space.Self);
    }

    private void MoveBoom()
    {
        if (boom == null) return;

        currentBoomAngle += boomInput * boomRotationSpeed * Time.deltaTime;
        currentBoomAngle = Mathf.Clamp(currentBoomAngle, boomMinAngle, boomMaxAngle);

        boom.localRotation = boomOriginalRot * Quaternion.Euler(0f, 0f, currentBoomAngle);
    }

    private void MoveStick()
    {
        if (stick == null) return;

        currentStickAngle += stickInput * stickRotationSpeed * Time.deltaTime;
        currentStickAngle = Mathf.Clamp(currentStickAngle, stickMinAngle, stickMaxAngle);

        stick.localRotation = stickOriginalRot * Quaternion.Euler(0f, 0f, currentStickAngle);
    }

    private void MoveBucket()
    {
        if (bucket == null) return;

        currentBucketAngle += bucketInput * bucketRotationSpeed * Time.deltaTime;
        currentBucketAngle = Mathf.Clamp(currentBucketAngle, bucketMinAngle, bucketMaxAngle);

        bucket.localRotation = bucketOriginalRot * Quaternion.Euler(0f, 0f, currentBucketAngle);
    }

    private Transform GetExcavatorRoot()
    {
        Transform current = transform;
        Transform bestRoot = transform;
        while (current != null)
        {
            if (current.name.Contains("Bull Dozer") || current.name.Contains("Excavator") || current.GetComponent<Rigidbody>() != null)
            {
                bestRoot = current;
            }
            current = current.parent;
        }
        return bestRoot;
    }

    private Transform FindChildRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform found = FindChildRecursive(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
