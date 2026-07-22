using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Excavator.Engine;

/// <summary>
/// Script de Editor para construir automáticamente toda la escena 'Escenario2_Zanja.unity'.
/// Crea el recinto de barreras, suelo de tierra, zona verde de excavación con terreno deformable,
/// ParkZone y excavadora con todos sus sistemas de simulación e informe.
/// </summary>
public static class BuildEscenarioZanja
{
    [MenuItem("Excavadora/CONSTRUIR: Escenario 2 — Zanja")]
    public static void BuildScene()
    {
        bool ok = EditorUtility.DisplayDialog(
            "Construir Escenario 2 — Zanja",
            "Esto va a construir/reemplazar el 'Escenario2_Zanja.unity' con:\n\n" +
            "  - Recinto cuadrado cerrado de barreras de seguridad\n" +
            "  - Suelo completo de tierra industrial\n" +
            "  - Zona verde delimitada con terreno deformable (Zanja)\n" +
            "  - ParkZone en un costado para estacionamiento\n" +
            "  - Excavadora completa con física de pala y reporte PDF\n\n" +
            "¿Desea continuar?",
            "Sí, construir", "Cancelar");

        if (!ok) return;

        const string scenePath = "Assets/_Project/Scenes/Escenario2_Zanja.unity";
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ----------------------------------------------------------------
        // 1. ILUMINACIÓN Y AMBIENTE
        // ----------------------------------------------------------------
        GameObject lightGo = new GameObject("Directional Light");
        Light lightComp = lightGo.AddComponent<Light>();
        lightComp.type = LightType.Directional;
        lightComp.intensity = 1.25f;
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // ----------------------------------------------------------------
        // 2. SUELO PRINCIPAL DE TIERRA
        // ----------------------------------------------------------------
        GameObject groundPlane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        groundPlane.name = "DirtGroundPlane";
        groundPlane.transform.position = Vector3.zero;
        groundPlane.transform.localScale = new Vector3(8f, 1f, 8f); // 80m x 80m

        // Material de Tierra
        Material dirtMat = CreateDirtMaterial();
        groundPlane.GetComponent<Renderer>().material = dirtMat;

        // Configurar Collider de Suelo
        var groundBox = groundPlane.AddComponent<BoxCollider>();
        groundBox.size = new Vector3(10f, 1f, 10f);
        groundBox.center = new Vector3(0f, -0.5f, 0f);
        var oldMeshCol = groundPlane.GetComponent<MeshCollider>();
        if (oldMeshCol != null) Object.DestroyImmediate(oldMeshCol);

        // ----------------------------------------------------------------
        // 3. RECINTO CUADRADO CERRADO DE BARRERAS
        // ----------------------------------------------------------------
        GameObject barrierGroup = new GameObject("PerimeterBarriers");
        BuildPerimeterBarriers(barrierGroup);

        // ----------------------------------------------------------------
        // 4. ZONA VERDE DE EXCAVACIÓN DE ZANJA
        // ----------------------------------------------------------------
        GameObject trenchGroup = new GameObject("TrenchExcavationArea");
        trenchGroup.transform.position = new Vector3(0f, 0.01f, 2f);

        // Indicador Visual Verde
        GameObject greenBorder = GameObject.CreatePrimitive(PrimitiveType.Quad);
        greenBorder.name = "GreenTrenchBorder";
        greenBorder.transform.SetParent(trenchGroup.transform, false);
        greenBorder.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        greenBorder.transform.localScale = new Vector3(6.4f, 3.4f, 1f);
        greenBorder.GetComponent<Renderer>().material = CreateGreenZoneMaterial();
        Object.DestroyImmediate(greenBorder.GetComponent<Collider>());

        // Malla Deformable
        GameObject deformableGo = new GameObject("DeformableTrenchMesh");
        deformableGo.transform.SetParent(trenchGroup.transform, false);
        deformableGo.transform.localPosition = new Vector3(0f, 0.02f, 0f);

        var deformable = deformableGo.AddComponent<DeformableTrenchArea>();
        deformableGo.GetComponent<Renderer>().material = dirtMat;

        // ----------------------------------------------------------------
        // 5. PARKZONE (ZONA VERDE DE ESTACIONAMIENTO)
        // ----------------------------------------------------------------
        GameObject parkZoneGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        parkZoneGo.name = "ParkZone";
        parkZoneGo.transform.position = new Vector3(-12f, 0.05f, -10f);
        parkZoneGo.transform.localScale = new Vector3(6f, 0.1f, 6f);
        parkZoneGo.GetComponent<Renderer>().material = CreateGreenZoneMaterial();

        var parkBox = parkZoneGo.GetComponent<BoxCollider>();
        parkBox.isTrigger = true;
        parkBox.size = new Vector3(1f, 4f, 1f);

        var parkingZoneComp = parkZoneGo.AddComponent<ParkingZone>();

        // ----------------------------------------------------------------
        // 6. EXCAVADORA Y COMPONENTES
        // ----------------------------------------------------------------
        GameObject excavator = SetupExcavator(new Vector3(-8f, 0.05f, -2f), dirtMat);

        // ----------------------------------------------------------------
        // 7. GUARDAR ESCENA
        // ----------------------------------------------------------------
        EditorSceneManager.SaveScene(scene, scenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Escenario 2 Creado",
            $"Se creó exitosamente el 'Escenario2_Zanja.unity'.\n\n" +
            "  ✓ Recinto cerrado de barreras\n" +
            "  ✓ Suelo completo de tierra industrial\n" +
            "  ✓ Zona verde de excavación con terreno deformable\n" +
            "  ✓ Carga de cuchara y generación de montículos 3D\n" +
            "  ✓ ParkZone e informe de evaluación en PDF (Tecla P)\n\n" +
            "Presione PLAY para probar el ejercicio.", "OK");
    }

