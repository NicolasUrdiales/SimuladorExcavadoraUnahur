using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// Script de Editor para duplicar EXPACTAMENTE el Escenario1_Circuito en Escenario2_Zanja,
/// conservando la excavadora, camara, motor, mandos y sonido 100% identicos,
/// y adaptando unicamente el entorno: recinto de barreras en cuadrado, suelo de tierra,
/// zona verde con terreno deformable para zanja y ParkZone.
/// </summary>
public static class DuplicateAndSetupEscenarioZanja
{
    [MenuItem("Excavadora/REPLICAR ESCENARIO 1 Y CONSTRUIR ESCENARIO 2 (ZANJA)")]
    public static void DuplicateAndSetup()
    {
        string srcScene = "Assets/_Project/Scenes/Escenario1_Circuito.unity";
        string dstScene = "Assets/_Project/Scenes/Escenario2_Zanja.unity";

        if (!File.Exists(srcScene))
        {
            EditorUtility.DisplayDialog("Error", $"No se encontró la escena origen: {srcScene}", "OK");
            return;
        }

        // 1. Copiar archivo de escena directamente para preservar 100% de la excavadora y componentes
        File.Copy(srcScene, dstScene, true);
        AssetDatabase.Refresh();

        // 2. Abrir la nueva escena duplicada
        var scene = EditorSceneManager.OpenScene(dstScene, OpenSceneMode.Single);

        // 3. Material de Tierra
        Material dirtMat = CreateDirtMaterial();

        // 4. Configurar el suelo de tierra (Plane)
        GameObject plane = GameObject.Find("Plane");
        if (plane != null)
        {
            plane.name = "DirtGroundPlane";
            plane.transform.localScale = new Vector3(8f, 1f, 8f); // 80m x 80m
            var rend = plane.GetComponent<Renderer>();
            if (rend != null) rend.material = dirtMat;
        }

        // 5. Reorganizar barreras existentes en un cuadrado cerrado de perimetro
        RearrangeBarriersIntoSquarePerimeter();

        // 6. Configurar la ParkZone (Zona de Estacionamiento)
        SetupParkZone();

        // 7. Configurar la Zona Verde de Zanja y Terreno Deformable
        SetupTrenchArea(dirtMat);

        // 8. Configurar BucketPayloadController en la Excavadora
        SetupExcavatorPayload(dirtMat);

        // 9. Guardar escena
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("Escenario 2 — Zanja Creado",
            "Se duplicó correctamente el 'Escenario1_Circuito' en 'Escenario2_Zanja'.\n\n" +
            "  ✓ Excavadora 100% idéntica (controles, cámara, motor, sonido, arm)\n" +
            "  ✓ Recinto de barreras en cuadrado cerrado\n" +
            "  ✓ Suelo con textura de tierra\n" +
            "  ✓ Zona verde de excavación con terreno deformable\n" +
            "  ✓ Carga de pala y montículos 3D al volcar\n" +
            "  ✓ ParkZone e Informe PDF (Tecla P)\n\n" +
            "Presione PLAY para iniciar la prueba.", "OK");
    }

    private static Material CreateDirtMaterial()
    {
        return Excavator.Reporting.RealisticDirtTextureGenerator.GetOrCreateRealisticDirtMaterial();
    }

