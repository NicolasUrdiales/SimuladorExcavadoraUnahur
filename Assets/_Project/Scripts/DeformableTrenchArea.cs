using System;
using UnityEngine;
using Excavator.Trench;

/// <summary>
/// Terreno deformable en tiempo real para el ejercicio de excavacion de zanja.
/// Implementa ITrenchTerrainSource para desacoplar el renderizado de malla de la evaluacion del ejercicio.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class DeformableTrenchArea : MonoBehaviour, ITrenchTerrainSource
{
    [Header("Dimensiones del Área de Zanja")]
    public float width = 6.0f;       // Ancho (eje X)
    public float length = 3.0f;      // Largo (eje Z)
    public float maxDepth = 1.2f;    // Profundidad maxima objetivo (metros)

    [Header("Resolución de Malla")]
    public int gridSubdivisionsX = 24;
    public int gridSubdivisionsZ = 12;

    [Header("Parámetros de Deformación")]
    public float digRadius = 0.65f;       // Radio de influencia de la pala
    public float digSpeed = 0.85f;        // Velocidad de excavacion por segundo
    public float payloadGainRate = 1.5f;  // Tasa de transferencia a la pala

    [Header("Referencias (Auto-detectadas)")]
    public Transform bucketTip;
    public BucketPayloadController bucketPayload;
    public Material dirtMaterial;

    // Estado Interno
    private Mesh _mesh;
    private MeshCollider _meshCollider;
    private Vector3[] _vertices;
    private float[] _initialY;
    private float[] _targetY;
    private float _totalDeformationVolume;
    private float _maxDeformationVolume;
    private float _completionPercentage;
    private bool _isCompleted;

    // IMGUI
    private Texture2D _white;
    private GUIStyle _hudTitleStyle;
    private GUIStyle _hudPercentStyle;
    private bool _guiReady;

    // --- Implementacion de ITrenchTerrainSource ---
    public int GridCols => gridSubdivisionsX + 1;
    public int GridRows => gridSubdivisionsZ + 1;
    public float AreaWidth => width;
    public float AreaLength => length;

    public float CompletionPercentage => _completionPercentage;
    public bool IsTrenchCompleted => _isCompleted;

    public event Action TrenchCompleted;

    void Awake()
    {
        GenerateTrenchMesh();
    }

    void Start()
    {
        AutoFindReferences();
    }

    private void AutoFindReferences()
    {
        if (bucketTip == null)
        {
            var arm = FindAnyObjectByType<ExcavatorArm>();
            if (arm != null && arm.BucketTransform != null)
                bucketTip = arm.BucketTransform;
        }

        if (bucketPayload == null)
            bucketPayload = FindAnyObjectByType<BucketPayloadController>();
    }

    void GenerateTrenchMesh()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        _meshCollider = GetComponent<MeshCollider>();

        _mesh = new Mesh();
        _mesh.name = "DeformableTrenchMesh";

        int numVertsX = gridSubdivisionsX + 1;
        int numVertsZ = gridSubdivisionsZ + 1;
        int totalVerts = numVertsX * numVertsZ;

        _vertices = new Vector3[totalVerts];
        _initialY = new float[totalVerts];
        _targetY = new float[totalVerts];

        Vector2[] uv = new Vector2[totalVerts];
        float halfW = width * 0.5f;
        float halfL = length * 0.5f;

        float stepX = width / gridSubdivisionsX;
        float stepZ = length / gridSubdivisionsZ;

        int idx = 0;
        for (int z = 0; z <= gridSubdivisionsZ; z++)
        {
            for (int x = 0; x <= gridSubdivisionsX; x++)
            {
                float posX = -halfW + (x * stepX);
                float posZ = -halfL + (z * stepZ);
                _vertices[idx] = new Vector3(posX, 0f, posZ);
                _initialY[idx] = 0f;
                _targetY[idx] = -maxDepth;
                uv[idx] = new Vector2((float)x / gridSubdivisionsX, (float)z / gridSubdivisionsZ);
                idx++;
            }
        }

        int[] triangles = new int[gridSubdivisionsX * gridSubdivisionsZ * 6];
        int tIdx = 0;
        for (int z = 0; z < gridSubdivisionsZ; z++)
        {
            for (int x = 0; x < gridSubdivisionsX; x++)
            {
                int vert = z * numVertsX + x;
                triangles[tIdx++] = vert;
                triangles[tIdx++] = vert + numVertsX;
                triangles[tIdx++] = vert + 1;

                triangles[tIdx++] = vert + 1;
                triangles[tIdx++] = vert + numVertsX;
                triangles[tIdx++] = vert + numVertsX + 1;
            }
        }

        _mesh.vertices = _vertices;
        _mesh.triangles = triangles;
        _mesh.uv = uv;
        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();

        mf.sharedMesh = _mesh;
        _meshCollider.sharedMesh = _mesh;

        if (dirtMaterial == null)
            dirtMaterial = Excavator.Reporting.RealisticDirtTextureGenerator.GetOrCreateRealisticDirtMaterial();

        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr != null && dirtMaterial != null)
        {
            mr.sharedMaterial = dirtMaterial;
        }

        _maxDeformationVolume = totalVerts * maxDepth * 0.75f;
    }

    void Update()
    {
        if (bucketTip == null) AutoFindReferences();

        if (bucketTip != null)
        {
            ProcessDigging(bucketTip.position);
        }

        CalculateCompletion();
    }

    private void ProcessDigging(Vector3 tipWorldPos)
    {
        float lowestY = tipWorldPos.y;
        try
        {
            if (bucketTip != null)
            {
                Collider col = bucketTip.GetComponent<Collider>() ?? bucketTip.GetComponentInChildren<Collider>();
                if (col != null && col.enabled)
                {
                    lowestY = col.bounds.min.y;
                }
            }
        }
        catch
        {
            lowestY = tipWorldPos.y;
        }

        Vector3 localPos = transform.InverseTransformPoint(tipWorldPos);

        float halfW = width * 0.5f;
        float halfL = length * 0.5f;

        if (Mathf.Abs(localPos.x) > halfW + 1.2f || Mathf.Abs(localPos.z) > halfL + 1.2f)
            return;

        if (lowestY > 0.85f) return;

        bool meshDeformed = false;
        float totalDugThisFrame = 0f;

        for (int i = 0; i < _vertices.Length; i++)
        {
            Vector3 vLocal = _vertices[i];
            float distXZ = Vector2.Distance(new Vector2(vLocal.x, vLocal.z), new Vector2(localPos.x, localPos.z));

            if (distXZ <= digRadius)
            {
                float factor = Mathf.SmoothStep(1f, 0f, distXZ / digRadius);
                float digDelta = digSpeed * factor * Time.deltaTime;

                float currentY = _vertices[i].y;
                float targetY = _targetY[i];

                if (currentY > targetY)
                {
                    float newY = Mathf.Max(targetY, currentY - digDelta);
                    float actualDug = currentY - newY;
                    _vertices[i].y = newY;
                    totalDugThisFrame += actualDug;
                    meshDeformed = true;
                }
            }
        }

        if (meshDeformed)
        {
            _mesh.vertices = _vertices;
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();

            if (_meshCollider != null)
            {
                _meshCollider.sharedMesh = _mesh;
            }

            if (bucketPayload != null && totalDugThisFrame > 0f)
            {
                bucketPayload.AddPayload(totalDugThisFrame * payloadGainRate);
            }
        }
    }

    private void CalculateCompletion()
    {
        float currentDeformSum = 0f;
        for (int i = 0; i < _vertices.Length; i++)
        {
            currentDeformSum += Mathf.Abs(_vertices[i].y);
        }

        _totalDeformationVolume = currentDeformSum;
        _completionPercentage = Mathf.Clamp((_totalDeformationVolume / _maxDeformationVolume) * 100f, 0f, 100f);

        if (!_isCompleted && _completionPercentage >= 99.5f)
        {
            _isCompleted = true;
            _completionPercentage = 100f;
            Debug.Log("[DeformableTrenchArea] ¡EXCAVACIÓN DE ZANJA COMPLETADA AL 100%!");
            TrenchCompleted?.Invoke();
        }
    }

    // -------------------------------------------------------
    // IMPLEMENTACIÓN DE ITrenchTerrainSource
    // -------------------------------------------------------
    public float GetDepthAt(int col, int row)
    {
        if (_vertices == null) return 0f;
        col = Mathf.Clamp(col, 0, gridSubdivisionsX);
        row = Mathf.Clamp(row, 0, gridSubdivisionsZ);
        int idx = row * (gridSubdivisionsX + 1) + col;
        if (idx >= 0 && idx < _vertices.Length)
        {
            return Mathf.Abs(_vertices[idx].y);
        }
        return 0f;
    }

    public Vector3 GetCellWorldPosition(int col, int row)
    {
        if (_vertices == null) return transform.position;
        col = Mathf.Clamp(col, 0, gridSubdivisionsX);
        row = Mathf.Clamp(row, 0, gridSubdivisionsZ);
        int idx = row * (gridSubdivisionsX + 1) + col;
        if (idx >= 0 && idx < _vertices.Length)
        {
            return transform.TransformPoint(_vertices[idx]);
        }
        return transform.position;
    }

    public void ResetTerrain()
    {
        if (_vertices == null) return;
        for (int i = 0; i < _vertices.Length; i++)
        {
            _vertices[i].y = 0f;
        }
        if (_mesh != null)
        {
            _mesh.vertices = _vertices;
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
            if (_meshCollider != null) _meshCollider.sharedMesh = _mesh;
        }
        _totalDeformationVolume = 0f;
        _completionPercentage = 0f;
        _isCompleted = false;
        Debug.Log("[DeformableTrenchArea] Terreno de zanja restablecido al 100%.");
    }

    // -------------------------------------------------------
    // IMGUI HUD DE PROGRESO DE ZANJA
    // -------------------------------------------------------
    void OnGUI()
    {
        EnsureGUI();

        float barW = 380f;
        float barH = 45f;
        float px = (Screen.width - barW) * 0.5f;
        float py = 18f;

        DrawRect(new Rect(px, py, barW, barH), new Color(0.06f, 0.08f, 0.12f, 0.92f));

        Color barColor = _isCompleted
            ? new Color(0.10f, 0.88f, 0.30f)
            : new Color(0.20f, 0.65f, 0.95f);

        DrawRect(new Rect(px, py, barW, 3f), barColor);

        _hudTitleStyle.normal.textColor = Color.white;
        string statusTitle = _isCompleted
            ? "✔ ZANJA EXCAVADA — VAYA A LA PARKZONE"
            : "EJERCICIO DE EXCAVACIÓN DE ZANJA";

        GUI.Label(new Rect(px + 12f, py + 6f, barW - 24f, 18f), statusTitle, _hudTitleStyle);

        float progressFrac = _completionPercentage / 100f;
        float innerBarW = barW - 24f;
        float innerBarH = 10f;
        float innerBarX = px + 12f;
        float innerBarY = py + 26f;

        DrawRect(new Rect(innerBarX, innerBarY, innerBarW, innerBarH), new Color(0.15f, 0.18f, 0.22f));

        if (progressFrac > 0.01f)
        {
            DrawRect(new Rect(innerBarX, innerBarY, innerBarW * progressFrac, innerBarH), barColor);
        }

        _hudPercentStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(px + barW - 70f, py + 6f, 60f, 18f), $"{_completionPercentage:F1}%", _hudPercentStyle);
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

        _hudPercentStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 11,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleRight
        };

        _guiReady = true;
    }
}
