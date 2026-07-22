using UnityEngine;

/// <summary>
/// Representa un monticulo de tierra 3D generado dinamicamente al vaciar la pala de la excavadora.
/// Incrementa su tamaño a medida que se descarga mas tierra en el mismo sector.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class DirtMound : MonoBehaviour
{
    [Header("Configuracion del Monticulo")]
    public float baseRadius = 0.8f;
    public float baseHeight = 0.5f;

    private float _volumeScale = 1.0f;
    private MeshFilter _mf;
    private MeshRenderer _mr;
    private MeshCollider _mc;

    void Awake()
    {
        _mf = GetComponent<MeshFilter>();
        _mr = GetComponent<MeshRenderer>();
        _mc = GetComponent<MeshCollider>();

        BuildMoundMesh();
    }

    public void AddDirtVolume(float amount)
    {
        _volumeScale += amount * 0.4f;
        _volumeScale = Mathf.Clamp(_volumeScale, 0.5f, 3.5f);
        transform.localScale = Vector3.one * _volumeScale;
    }

    void BuildMoundMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "DirtMoundMesh";

        int segments = 16;
        int vertCount = segments + 2; // +1 centro base, +1 vertice cuspide

        Vector3[] vertices = new Vector3[vertCount];
        Vector2[] uv = new Vector2[vertCount];

        // Vertice 0: Cuspide del monticulo
        vertices[0] = new Vector3(0f, baseHeight, 0f);
        uv[0] = new Vector2(0.5f, 0.5f);

        // Vertice 1: Centro base
        vertices[1] = new Vector3(0f, 0f, 0f);
        uv[1] = new Vector2(0.5f, 0.5f);

        float angleStep = (Mathf.PI * 2f) / segments;
        for (int i = 0; i < segments; i++)
        {
            float a = i * angleStep;
            float x = Mathf.Cos(a) * baseRadius;
            float z = Mathf.Sin(a) * baseRadius;
            vertices[i + 2] = new Vector3(x, 0f, z);
            uv[i + 2] = new Vector2((x / (baseRadius * 2f)) + 0.5f, (z / (baseRadius * 2f)) + 0.5f);
        }

        // Triangulos para el cono suavizado del monticulo
        int[] triangles = new int[segments * 3];
        int tIdx = 0;
        for (int i = 0; i < segments; i++)
        {
            int next = (i == segments - 1) ? 2 : i + 3;
            triangles[tIdx++] = 0;
            triangles[tIdx++] = i + 2;
            triangles[tIdx++] = next;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uv;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        _mf.sharedMesh = mesh;
        _mc.sharedMesh = mesh;
    }
}
