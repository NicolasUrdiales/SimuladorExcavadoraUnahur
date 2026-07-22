using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using Excavator.Engine;
using Excavator.Trench;

/// <summary>
/// Script de Editor para construir la escena 'ObraExcavadora.unity' (FASE 3: EJERCICIO DE EXCAVACIÓN DE ZANJA).
/// Integra el sistema desacoplado de evaluacion de zanja (TrenchExerciseConfig, TrenchExcavationTracker,
/// TrenchExerciseEvaluator, TrenchExerciseHUD e ITrenchTerrainSource).
/// </summary>
public static class BuildObraExcavadora
{
    private const string SCENE_PATH = "Assets/_Project/Scenes/ObraExcavadora.unity";

    // Rutas de Prefabs Existentes
    private const string PREFAB_BUILDING_1 = "Assets/Mellow Fox studios/Versatile Building Kit - 15 Medium Poly Models for Game Development/Prefabs/Building 1.prefab";
    private const string PREFAB_BUILDING_3 = "Assets/Mellow Fox studios/Versatile Building Kit - 15 Medium Poly Models for Game Development/Prefabs/Building 3.prefab";
    private const string PREFAB_BUILDING_5 = "Assets/Mellow Fox studios/Versatile Building Kit - 15 Medium Poly Models for Game Development/Prefabs/Building 5.prefab";

    private const string PREFAB_BARRIER_RED = "Assets/Prototype Collection/URP/Prefabs/Blocks/Block_Barrier_1_RedStripes.prefab";
    private const string PREFAB_BARRIER_YELLOW = "Assets/Prototype Collection/URP/Prefabs/Blocks/Block_Barrier_1_YellowStripes.prefab";
    private const string PREFAB_CHANNELIZER = "Assets/Prototype Collection/URP/Prefabs/Channelizing/Channelizing_Clean.prefab";
    private const string PREFAB_CONE = "Assets/Prototype Collection/URP/Prefabs/Cone/Cone_Clean.prefab";
    private const string PREFAB_TRAFFIC_LIGHT = "Assets/Prototype Collection/URP/Prefabs/Barrel Light/Traffic_Light_Clean.prefab";

    private const string PREFAB_PLASTIC_RED = "Assets/VintProg_OldPlastic_Barrier_04/Prefabs/Plastic_Barrier04_Red.prefab";
    private const string PREFAB_PLASTIC_WHITE = "Assets/VintProg_OldPlastic_Barrier_04/Prefabs/Plastic_Barrier04_White.prefab";

