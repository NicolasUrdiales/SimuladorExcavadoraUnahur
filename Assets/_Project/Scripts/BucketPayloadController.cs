using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controlador de la carga de tierra en la pala de la excavadora.
/// Administra la masa de tierra visible dentro de la cuchara y detecta el momento en que
/// el operador la vuelca para depositar monticulos de tierra 3D en el suelo.
/// </summary>
public class BucketPayloadController : MonoBehaviour
{
    [Header("Referencias (Auto-detectadas)")]
    public Transform bucketTransform;

    [Header("Configuración de Carga")]
    public float maxPayloadCapacity = 1.0f;
    public float dumpAngleThreshold = 45.0f; // Angulo de inclinacion hacia abajo para volcar
    public Material dirtMaterial;

    // Estado Interno
    private float _currentPayload = 0.0f;
    private GameObject _bucketDirtVisual;
    private float _lastDumpTime;

    public float CurrentPayload => _currentPayload;
    public bool IsBucketFull => _currentPayload >= maxPayloadCapacity;

    void Start()
    {
        AutoFindBucket();
        CreateBucketDirtVisual();
    }

    private void AutoFindBucket()
    {
        if (bucketTransform == null)
        {
            var arm = FindAnyObjectByType<ExcavatorArm>();
            if (arm != null && arm.BucketTransform != null)
                bucketTransform = arm.BucketTransform;
        }
    }

    void CreateBucketDirtVisual()
    {
        if (bucketTransform == null) return;

        // Crear objeto visual de tierra dentro de la cuchara
        _bucketDirtVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        _bucketDirtVisual.name = "BucketDirtVisual";
        Destroy(_bucketDirtVisual.GetComponent<Collider>());

        _bucketDirtVisual.transform.SetParent(bucketTransform, false);
        _bucketDirtVisual.transform.localPosition = new Vector3(0f, 0.05f, 0.15f);
        _bucketDirtVisual.transform.localRotation = Quaternion.identity;
        _bucketDirtVisual.transform.localScale = Vector3.zero;

        if (dirtMaterial == null)
            dirtMaterial = Excavator.Reporting.RealisticDirtTextureGenerator.GetOrCreateRealisticDirtMaterial();

        // Asignar material de tierra
        if (dirtMaterial != null)
        {
            var rend = _bucketDirtVisual.GetComponent<Renderer>();
            if (rend != null) rend.material = dirtMaterial;
        }
    }

    public void AddPayload(float amount)
    {
        _currentPayload = Mathf.Clamp(_currentPayload + amount, 0f, maxPayloadCapacity);
        UpdateBucketVisual();
    }

    void Update()
    {
        if (bucketTransform == null) AutoFindBucket();

        CheckBucketDumping();
    }

    private void UpdateBucketVisual()
    {
        if (_bucketDirtVisual == null) return;

        float fillRatio = _currentPayload / maxPayloadCapacity;
        if (fillRatio <= 0.02f)
        {
            _bucketDirtVisual.transform.localScale = Vector3.zero;
        }
        else
        {
            // Escala progresiva de tierra dentro de la pala
            float s = Mathf.Lerp(0.35f, 0.95f, fillRatio);
            _bucketDirtVisual.transform.localScale = new Vector3(s * 1.3f, s * 0.75f, s * 1.1f);
        }
    }

    private void CheckBucketDumping()
    {
        if (_currentPayload <= 0.05f || bucketTransform == null) return;

        // Evaluar inclinacion de la pala respecto a la vertical
        float tiltAngle = Vector3.Angle(bucketTransform.forward, Vector3.down);

        if (tiltAngle <= dumpAngleThreshold)
        {
            DumpSoilToGround();
        }
    }

    private void DumpSoilToGround()
    {
        if (Time.time - _lastDumpTime < 0.6f) return;
        _lastDumpTime = Time.time;

        float dumpedVolume = _currentPayload;
        _currentPayload = 0f;
        UpdateBucketVisual();

        // Proyectar raycast hacia el suelo para colocar el monticulo 3D
        Vector3 dumpOrigin = bucketTransform.position;
        Vector3 groundPos = dumpOrigin + Vector3.down * 1.5f;

        if (Physics.Raycast(dumpOrigin, Vector3.down, out RaycastHit hit, 6.0f))
        {
            groundPos = hit.point;
        }
        else
        {
            groundPos.y = 0f;
        }

        SpawnOrGrowDirtMound(groundPos, dumpedVolume);
    }

    private void SpawnOrGrowDirtMound(Vector3 pos, float volume)
    {
        // Buscar monticulos existentes cercanos
        float searchRadius = 1.2f;
        DirtMound existingMound = null;

        var mounds = FindObjectsByType<DirtMound>(FindObjectsInactive.Exclude);
        foreach (var m in mounds)
        {
            if (Vector3.Distance(m.transform.position, pos) <= searchRadius)
            {
                existingMound = m;
                break;
            }
        }

        if (existingMound != null)
        {
            existingMound.AddDirtVolume(volume);
            Debug.Log($"[BucketPayload] Montículo incrementado en {pos}");
        }
        else
        {
            GameObject moundGo = new GameObject("DirtMound_Instance");
            moundGo.transform.position = pos;
            moundGo.transform.rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);

            DirtMound newMound = moundGo.AddComponent<DirtMound>();
            newMound.AddDirtVolume(volume);

            if (dirtMaterial != null)
            {
                var rend = moundGo.GetComponent<Renderer>();
                if (rend != null) rend.material = dirtMaterial;
            }

            Debug.Log($"[BucketPayload] Nuevo montículo de tierra creado en {pos}");
        }
    }
}
