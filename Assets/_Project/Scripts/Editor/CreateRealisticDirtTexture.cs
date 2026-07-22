using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Generador procedural de archivos de textura PNG de tierra realista y mapas de normales 3D.
/// Guarda los assets en el proyecto Unity para ser usados en materiales PBR/Lit.
/// </summary>
public static class CreateRealisticDirtTexture
{
    public static Material GenerateAndGetDirtMaterial()
    {
        string dir = "Assets/_Project/Textures";
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        string albedoPath = dir + "/DirtRealAlbedo.png";
        string normalPath = dir + "/DirtRealNormal.png";

        if (!File.Exists(albedoPath))
        {
            Texture2D albedo = new Texture2D(1024, 1024, TextureFormat.RGBA32, true);
            Color base1 = new Color(0.38f, 0.26f, 0.16f); // Tierra organica
            Color base2 = new Color(0.26f, 0.16f, 0.09f); // Barro/tierra oscura
            Color base3 = new Color(0.48f, 0.34f, 0.20f); // Arena/tierra seca

            for (int y = 0; y < 1024; y++)
            {
                for (int x = 0; x < 1024; x++)
                {
                    float u = (float)x / 1024f;
                    float v = (float)y / 1024f;

                    float n1 = Mathf.PerlinNoise(u * 14f, v * 14f);
                    float n2 = Mathf.PerlinNoise(u * 38f + 3.1f, v * 38f + 7.4f);
                    float n3 = Mathf.PerlinNoise(u * 120f + 1.5f, v * 120f + 9.2f);

                    Color c = Color.Lerp(base1, base2, n1);
                    c = Color.Lerp(c, base3, n2 * 0.35f);

                    float grain = (n3 - 0.5f) * 0.14f;
                    c.r = Mathf.Clamp01(c.r + grain);
                    c.g = Mathf.Clamp01(c.g + grain * 0.8f);
                    c.b = Mathf.Clamp01(c.b + grain * 0.6f);

                    albedo.SetPixel(x, y, c);
                }
            }
            albedo.Apply();
            File.WriteAllBytes(albedoPath, albedo.EncodeToPNG());
            AssetDatabase.ImportAsset(albedoPath);
        }

        if (!File.Exists(normalPath))
        {
            Texture2D normal = new Texture2D(512, 512, TextureFormat.RGBA32, true);
            for (int y = 0; y < 512; y++)
            {
                for (int x = 0; x < 512; x++)
                {
                    float u = (float)x / 512f;
                    float v = (float)y / 512f;

                    float nL = Mathf.PerlinNoise((u - 0.01f) * 22f, v * 22f);
                    float nR = Mathf.PerlinNoise((u + 0.01f) * 22f, v * 22f);
                    float nD = Mathf.PerlinNoise(u * 22f, (v - 0.01f) * 22f);
                    float nU = Mathf.PerlinNoise(u * 22f, (v + 0.01f) * 22f);

                    float dX = (nL - nR) * 4.2f;
                    float dY = (nD - nU) * 4.2f;
                    Vector3 n = new Vector3(dX, dY, 1.0f).normalized;

                    Color c = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1.0f);
                    normal.SetPixel(x, y, c);
                }
            }
            normal.Apply();
            File.WriteAllBytes(normalPath, normal.EncodeToPNG());
            AssetDatabase.ImportAsset(normalPath);
        }

        Texture2D loadedAlbedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
        Texture2D loadedNormal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);

        string matPath = "Assets/_Project/Materials/DirtRealMaterial.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.name = "DirtRealMaterial";
            AssetDatabase.CreateAsset(mat, matPath);
        }

        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", loadedAlbedo);
        else if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", loadedAlbedo);

        if (mat.HasProperty("_BumpMap"))
        {
            mat.SetTexture("_BumpMap", loadedNormal);
            mat.EnableKeyword("_NORMALMAP");
        }

        if (mat.HasProperty("_BaseMap")) mat.SetTextureScale("_BaseMap", new Vector2(24f, 24f));
        else if (mat.HasProperty("_MainTex")) mat.SetTextureScale("_MainTex", new Vector2(24f, 24f));

        if (mat.HasProperty("_BumpMap")) mat.SetTextureScale("_BumpMap", new Vector2(24f, 24f));

        AssetDatabase.SaveAssets();
        return mat;
    }
}