    [MenuItem("Excavadora/CONSTRUIR: Escenario ObraExcavadora (Fase 3)")]
    public static void BuildScene()
    {
        // 1. Crear nueva escena limpia
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 2. Materiales de Superficie y Terreno
        Material dirtMat = GetOrCreateDirtMaterial();
        Material greenZoneMat = GetOrCreateGreenZoneMaterial();
        Material asphaltMat = GetOrCreateAsphaltMaterial();
        Material concreteMat = GetOrCreateConcreteMaterial();
        Material gravelMat = GetOrCreateGravelMaterial();

        // ----------------------------------------------------------------
        // A. LIGHTING & URP VOLUME
        // ----------------------------------------------------------------
        GameObject lightingRoot = new GameObject("Lighting");

        GameObject sunLight = new GameObject("Directional Light");
        sunLight.transform.SetParent(lightingRoot.transform, false);
        Light lightComp = sunLight.AddComponent<Light>();
        lightComp.type = LightType.Directional;
        lightComp.intensity = 1.35f;
        lightComp.color = new Color(1.0f, 0.96f, 0.88f);
        lightComp.shadows = LightShadows.Soft;
        sunLight.transform.rotation = Quaternion.Euler(52f, -35f, 0f);

        GameObject globalVolume = new GameObject("Global Volume");
        globalVolume.transform.SetParent(lightingRoot.transform, false);
        var volume = globalVolume.AddComponent<UnityEngine.Rendering.Volume>();
        volume.isGlobal = true;

        // ----------------------------------------------------------------
        // B. ENVIRONMENT
        // ----------------------------------------------------------------
        GameObject envRoot = new GameObject("Environment");

        // B.1 Terreno Principal de Tierra
        GameObject terrainGo = GameObject.CreatePrimitive(PrimitiveType.Plane);
        terrainGo.name = "Terrain";
        terrainGo.transform.SetParent(envRoot.transform, false);
        terrainGo.transform.position = Vector3.zero;
        terrainGo.transform.localScale = new Vector3(10f, 1f, 10f); // 100m x 100m
        terrainGo.GetComponent<Renderer>().material = dirtMat;

        var terrainBox = terrainGo.AddComponent<BoxCollider>();
        terrainBox.size = new Vector3(10f, 1f, 10f);
        terrainBox.center = new Vector3(0f, -0.5f, 0f);
        var oldMeshCol = terrainGo.GetComponent<MeshCollider>();
        if (oldMeshCol != null) Object.DestroyImmediate(oldMeshCol);

        // B.2 Zonas y Plataformas de Construccion
        GameObject constArea = new GameObject("ConstructionArea");
        constArea.transform.SetParent(envRoot.transform, false);

        GameObject mainPad = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mainPad.name = "ConcreteFoundationPad";
        mainPad.transform.SetParent(constArea.transform, false);
        mainPad.transform.position = new Vector3(15f, 0.01f, 5f);
        mainPad.transform.localScale = new Vector3(40f, 0.05f, 30f);
        mainPad.GetComponent<Renderer>().material = concreteMat;

        GameObject gravelPad = GameObject.CreatePrimitive(PrimitiveType.Cube);
        gravelPad.name = "GravelStoragePad";
        gravelPad.transform.SetParent(constArea.transform, false);
        gravelPad.transform.position = new Vector3(-20f, 0.012f, 10f);
        gravelPad.transform.localScale = new Vector3(25f, 0.04f, 20f);
        gravelPad.GetComponent<Renderer>().material = gravelMat;

        // B.3 Zonas Verdes y Césped Perimetral
        GameObject greenAreas = new GameObject("GreenAreas");
        greenAreas.transform.SetParent(envRoot.transform, false);

        BuildGreenZoneStrip(greenAreas, "GreenZone_North", new Vector3(0f, 0.02f, 42f), new Vector3(90f, 14f, 1f), greenZoneMat);
        BuildGreenZoneStrip(greenAreas, "GreenZone_East", new Vector3(42f, 0.02f, 0f), new Vector3(14f, 90f, 1f), greenZoneMat);
        BuildGreenZoneStrip(greenAreas, "GreenZone_West", new Vector3(-42f, 0.02f, -10f), new Vector3(14f, 70f, 1f), greenZoneMat);

        // B.4 Red Vial y Caminos de Circulación
        GameObject roads = new GameObject("Roads");
        roads.transform.SetParent(envRoot.transform, false);

        GameObject mainRoad = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mainRoad.name = "MainAsphaltAccessRoad";
        mainRoad.transform.SetParent(roads.transform, false);
        mainRoad.transform.position = new Vector3(-15f, 0.015f, -10f);
        mainRoad.transform.localScale = new Vector3(8f, 0.04f, 60f);
        mainRoad.GetComponent<Renderer>().material = asphaltMat;

        GameObject dirtRoad = GameObject.CreatePrimitive(PrimitiveType.Cube);
        dirtRoad.name = "SecondaryDirtTrack";
        dirtRoad.transform.SetParent(roads.transform, false);
        dirtRoad.transform.position = new Vector3(-3f, 0.014f, 8f);
        dirtRoad.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
        dirtRoad.transform.localScale = new Vector3(7f, 0.03f, 35f);
        dirtRoad.GetComponent<Renderer>().material = gravelMat;

        // B.5 Estructuras de Obra y Objetos Modulares
        GameObject structures = new GameObject("ConstructionStructures");
        structures.transform.SetParent(envRoot.transform, false);

        SpawnStructurePrefab(PREFAB_BUILDING_1, structures, new Vector3(22f, 0f, 12f), Quaternion.identity, Vector3.one * 0.9f, "MainTower_Structure");
        SpawnStructurePrefab(PREFAB_BUILDING_3, structures, new Vector3(28f, 0f, -8f), Quaternion.Euler(0f, -90f, 0f), Vector3.one * 0.95f, "OfficeBuilding_Structure");
        SpawnStructurePrefab(PREFAB_BUILDING_5, structures, new Vector3(-24f, 0f, 18f), Quaternion.Euler(0f, 45f, 0f), Vector3.one * 0.85f, "Warehouse_Structure");

        BuildScaffoldingGroup(structures, new Vector3(14f, 0f, 12f));
        BuildConcretePiles(structures, new Vector3(-16f, 0f, 14f));

        BuildDirtMoundsGroup(envRoot, new Vector3(-8f, 0f, 18f));

        BuildSiteSafetyPerimeter(structures);

        BuildRoadConesAndSignage(roads);

        // ----------------------------------------------------------------
        // C. FUNCTIONAL ZONES
        // ----------------------------------------------------------------
        GameObject functionalZones = new GameObject("FunctionalZones");

        // C.1 ExcavationZone (Zona Verde de Zanja con Malla Deformable y Sistema de Evaluación FASE 3)
        GameObject excavationZone = new GameObject("ExcavationZone");
        excavationZone.transform.SetParent(functionalZones.transform, false);
        excavationZone.transform.position = new Vector3(0f, 0.01f, 8f);

        GameObject greenTrenchMarker = GameObject.CreatePrimitive(PrimitiveType.Quad);
        greenTrenchMarker.name = "TrenchBoundaryMarker";
        greenTrenchMarker.transform.SetParent(excavationZone.transform, false);
        greenTrenchMarker.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        greenTrenchMarker.transform.localScale = new Vector3(6.4f, 3.4f, 1f);
        greenTrenchMarker.GetComponent<Renderer>().material = greenZoneMat;
        Object.DestroyImmediate(greenTrenchMarker.GetComponent<Collider>());

        GameObject deformableMeshGo = new GameObject("DeformableTrenchMesh");
        deformableMeshGo.transform.SetParent(excavationZone.transform, false);
        deformableMeshGo.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        var deformableComp = deformableMeshGo.AddComponent<DeformableTrenchArea>();
        deformableMeshGo.GetComponent<Renderer>().material = dirtMat;

        // Modulos del Ejercicio de Excavacion (FASE 3)
        var trenchConfig = excavationZone.AddComponent<TrenchExerciseConfig>();
        trenchConfig.targetLength = 6.0f;
        trenchConfig.targetWidth = 3.0f;
        trenchConfig.targetDepth = 1.20f;
        trenchConfig.toleranceMeters = 0.20f;
        trenchConfig.maxTimeSeconds = 300f;

        var trenchTracker = excavationZone.AddComponent<TrenchExcavationTracker>();
        trenchTracker.terrainSourceComponent = deformableComp;
        trenchTracker.config = trenchConfig;

        var trenchEvaluator = excavationZone.AddComponent<TrenchExerciseEvaluator>();
        trenchEvaluator.config = trenchConfig;
        trenchEvaluator.tracker = trenchTracker;

        var trenchHUD = excavationZone.AddComponent<TrenchExerciseHUD>();
        trenchHUD.config = trenchConfig;
        trenchHUD.tracker = trenchTracker;
        trenchHUD.evaluator = trenchEvaluator;

        // C.2 RestrictedZones
        GameObject restrictedZones = new GameObject("RestrictedZones");
        restrictedZones.transform.SetParent(functionalZones.transform, false);

        GameObject hazardArea = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hazardArea.name = "HazardZone_DangerArea";
        hazardArea.transform.SetParent(restrictedZones.transform, false);
        hazardArea.transform.position = new Vector3(18f, 0.05f, -16f);
        hazardArea.transform.localScale = new Vector3(12f, 0.1f, 12f);
        Material redHazardMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        redHazardMat.color = new Color(0.9f, 0.15f, 0.10f, 0.5f);
        hazardArea.GetComponent<Renderer>().material = redHazardMat;

        SpawnStructurePrefab(PREFAB_TRAFFIC_LIGHT, restrictedZones, new Vector3(12f, 0f, -10f), Quaternion.identity, Vector3.one, "TrafficLight_1");
        SpawnStructurePrefab(PREFAB_TRAFFIC_LIGHT, restrictedZones, new Vector3(24f, 0f, -10f), Quaternion.identity, Vector3.one, "TrafficLight_2");

        // C.3 SoilDumpZone
        GameObject soilDumpZone = new GameObject("SoilDumpZone");
        soilDumpZone.transform.SetParent(functionalZones.transform, false);
        soilDumpZone.transform.position = new Vector3(-8f, 0.02f, 18f);

        GameObject dumpTargetMarker = GameObject.CreatePrimitive(PrimitiveType.Quad);
        dumpTargetMarker.name = "SoilDumpMarker";
        dumpTargetMarker.transform.SetParent(soilDumpZone.transform, false);
        dumpTargetMarker.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        dumpTargetMarker.transform.localScale = new Vector3(7f, 7f, 1f);
        dumpTargetMarker.GetComponent<Renderer>().material = greenZoneMat;
        Object.DestroyImmediate(dumpTargetMarker.GetComponent<Collider>());

        // C.4 GreenParkingZone
        GameObject greenParkingZone = GameObject.CreatePrimitive(PrimitiveType.Cube);
        greenParkingZone.name = "GreenParkingZone";
        greenParkingZone.transform.SetParent(functionalZones.transform, false);
        greenParkingZone.transform.position = new Vector3(-15f, 0.05f, -24f);
        greenParkingZone.transform.localScale = new Vector3(6f, 0.1f, 6f);
        greenParkingZone.GetComponent<Renderer>().material = greenZoneMat;

        var parkBox = greenParkingZone.GetComponent<BoxCollider>();
        parkBox.isTrigger = true;
        parkBox.size = new Vector3(1f, 40f, 1f);
        greenParkingZone.AddComponent<ParkingZone>();

        // ----------------------------------------------------------------
        // D. EXERCISE
        // ----------------------------------------------------------------
        GameObject exerciseRoot = new GameObject("Exercise");

        // ----------------------------------------------------------------
        // E. EXCAVATOR
        // ----------------------------------------------------------------
        GameObject excavatorObj = SetupExcavator(new Vector3(-15f, 0.05f, -15f), dirtMat);
        excavatorObj.transform.rotation = Quaternion.Euler(0f, 0f, 0f);

        // ----------------------------------------------------------------
        // F. CAMERA
        // ----------------------------------------------------------------
        GameObject cameraRoot = new GameObject("Camera");

        // ----------------------------------------------------------------
        // G. GUARDAR ESCENA Y REGISTRAR EN BUILD SETTINGS
        // ----------------------------------------------------------------
        EditorSceneManager.SaveScene(scene, SCENE_PATH);
        AddSceneToBuildSettings(SCENE_PATH);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[BuildObraExcavadora] Escena 'ObraExcavadora.unity' (FASE 3) construida con exito en {SCENE_PATH}");
    }

