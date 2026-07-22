using System.IO;
using UnityEngine;

namespace Excavator.Reporting
{
    /// <summary>
    /// Generador procedural de textura de tierra realista y mapa de normales en C# nativo.
    /// Crea archivos PNG de textura con granulado, matices de barro/tierra y mapa de relieve 3D.
    /// </summary>
    public static class RealisticDirtTextureGenerator
    {
        public static Material GetOrCreateRealisticDirtMaterial()
        {
            Texture2D albedoTex = GetOrCreateAlbedoTexture();
            Texture2D normalTex = GetOrCreateNormalTexture();

            Shader litShader = Shader.Find("Universal Render Pipeline/Lit")
                            ?? Shader.Find("Standard");

            Material mat = new Material(litShader);
            mat.name = "RealisticDirtMaterial";

            if (mat.HasProperty("_BaseMap"))
                mat.SetTexture("_BaseMap", albedoTex);
            else if (mat.HasProperty("_MainTex"))
                mat.SetTexture("_MainTex", albedoTex);

            if (mat.HasProperty("_BumpMap"))
            {
                mat.SetTexture("_BumpMap", normalTex);
                mat.EnableKeyword("_NORMALMAP");
            }

            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", 0.15f);

            // Configurar Tiling en la textura (repetition de patron para realismo)
            if (mat.HasProperty("_BaseMap"))
                mat.SetTextureScale("_BaseMap", new Vector2(16f, 16f));
            else if (mat.HasProperty("_MainTex"))
                mat.SetTextureScale("_MainTex", new Vector2(16f, 16f));

            if (mat.HasProperty("_BumpMap"))
                mat.SetTextureScale("_BumpMap", new Vector2(16f, 16f));

            return mat;
        }

        private static Texture2D GetOrCreateAlbedoTexture()
        {
            int size = 512;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            tex.name = "DirtAlbedoProc";

            Color baseColor1 = new Color(0.32f, 0.22f, 0.14f); // Tierra oscura
            Color baseColor2 = new Color(0.42f, 0.30f, 0.18f); // Tierra media
            Color pebbleColor = new Color(0.25f, 0.18f, 0.12f); // Piedrecillas/Barro

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float v = (float)y / size;

                    float n1 = Mathf.PerlinNoise(u * 12f, v * 12f);
                    float n2 = Mathf.PerlinNoise(u * 28f + 5.2f, v * 28f + 1.3f);
                    float n3 = Mathf.PerlinNoise(u * 64f + 12f, v * 64f + 8f);

                    Color col = Color.Lerp(baseColor1, baseColor2, n1);
                    col = Color.Lerp(col, pebbleColor, n2 * 0.4f);

                    // Granulado de micro-textura
                    float grain = (n3 - 0.5f) * 0.08f;
                    col.r = Mathf.Clamp01(col.r + grain);
                    col.g = Mathf.Clamp01(col.g + grain * 0.8f);
                    col.b = Mathf.Clamp01(col.b + grain * 0.6f);

                    tex.SetPixel(x, y, col);
                }
            }

            tex.Apply();
            return tex;
        }

        private static Texture2D GetOrCreateNormalTexture()
        {
            int size = 512;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            tex.name = "DirtNormalProc";

            float bumpScale = 3.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float v = (float)y / size;

                    float nL = Mathf.PerlinNoise((u - 0.01f) * 20f, v * 20f);
                    float nR = Mathf.PerlinNoise((u + 0.01f) * 20f, v * 20f);
                    float nD = Mathf.PerlinNoise(u * 20f, (v - 0.01f) * 20f);
                    float nU = Mathf.PerlinNoise(u * 20f, (v + 0.01f) * 20f);

                    float dX = (nL - nR) * bumpScale;
                    float dY = (nD - nU) * bumpScale;

                    Vector3 normal = new Vector3(dX, dY, 1.0f).normalized;

                    // Mapear normal de [-1, 1] a [0, 1]
                    Color col = new Color(
                        normal.x * 0.5f + 0.5f,
                        normal.y * 0.5f + 0.5f,
                        normal.z * 0.5f + 0.5f,
                        1.0f);

                    tex.SetPixel(x, y, col);
                }
            }

            tex.Apply();
            return tex;
        }
    }
}
