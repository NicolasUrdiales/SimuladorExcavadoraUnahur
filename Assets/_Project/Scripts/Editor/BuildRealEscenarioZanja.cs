using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Excavator.Engine;

/// <summary>
/// Reconstruccion total de Escenario 2 — Zanja coincidente con los requerimientos industriales
/// e imagen de referencia del simulador: recinto cerrado con barreras rojas/blancas, suelo de tierra realista,
/// ranura de excavacion verde delimitada frente a la pala y ParkZone clara de estacionamiento.
/// </summary>
public static class BuildRealEscenarioZanja
{
    [MenuItem("Excavadora/RECONSTRUIR ESCENARIO 2 (ZANJA REALISTA)")]
    public static void RebuildScene()
    {
        string srcScene = "Assets/_Project/Scenes/Escenario1_Circuito.unity";
        string dstScene = "Assets/_Project/Scenes/Escenario2_Zanja.unity";

        if (!File.Exists(srcScene))
        {
            EditorUtility.DisplayDialog("Error", $"No se encontró la escena original {srcScene}", "OK");
            return;
        }

        // 1. Duplicar la escena de Escenario 1 para conservar 100% de la excavadora y componentes
        File.Copy(srcScene, dstScene, true);
        AssetDatabase.Refresh();

        var scene = EditorSceneManager.OpenScene(dstScene, OpenSceneMode.Single);

        // 2. Generar y obtener Material de Tierra Realista con mapa de normales PNG
        Material dirtMat = CreateRealisticDirtTexture.GenerateAndGetDirtMaterial();

        // 3. Limpiar objetos de edificios o barreras desordenadas del Escenario 1
        CleanUpUnwantedSceneObjects();

        // 4. Configurar el Suelo de Tierra Realista (Plane)
        GameObject plane = GameObject.Find("Plane");
        if (plane == null)
        {
            plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "Plane";
        }
        plane.name = "DirtGroundPlane";
        plane.transform.position = Vector3.zero;
        plane.transform.localScale = new Vector3(6f, 1f, 6f); // 60m x 60m
        var rend = plane.GetComponent<Renderer>();
        if (rend != null) rend.material = dirtMat;

        var boxCol = plane.GetComponent<BoxCollider>();
        if (boxCol == null) boxCol = plane.AddComponent<BoxCollider>();
        boxCol.size = new Vector3(10f, 1f, 10f);
        boxCol.center = new Vector3(0f, -0.5f, 0f);

        // 5. Construir Perímetro Cuadrado Cerrado de Barreras Industriales (NO edificios de ciudad)
        GameObject perimeterGroup = new GameObject("PerimeterBarriers");
        BuildCleanSafetyPerimeter(perimeterGroup);

        // 6. Configurar la Zona Verde de Zanja (Ranura Angosta Verde frente a la pala)
        SetupPreciseTrenchSlot(dirtMat);

        // 7. Configurar la ParkZone (Zona de Estacionamiento Clara)
        SetupClearParkZone();

        // 8. Posicionar y Configurar la Excavadora frente a la zanja
        SetupExcavatorPositionAndPayload(dirtMat);

        // 9. Guardar escena
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("Escenario 2 — Zanja Reconstruido",
            "Se reconstruyó el 'Escenario2_Zanja.unity' con calidad industrial:\n\n" +
            "  ✓ Suelo con textura de tierra realista y mapa de normales 3D\n" +
            "  ✓ Perímetro cerrado con barreras de seguridad rojas/blancas\n" +
            "  ✓ Ranura delimitada verde angosta frente a la excavadora para la zanja\n" +
            "  ✓ ParkZone clara de estacionamiento a un costado\n" +
            "  ✓ Excavadora 100% idéntica con física de excavación, carga y montículos\n" +
            "  ✓ Sistema de penalidades e informe PDF (Tecla P)\n\n" +
            "Presione PLAY para probar la simulación.", "OK");
    }

    private static void CleanUpUnwantedSceneObjects()
    {
        var allGOs = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
        foreach (var go in allGOs)
        {
            if (go.transform.parent != null) continue;
            string nameLower = go.name.ToLower();

            // Eliminar objetos de barreras/edificios viejos del circuito para crear el perimetro limpio
            if (nameLower.Contains("block_barrier") || nameLower.Contains("pedestrian_barrier") ||
                nameLower.Contains("channelizing") || nameLower.Contains("building") ||
                nameLower.Contains("perimeter") || nameLower.Contains("trench"))
            {
                Object.DestroyImmediate(go);
            }
        }
    }

    private static void BuildCleanSafetyPerimeter(GameObject parent)
    {
        float sizeX = 34f;
        float sizeZ = 28f;
        float halfX = sizeX * 0.5f;
        float halfZ = sizeZ * 0.5f;

        Material barrierMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        barrierMat.name = "SafetyBarrierRedWhiteMat";
        barrierMat.color = new Color(0.85f, 0.18f, 0.12f); // Rojo barrera industrial

        // Lados Norte y Sur
        for (float x = -halfX; x <= halfX; x += 3.2f)
        {
            CreateSingleBarrierBlock(parent, new Vector3(x, 0.55f, halfZ), Quaternion.identity, barrierMat);
            CreateSingleBarrierBlock(parent, new Vector3(x, 0.55f, -halfZ), Quaternion.identity, barrierMat);
        }

        // Lados Este y Oeste
        for (float z = -halfZ; z <= halfZ; z += 3.2f)
        {
            CreateSingleBarrierBlock(parent, new Vector3(halfX, 0.55f, z), Quaternion.Euler(0f, 90f, 0f), barrierMat);
            CreateSingleBarrierBlock(parent, new Vector3(-halfX, 0.55f, z), Quaternion.Euler(0f, 90f, 0f), barrierMat);
        }
    }