    // ----------------------------------------------------------------
    //  HELPERS DE CONSTRUCCION MODULAR
    // ----------------------------------------------------------------

    private static void BuildGreenZoneStrip(GameObject parent, string name, Vector3 pos, Vector3 scale, Material mat)
    {
        GameObject strip = GameObject.CreatePrimitive(PrimitiveType.Quad);
        strip.name = name;
        strip.transform.SetParent(parent.transform, false);
        strip.transform.position = pos;
        strip.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        strip.transform.localScale = scale;
        strip.GetComponent<Renderer>().material = mat;
        Object.DestroyImmediate(strip.GetComponent<Collider>());
    }

    private static void SpawnStructurePrefab(string prefabPath, GameObject parent, Vector3 pos, Quaternion rot, Vector3 scale, string name)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab != null)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = name;
            instance.transform.SetParent(parent.transform, false);
            instance.transform.position = pos;
            instance.transform.rotation = rot;
            instance.transform.localScale = scale;
        }
        else
        {
            GameObject placeholder = GameObject.CreatePrimitive(PrimitiveType.Cube);
            placeholder.name = name + "_Placeholder";
            placeholder.transform.SetParent(parent.transform, false);
            placeholder.transform.position = pos;
            placeholder.transform.rotation = rot;
            placeholder.transform.localScale = scale;
        }
    }

    private static void BuildScaffoldingGroup(GameObject parent, Vector3 pos)
    {
        GameObject scafGroup = new GameObject("ScaffoldingGroup");
        scafGroup.transform.SetParent(parent.transform, false);
        scafGroup.transform.position = pos;

        Material steelMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        steelMat.color = new Color(0.45f, 0.48f, 0.52f);

        for (int i = 0; i < 4; i++)
        {
            GameObject col = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            col.name = $"ScaffoldingPillar_{i}";
            col.transform.SetParent(scafGroup.transform, false);
            col.transform.localPosition = new Vector3((i % 2 == 0 ? -1.2f : 1.2f), 2.5f, (i < 2 ? -1.2f : 1.2f));
            col.transform.localScale = new Vector3(0.12f, 2.5f, 0.12f);
            col.GetComponent<Renderer>().material = steelMat;
        }

        GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
        platform.name = "ScaffoldingDeck";
        platform.transform.SetParent(scafGroup.transform, false);
        platform.transform.localPosition = new Vector3(0f, 2.5f, 0f);
        platform.transform.localScale = new Vector3(2.6f, 0.1f, 2.6f);
        platform.GetComponent<Renderer>().material = steelMat;
    }

    private static void BuildConcretePiles(GameObject parent, Vector3 pos)
    {
        GameObject pileGroup = new GameObject("ConcretePipesStorage");
        pileGroup.transform.SetParent(parent.transform, false);
        pileGroup.transform.position = pos;

        Material concreteMat = GetOrCreateConcreteMaterial();

        for (int i = 0; i < 3; i++)
        {
            GameObject pipe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pipe.name = $"ConcretePipe_{i}";
            pipe.transform.SetParent(pileGroup.transform, false);
            pipe.transform.localPosition = new Vector3(i * 1.4f, 0.6f, 0f);
            pipe.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            pipe.transform.localScale = new Vector3(1.2f, 1.8f, 1.2f);
            pipe.GetComponent<Renderer>().material = concreteMat;
        }
    }

    private static void BuildDirtMoundsGroup(GameObject parent, Vector3 pos)
    {
        GameObject moundsGroup = new GameObject("DirtMoundsGroup");
        moundsGroup.transform.SetParent(parent.transform, false);
        moundsGroup.transform.position = pos;

        Vector3[] offsets = new Vector3[]
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(2.2f, 0f, 1.2f),
            new Vector3(-1.8f, 0f, 2.0f)
        };

        foreach (var off in offsets)
        {
            GameObject moundGo = new GameObject("DirtMound_Static");
            moundGo.transform.SetParent(moundsGroup.transform, false);
            moundGo.transform.position = pos + off;

            var mound = moundGo.AddComponent<DirtMound>();
            mound.AddDirtVolume(1.8f);

            var rend = moundGo.GetComponent<Renderer>();
            if (rend != null) rend.material = GetOrCreateDirtMaterial();
        }
    }

    private static void BuildSiteSafetyPerimeter(GameObject parent)
    {
        GameObject perimeterGroup = new GameObject("PerimeterSafetyBarriers");
        perimeterGroup.transform.SetParent(parent.transform, false);

        float sizeX = 44f;
        float sizeZ = 44f;
        float halfX = sizeX * 0.5f;
        float halfZ = sizeZ * 0.5f;

        for (float x = -halfX; x <= halfX; x += 4.2f)
        {
            SpawnStructurePrefab(PREFAB_BARRIER_RED, perimeterGroup, new Vector3(x, 0.5f, halfZ), Quaternion.identity, Vector3.one, "Block_Barrier_Red");
            SpawnStructurePrefab(PREFAB_BARRIER_YELLOW, perimeterGroup, new Vector3(x, 0.5f, -halfZ), Quaternion.identity, Vector3.one, "Block_Barrier_Yellow");
        }

        for (float z = -halfZ; z <= halfZ; z += 3.2f)
        {
            string plasticPrefab = (z % 2 == 0) ? PREFAB_PLASTIC_RED : PREFAB_PLASTIC_WHITE;
            SpawnStructurePrefab(plasticPrefab, perimeterGroup, new Vector3(halfX, 0.4f, z), Quaternion.Euler(0f, 90f, 0f), Vector3.one, "Plastic_Barrier_Fence");
            SpawnStructurePrefab(plasticPrefab, perimeterGroup, new Vector3(-halfX, 0.4f, z), Quaternion.Euler(0f, 90f, 0f), Vector3.one, "Plastic_Barrier_Fence");
        }
    }

    private static void BuildRoadConesAndSignage(GameObject parent)
    {
        GameObject signageGroup = new GameObject("RoadConesAndSignage");
        signageGroup.transform.SetParent(parent.transform, false);

        for (float z = -35f; z <= 15f; z += 5f)
        {
            SpawnStructurePrefab(PREFAB_CONE, signageGroup, new Vector3(-19.2f, 0.2f, z), Quaternion.identity, Vector3.one, "SafetyCone_West");
            SpawnStructurePrefab(PREFAB_CONE, signageGroup, new Vector3(-10.8f, 0.2f, z), Quaternion.identity, Vector3.one, "SafetyCone_East");
        }

        SpawnStructurePrefab(PREFAB_CHANNELIZER, signageGroup, new Vector3(-10.5f, 0.4f, 15f), Quaternion.identity, Vector3.one, "Channelizer_1");
        SpawnStructurePrefab(PREFAB_CHANNELIZER, signageGroup, new Vector3(-10.5f, 0.4f, 20f), Quaternion.identity, Vector3.one, "Channelizer_2");
    }

    private static GameObject SetupExcavator(Vector3 spawnPos, Material dirtMat)
    {
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

        var rb = GetOrAdd<Rigidbody>(excavator);
        rb.mass = 18000f;
        rb.linearDamping = 5f;
        rb.angularDamping = 10f;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        const string configPath = "Assets/_Project/EngineConfig.asset";
        var engineConfig = AssetDatabase.LoadAssetAtPath<EngineConfig>(configPath);

        var kbi = GetOrAdd<KeyboardIgnitionInput>(excavator);
        var ec = GetOrAdd<EngineController>(excavator);
        var em = GetOrAdd<ExcavatorMovement>(excavator);
        var arm = GetOrAdd<ExcavatorArm>(excavator);
        var hud = GetOrAdd<EngineHUD>(excavator);
        GetOrAdd<AudioSource>(excavator);
        var pes = GetOrAdd<ProceduralEngineSound>(excavator);

        var penalty = GetOrAdd<PenaltyTracker>(excavator);
        var reportMgr = GetOrAdd<SimulatorReportManager>(excavator);
        var bucketPayload = GetOrAdd<BucketPayloadController>(excavator);
        bucketPayload.dirtMaterial = dirtMat;

        reportMgr.penaltyTracker = penalty;
        reportMgr.engineController = ec;

        return excavator;
    }

    private static void AddSceneToBuildSettings(string scenePath)
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        bool exists = false;
        foreach (var s in scenes)
        {
            if (s.path == scenePath)
            {
                exists = true;
                s.enabled = true;
                break;
            }
        }

        if (!exists)
        {
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }

    private static Material GetOrCreateDirtMaterial()
    {
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        mat.name = "DirtIndustrialMat";
        mat.color = new Color(0.36f, 0.25f, 0.16f);
        return mat;
    }

    private static Material GetOrCreateGreenZoneMaterial()
    {
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        mat.name = "GreenZoneMarkerMat";
        mat.color = new Color(0.10f, 0.85f, 0.30f, 0.45f);
        return mat;
    }

    private static Material GetOrCreateAsphaltMaterial()
    {
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        mat.name = "AsphaltRoadMat";
        mat.color = new Color(0.18f, 0.20f, 0.22f);
        return mat;
    }

    private static Material GetOrCreateConcreteMaterial()
    {
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        mat.name = "ConcreteStructureMat";
        mat.color = new Color(0.60f, 0.62f, 0.65f);
        return mat;
    }

    private static Material GetOrCreateGravelMaterial()
    {
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        mat.name = "GravelTrackMat";
        mat.color = new Color(0.48f, 0.45f, 0.40f);
        return mat;
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        if (c == null) c = Undo.AddComponent<T>(go);
        return c;
    }
}