    private static Material CreateDirtMaterial()
    {
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        mat.name = "DirtIndustrialMat";
        mat.color = new Color(0.36f, 0.25f, 0.16f); // Color tierra marrón oscuro
        return mat;
    }

    private static Material CreateGreenZoneMaterial()
    {
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        mat.name = "GreenZoneMarkerMat";
        mat.color = new Color(0.10f, 0.85f, 0.30f, 0.45f);
        return mat;
    }

    private static void BuildPerimeterBarriers(GameObject parent)
    {
        float sizeX = 36f;
        float sizeZ = 32f;
        float halfX = sizeX * 0.5f;
        float halfZ = sizeZ * 0.5f;

        // Crear perimetro cuadrado usando primitivas de barrera rigida
        for (float x = -halfX; x <= halfX; x += 3.5f)
        {
            CreateBarrierPrimitive(parent, new Vector3(x, 0.5f, halfZ), Quaternion.identity);
            CreateBarrierPrimitive(parent, new Vector3(x, 0.5f, -halfZ), Quaternion.identity);
        }

        for (float z = -halfZ; z <= halfZ; z += 3.5f)
        {
            CreateBarrierPrimitive(parent, new Vector3(halfX, 0.5f, z), Quaternion.Euler(0f, 90f, 0f));
            CreateBarrierPrimitive(parent, new Vector3(-halfX, 0.5f, z), Quaternion.Euler(0f, 90f, 0f));
        }
    }

    private static void CreateBarrierPrimitive(GameObject parent, Vector3 pos, Quaternion rot)
    {
        GameObject b = GameObject.CreatePrimitive(PrimitiveType.Cube);
        b.name = "Block_Barrier_Safety";
        b.transform.SetParent(parent.transform, false);
        b.transform.position = pos;
        b.transform.rotation = rot;
        b.transform.localScale = new Vector3(3.2f, 1.1f, 0.8f);

        Material bMat = new Material(Shader.Find("Standard"));
        bMat.color = new Color(0.85f, 0.20f, 0.10f); // Rojo barrera
        b.GetComponent<Renderer>().material = bMat;

        var rb = b.AddComponent<Rigidbody>();
        rb.mass = 2500f;
        rb.isKinematic = true;
    }

    private static GameObject SetupExcavator(Vector3 spawnPos, Material dirtMat)
    {
        // Buscar prefab de Excavator o cargar de la escena 1
        GameObject excavator = null;

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Excavator.prefab");
        if (prefab != null)
        {
            excavator = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        }
        else
        {
            excavator = new GameObject("Excavator");
        }

        excavator.name = "Excavator";
        excavator.transform.position = spawnPos;

        // Rigidbody
        var rb = GetOrAdd<Rigidbody>(excavator);
        rb.mass = 18000f;
        rb.linearDamping = 5f;
        rb.angularDamping = 10f;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        // ScriptableObject EngineConfig
        const string configPath = "Assets/_Project/EngineConfig.asset";
        var engineConfig = AssetDatabase.LoadAssetAtPath<EngineConfig>(configPath);

        var kbi = GetOrAdd<KeyboardIgnitionInput>(excavator);
        var ec = GetOrAdd<EngineController>(excavator);
        var em = GetOrAdd<ExcavatorMovement>(excavator);
        var arm = GetOrAdd<ExcavatorArm>(excavator);
        var hud = GetOrAdd<EngineHUD>(excavator);
        GetOrAdd<AudioSource>(excavator);
        var pes = GetOrAdd<ProceduralEngineSound>(excavator);

        // Sistema de Penalizaciones e Informe
        var penalty = GetOrAdd<PenaltyTracker>(excavator);
        var reportMgr = GetOrAdd<SimulatorReportManager>(excavator);

        // Controlador de Carga de Cuchara
        var bucketPayload = GetOrAdd<BucketPayloadController>(excavator);
        bucketPayload.dirtMaterial = dirtMat;

        reportMgr.penaltyTracker = penalty;
        reportMgr.engineController = ec;

        return excavator;
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        if (c == null) c = Undo.AddComponent<T>(go);
        return c;
    }
}