    private static void CreateSingleBarrierBlock(GameObject parent, Vector3 pos, Quaternion rot, Material mat)
    {
        GameObject b = GameObject.CreatePrimitive(PrimitiveType.Cube);
        b.name = "Block_Barrier_Safety";
        b.transform.SetParent(parent.transform, false);
        b.transform.position = pos;
        b.transform.rotation = rot;
        b.transform.localScale = new Vector3(3.0f, 1.1f, 0.7f);
        b.GetComponent<Renderer>().material = mat;

        var rb = b.GetComponent<Rigidbody>();
        if (rb == null) rb = b.AddComponent<Rigidbody>();
        rb.mass = 2500f;
        rb.isKinematic = true;
    }

    private static void SetupPreciseTrenchSlot(Material dirtMat)
    {
        GameObject trenchGroup = new GameObject("TrenchExcavationArea");
        trenchGroup.transform.position = new Vector3(0f, 0.01f, 2.5f);

        // Marco Delimitador Verde Angosto (Exactamente como en la imagen de referencia)
        GameObject borderFrame = new GameObject("GreenTrenchOutline");
        borderFrame.transform.SetParent(trenchGroup.transform, false);

        // Dimensiones de la ranura de zanja: 1.8m ancho x 5.0m largo
        float w = 1.8f;
        float l = 5.0f;
        float t = 0.08f; // Grosor de la linea verde

        Material greenLineMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        greenLineMat.color = new Color(0.10f, 0.90f, 0.25f, 0.85f);

        // 4 Bordes del marco verde
        CreateLineSegment(borderFrame, new Vector3(0f, 0.01f, l * 0.5f), new Vector3(w, 0.02f, t), greenLineMat);
        CreateLineSegment(borderFrame, new Vector3(0f, 0.01f, -l * 0.5f), new Vector3(w, 0.02f, t), greenLineMat);
        CreateLineSegment(borderFrame, new Vector3(-w * 0.5f, 0.01f, 0f), new Vector3(t, 0.02f, l), greenLineMat);
        CreateLineSegment(borderFrame, new Vector3(w * 0.5f, 0.01f, 0f), new Vector3(t, 0.02f, l), greenLineMat);

        // Malla Deformable de Zanja
        GameObject deformableGo = new GameObject("DeformableTrenchMesh");
        deformableGo.transform.SetParent(trenchGroup.transform, false);
        deformableGo.transform.localPosition = new Vector3(0f, 0.02f, 0f);

        var deformable = deformableGo.AddComponent<DeformableTrenchArea>();
        deformable.width = w;
        deformable.length = l;
        deformable.maxDepth = 1.2f;
        deformable.dirtMaterial = dirtMat;

        var rend = deformableGo.GetComponent<Renderer>();
        if (rend != null) rend.material = dirtMat;
    }

    private static void CreateLineSegment(GameObject parent, Vector3 localPos, Vector3 scale, Material mat)
    {
        GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cube);
        line.name = "GreenLine";
        line.transform.SetParent(parent.transform, false);
        line.transform.localPosition = localPos;
        line.transform.localScale = scale;
        line.GetComponent<Renderer>().material = mat;
        Object.DestroyImmediate(line.GetComponent<Collider>());
    }

    private static void SetupClearParkZone()
    {
        GameObject parkZoneGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        parkZoneGo.name = "ParkZone";
        parkZoneGo.transform.position = new Vector3(-10f, 0.05f, -6f);
        parkZoneGo.transform.localScale = new Vector3(5.5f, 0.08f, 5.5f);

        Material greenParkMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        greenParkMat.color = new Color(0.10f, 0.85f, 0.30f, 0.50f);
        parkZoneGo.GetComponent<Renderer>().material = greenParkMat;

        var parkBox = parkZoneGo.GetComponent<BoxCollider>();
        if (parkBox == null) parkBox = parkZoneGo.AddComponent<BoxCollider>();
        parkBox.isTrigger = true;
        parkBox.size = new Vector3(1f, 4f, 1f);

        if (parkZoneGo.GetComponent<ParkingZone>() == null)
            parkZoneGo.AddComponent<ParkingZone>();
    }

    private static void SetupExcavatorPositionAndPayload(Material dirtMat)
    {
        var allGOs = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
        GameObject excavator = null;

        foreach (var go in allGOs)
        {
            if (go.transform.parent == null && go.name == "Excavator")
            {
                excavator = go;
                break;
            }
        }

        if (excavator != null)
        {
            // Posicionar la excavadora mirando de frente a la ranura de excavacion (X=0, Z=-3.5m)
            excavator.transform.position = new Vector3(0f, 0.05f, -3.5f);
            excavator.transform.rotation = Quaternion.Euler(0f, 0f, 0f);

            var bucketPayload = excavator.GetComponent<BucketPayloadController>();
            if (bucketPayload == null) bucketPayload = excavator.AddComponent<BucketPayloadController>();
            bucketPayload.dirtMaterial = dirtMat;

            var penalty = excavator.GetComponent<PenaltyTracker>();
            if (penalty == null) excavator.AddComponent<PenaltyTracker>();

            var reportMgr = excavator.GetComponent<SimulatorReportManager>();
            if (reportMgr == null) reportMgr = excavator.AddComponent<SimulatorReportManager>();

            reportMgr.penaltyTracker = penalty;
            reportMgr.engineController = excavator.GetComponent<EngineController>();
            reportMgr.parkingZone = Object.FindAnyObjectByType<ParkingZone>();
        }
        else
        {
            Debug.LogError("[Escenario2] No se encontró el GameObject Excavator duplicado de Escenario 1.");
        }
    }
}