    private static Material CreateGreenZoneMaterial()
    {
        const string matPath = "Assets/_Project/Materials/GreenZoneMarkerMat.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            if (!Directory.Exists("Assets/_Project/Materials"))
                Directory.CreateDirectory("Assets/_Project/Materials");

            mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.name = "GreenZoneMarkerMat";
            mat.color = new Color(0.10f, 0.85f, 0.30f, 0.45f);
            AssetDatabase.CreateAsset(mat, matPath);
        }
        return mat;
    }

    private static void RearrangeBarriersIntoSquarePerimeter()
    {
        var allGOs = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude);
        List<GameObject> barriers = new List<GameObject>();

        foreach (var go in allGOs)
        {
            string nameLower = go.name.ToLower();
            if (nameLower.Contains("barrier") || nameLower.Contains("channelizing"))
            {
                if (go.transform.parent == null || !go.transform.parent.name.Contains("Perimeter"))
                    barriers.Add(go);
            }
        }

        GameObject perimeterGroup = GameObject.Find("PerimeterBarriers");
        if (perimeterGroup == null) perimeterGroup = new GameObject("PerimeterBarriers");

        float sizeX = 36f;
        float sizeZ = 32f;
        float halfX = sizeX * 0.5f;
        float halfZ = sizeZ * 0.5f;

        List<Vector3> positions = new List<Vector3>();
        List<Quaternion> rotations = new List<Quaternion>();

        // Lados Norte y Sur
        for (float x = -halfX; x <= halfX; x += 3.5f)
        {
            positions.Add(new Vector3(x, 0.5f, halfZ));
            rotations.Add(Quaternion.identity);

            positions.Add(new Vector3(x, 0.5f, -halfZ));
            rotations.Add(Quaternion.identity);
        }

        // Lados Este y Oeste
        for (float z = -halfZ; z <= halfZ; z += 3.5f)
        {
            positions.Add(new Vector3(halfX, 0.5f, z));
            rotations.Add(Quaternion.Euler(0f, 90f, 0f));

            positions.Add(new Vector3(-halfX, 0.5f, z));
            rotations.Add(Quaternion.Euler(0f, 90f, 0f));
        }

        // Reubicar barreras existentes
        for (int i = 0; i < barriers.Count && i < positions.Count; i++)
        {
            var b = barriers[i];
            b.transform.SetParent(perimeterGroup.transform, true);
            b.transform.position = positions[i];
            b.transform.rotation = rotations[i];
        }

        // Si faltan barreras para completar el perimetro cuadrado, duplicar existentes
        if (barriers.Count > 0 && barriers.Count < positions.Count)
        {
            for (int i = barriers.Count; i < positions.Count; i++)
            {
                var clone = Object.Instantiate(barriers[i % barriers.Count], perimeterGroup.transform);
                clone.name = "Block_Barrier_Square";
                clone.transform.position = positions[i];
                clone.transform.rotation = rotations[i];
            }
        }
    }

    private static void SetupParkZone()
    {
        GameObject parkZoneGo = GameObject.Find("ParkZone");
        if (parkZoneGo == null)
        {
            parkZoneGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            parkZoneGo.name = "ParkZone";
        }

        parkZoneGo.transform.position = new Vector3(-12f, 0.05f, -10f);
        parkZoneGo.transform.localScale = new Vector3(6f, 0.1f, 6f);
        parkZoneGo.GetComponent<Renderer>().material = CreateGreenZoneMaterial();

        var parkBox = parkZoneGo.GetComponent<BoxCollider>();
        if (parkBox == null) parkBox = parkZoneGo.AddComponent<BoxCollider>();
        parkBox.isTrigger = true;
        parkBox.size = new Vector3(1f, 4f, 1f);

        if (parkZoneGo.GetComponent<ParkingZone>() == null)
            parkZoneGo.AddComponent<ParkingZone>();
    }

    private static void SetupTrenchArea(Material dirtMat)
    {
        GameObject trenchGroup = GameObject.Find("TrenchExcavationArea");
        if (trenchGroup == null)
        {
            trenchGroup = new GameObject("TrenchExcavationArea");
        }
        trenchGroup.transform.position = new Vector3(0f, 0.01f, 2f);

        // Indicador Borde Verde
        Transform greenBorder = trenchGroup.transform.Find("GreenTrenchBorder");
        if (greenBorder == null)
        {
            GameObject borderGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            borderGo.name = "GreenTrenchBorder";
            borderGo.transform.SetParent(trenchGroup.transform, false);
            borderGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            borderGo.transform.localScale = new Vector3(6.4f, 3.4f, 1f);
            borderGo.GetComponent<Renderer>().material = CreateGreenZoneMaterial();
            Object.DestroyImmediate(borderGo.GetComponent<Collider>());
        }

        // Malla Deformable
        Transform deformableTrans = trenchGroup.transform.Find("DeformableTrenchMesh");
        if (deformableTrans == null)
        {
            GameObject deformableGo = new GameObject("DeformableTrenchMesh");
            deformableGo.transform.SetParent(trenchGroup.transform, false);
            deformableGo.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            deformableGo.AddComponent<DeformableTrenchArea>();
            deformableGo.GetComponent<Renderer>().material = dirtMat;
        }
    }

    private static void SetupExcavatorPayload(Material dirtMat)
    {
        var allGOs = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude);
        GameObject excavator = null;

        foreach (var go in allGOs)
        {
            if (go.transform.parent == null && go.name == "Excavator")
            {
                excavator = go;
                break;
            }
        }

        if (excavator == null)
        {
            foreach (var go in allGOs)
            {
                if (go.transform.parent == null && go.name.ToLower().Contains("excavat") && go.transform.childCount > 1)
                {
                    excavator = go;
                    break;
                }
            }
        }

        if (excavator != null)
        {
            excavator.transform.position = new Vector3(-8f, 0.05f, -2f);
            excavator.transform.rotation = Quaternion.Euler(0f, 90f, 0f);

            var bucketPayload = excavator.GetComponent<BucketPayloadController>();
            if (bucketPayload == null) bucketPayload = excavator.AddComponent<BucketPayloadController>();
            bucketPayload.dirtMaterial = dirtMat;

            var penalty = excavator.GetComponent<PenaltyTracker>();
            if (penalty == null) excavator.AddComponent<PenaltyTracker>();

            var reportMgr = excavator.GetComponent<SimulatorReportManager>();
            if (reportMgr == null) reportMgr = excavator.AddComponent<SimulatorReportManager>();
        }
        else
        {
            Debug.LogError("[Escenario2] No se encontró el GameObject Excavator clonado de Escenario 1.");
        }
    }
}
