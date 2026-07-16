using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Control del brazo articulado de la excavadora.
/// Soporta dos jerarquias automaticamente:
///
/// MODELO Bull Dozer 2:
///   ├── Body.003   (cabina)  — giro horizontal (Y mundo)
///   ├── Main_Forks (boom)   — sube/baja (Y local invertido)
///   │   └── Secondary Forks (stick)
///   │       └── Secondary_Supporter / Plane.003 (bucket)
///
/// MODELO Excavator (Low Poly Heavy Machinery Pack):
///   ├── Base    (cabina) — giro horizontal (Y mundo)
///   │   └── Arm_1 (boom)  — pitch (X local)
///   │       └── Arm_2 (stick)
///   │           └── Head (bucket)
///
/// Controles: Q/E cabina, R/F boom, T/G stick, Y/H bucket
/// </summary>
public class ExcavatorArm : MonoBehaviour
{
    [Header("Velocidades (grados/s)")]
    public float cabinRotationSpeed = 40f;
    public float boomRotationSpeed  = 25f;
    public float stickRotationSpeed = 30f;
    public float bucketRotationSpeed = 40f;

    [Header("Limites (grados)")]
    public float boomMinAngle = -30f;
    public float boomMaxAngle = 50f;
    public float stickMinAngle = -70f;
    public float stickMaxAngle = 70f;
    public float bucketMinAngle = -100f;
    public float bucketMaxAngle = 100f;

    [Header("Suavizado")]
    public float inputSmoothing = 10f;

    private Transform cabin, boom, stick, bucket;
    private Quaternion boomOriginalLocalRot, stickOriginalLocalRot, bucketOriginalLocalRot;
    private float boomAngle, stickAngle, bucketAngle;
    private float inCabin, inBoom, inStick, inBucket;
    private bool initialized;
    private bool isBullDozer;

    void Start()
    {
        Transform root = GetRoot();

        // Intentar detectar estructura
        if (DeepFind(root, "Body.003") != null)
        {
            isBullDozer = true;
            cabin = DeepFind(root, "Body.003");
            boom = DeepFind(root, "Main_Forks");
            stick = DeepFind(root, "Secondary Forks");
            bucket = DeepFind(root, "Secondary_Supporter") ?? DeepFind(root, "Plane.003");
        }
        else
        {
            isBullDozer = false;
            cabin = DeepFind(root, "Base");
            boom = DeepFind(root, "Arm_1");
            stick = DeepFind(root, "Arm_2");
            bucket = DeepFind(root, "Head");
        }

        if (boom == null) { Debug.LogError("No se pudo detectar la estructura de la excavadora."); return; }

        if (isBullDozer && cabin != null && boom.parent != cabin)
            boom.SetParent(cabin, true);

        boomOriginalLocalRot = boom.localRotation;
        if (stick != null) stickOriginalLocalRot = stick.localRotation;
        if (bucket != null) bucketOriginalLocalRot = bucket.localRotation;

        initialized = true;
    }

    void Update()
    {
        if (!initialized) return;
        ReadInput();
        ApplyCabin();
        ApplyRotation(boom,   ref boomAngle,   inBoom,   boomRotationSpeed,   boomOriginalLocalRot,   boomMinAngle,   boomMaxAngle);
        ApplyRotation(stick,  ref stickAngle,  inStick,  stickRotationSpeed,  stickOriginalLocalRot,  stickMinAngle,  stickMaxAngle);
        ApplyRotation(bucket, ref bucketAngle, inBucket, bucketRotationSpeed, bucketOriginalLocalRot, bucketMinAngle, bucketMaxAngle);
    }

    private void ReadInput()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;
        float s = inputSmoothing * Time.deltaTime;
        inCabin = Mathf.Lerp(inCabin, (kb.eKey.isPressed ? 1f : kb.qKey.isPressed ? -1f : 0f), s);
        inBoom = Mathf.Lerp(inBoom, (kb.rKey.isPressed ? 1f : kb.fKey.isPressed ? -1f : 0f), s);
        inStick = Mathf.Lerp(inStick, (kb.tKey.isPressed ? 1f : kb.gKey.isPressed ? -1f : 0f), s);
        inBucket = Mathf.Lerp(inBucket, (kb.yKey.isPressed ? 1f : kb.hKey.isPressed ? -1f : 0f), s);
    }

    private void ApplyCabin()
    {
        if (cabin == null) return;
        cabin.Rotate(Vector3.up, inCabin * cabinRotationSpeed * Time.deltaTime, Space.World);
    }

    private void ApplyRotation(Transform t, ref float angle, float input, float speed,
                               Quaternion startRot, float minAngle, float maxAngle)
    {
        if (t == null) return;
        angle = Mathf.Clamp(angle + input * speed * Time.deltaTime, minAngle, maxAngle);
        // Bull Dozer 2: eje Y local invertido (-90°X en el FBX). Excavator: eje X local (pitch).
        Vector3 axis = isBullDozer ? new Vector3(0f, -1f, 0f) : new Vector3(1f, 0f, 0f);
        t.localRotation = startRot * Quaternion.AngleAxis(angle, axis);
    }

    private Transform GetRoot()
    {
        Transform cur = transform;
        Transform best = transform;
        while (cur != null)
        {
            if (cur.name.Contains("Bull Dozer") || cur.name.Contains("Excavator")) best = cur;
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
