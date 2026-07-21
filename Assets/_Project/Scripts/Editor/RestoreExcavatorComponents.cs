using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Excavator.Engine;

/// <summary>
/// Agrega todos los componentes del sistema de motor+HUD a la excavadora en escena.
/// Tambien posiciona la camara en primera persona dentro de la cabina.
/// Ejecutar UNA SOLA VEZ: Excavadora → RESTAURAR: Motor + HUD + Camara FPS
/// Borrar este archivo despues de ejecutarlo.
/// </summary>
public static class RestoreExcavatorComponents
{
    [MenuItem("Excavadora/RESTAURAR: Motor + HUD + Camara FPS")]
    static void RestoreAll()
    {
        bool ok = EditorUtility.DisplayDialog(
            "Restaurar Excavadora",
            "Esto va a agregar a la excavadora:\n\n" +
            "  - Rigidbody\n" +
            "  - ExcavatorMovement (movimiento con motor)\n" +
            "  - ExcavatorArm (pala)\n" +
            "  - KeyboardIgnitionInput (C=contacto, I=arrancar)\n" +
            "  - EngineController\n" +
            "  - EngineHUD (barra RPM + estado)\n" +
            "  - ProceduralEngineSound\n" +
            "  - Camara en primera persona (cabina)\n\n" +
            "La escena se guardara al terminar.\n\nContinuar?",
            "Si, restaurar", "Cancelar");

        if (!ok) return;

        // ----------------------------------------------------------------
        // Encontrar la excavadora en escena
        // ----------------------------------------------------------------
        GameObject excavator = FindExcavator();
        if (excavator == null)
        {
            EditorUtility.DisplayDialog("Error",
                "No se encontro la excavadora.\n\n" +
                "Asegurate de tener abierta la escena Escenario1_Circuito.", "OK");
            return;
        }
        Debug.Log($"[RestoreExcavator] Excavadora: '{excavator.name}'");

        // ----------------------------------------------------------------
        // 1. Rigidbody
        // ----------------------------------------------------------------
        var rb = GetOrAdd<Rigidbody>(excavator);
        rb.mass            = 18000f;
        rb.linearDamping   = 5f;
        rb.angularDamping  = 10f;
        rb.constraints     = RigidbodyConstraints.FreezeRotationX |
                             RigidbodyConstraints.FreezeRotationZ;
        EditorUtility.SetDirty(rb);

        // ----------------------------------------------------------------
        // 2. EngineConfig ScriptableObject (buscar en proyecto o crear)
        // ----------------------------------------------------------------
        const string configPath = "Assets/_Project/EngineConfig.asset";
        var engineConfig = AssetDatabase.LoadAssetAtPath<EngineConfig>(configPath);
        if (engineConfig == null)
        {
            engineConfig = ScriptableObject.CreateInstance<EngineConfig>();
            AssetDatabase.CreateAsset(engineConfig, configPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[RestoreExcavator] EngineConfig.asset creado en " + configPath);
        }

        // ----------------------------------------------------------------
        // 3. KeyboardIgnitionInput
        // ----------------------------------------------------------------
        var kbi = GetOrAdd<KeyboardIgnitionInput>(excavator);
        EditorUtility.SetDirty(kbi);

        // ----------------------------------------------------------------
        // 4. EngineController
        // ----------------------------------------------------------------
        var ec = GetOrAdd<EngineController>(excavator);
        // Asignar campos via SerializedObject (nombres reales del script)
        {
            var so = new SerializedObject(ec);
            SetRef(so, "config",               engineConfig);
            SetRef(so, "ignitionInputSource",  kbi);          // MonoBehaviour field
            so.ApplyModifiedProperties();
        }
        EditorUtility.SetDirty(ec);

        // ----------------------------------------------------------------
        // 5. ExcavatorMovement  (IThrottleInput para el EngineController)
        // ----------------------------------------------------------------
        var em = GetOrAdd<ExcavatorMovement>(excavator);
        {
            var so = new SerializedObject(em);
            SetRef(so, "engineController", ec);
            so.ApplyModifiedProperties();
        }
        // Conectar ExcavatorMovement como fuente de throttle
        {
            var so = new SerializedObject(ec);
            SetRef(so, "throttleInputSource", em);
            so.ApplyModifiedProperties();
        }
        EditorUtility.SetDirty(em);

        // ----------------------------------------------------------------
        // 6. ExcavatorArm
        // ----------------------------------------------------------------
        GetOrAdd<ExcavatorArm>(excavator);

        // ----------------------------------------------------------------
        // 7. EngineHUD
        // ----------------------------------------------------------------
        var hud = GetOrAdd<EngineHUD>(excavator);
        {
            var so = new SerializedObject(hud);
            SetRef(so, "engineController", ec);
            so.ApplyModifiedProperties();
        }
        EditorUtility.SetDirty(hud);

        // ----------------------------------------------------------------
        // 8. ProceduralEngineSound  (necesita AudioSource)
        // ----------------------------------------------------------------
        GetOrAdd<AudioSource>(excavator);
        var pes = GetOrAdd<ProceduralEngineSound>(excavator);
        {
            var so = new SerializedObject(pes);
            SetRef(so, "engineController", ec);
            so.ApplyModifiedProperties();
        }
        EditorUtility.SetDirty(pes);

        // ----------------------------------------------------------------
        // 9. Camara en primera persona dentro de la cabina
        // ----------------------------------------------------------------
        SetupFPSCamera(excavator);

        // ----------------------------------------------------------------
        // 10. Guardar escena
        // ----------------------------------------------------------------
        EditorUtility.SetDirty(excavator);
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        EditorUtility.DisplayDialog("Restauracion completada",
            $"Excavadora '{excavator.name}' restaurada.\n\n" +
            "  ✓ Rigidbody\n" +
            "  ✓ KeyboardIgnitionInput  (C = contacto | I = arrancar)\n" +
            "  ✓ EngineController\n" +
            "  ✓ ExcavatorMovement  (requiere motor encendido para moverse)\n" +
            "  ✓ ExcavatorArm  (pala con F/G/H)\n" +
            "  ✓ EngineHUD  (barra RPM + estado del motor)\n" +
            "  ✓ ProceduralEngineSound\n" +
            "  ✓ Camara FPS en cabina\n\n" +
            "Podes borrar RestoreExcavatorComponents.cs cuando quieras.",
            "OK");
    }

    // ----------------------------------------------------------------
    //  Helpers
    // ----------------------------------------------------------------

    static GameObject FindExcavator()
    {
        var allGOs = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);

        // 1. Por nombre exacto
        foreach (var go in allGOs)
            if (go.transform.parent == null && go.name == "Excavator")
                return go;

        // 2. Nombre que contiene "excavat"
        foreach (var go in allGOs)
            if (go.transform.parent == null &&
                go.name.ToLower().Contains("excavat") &&
                go.transform.childCount > 1)
                return go;

        // 3. Tiene BoxCollider en la raiz y multiples hijos
        foreach (var go in allGOs)
        {
            if (go.transform.parent != null) continue;
            var bc = go.GetComponent<BoxCollider>();
            if (bc != null && !bc.isTrigger && go.transform.childCount > 2)
                return go;
        }
        return null;
    }

    static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        if (c == null) c = Undo.AddComponent<T>(go);
        return c;
    }

    static void SetRef(SerializedObject so, string fieldName, Object value)
    {
        var prop = so.FindProperty(fieldName);
        if (prop != null)
            prop.objectReferenceValue = value;
        else
            Debug.LogWarning($"[RestoreExcavator] Campo '{fieldName}' no encontrado en {so.targetObject.GetType().Name}");
    }

    static void SetupFPSCamera(GameObject excavator)
    {
        // Buscar CameraPivot dentro de la excavadora
        Transform cameraPivot = null;
        foreach (Transform t in excavator.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "CameraPivot") { cameraPivot = t; break; }
        }

        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogWarning("[RestoreExcavator] Camera.main no encontrada.");
            return;
        }

        if (cameraPivot == null)
        {
            // Crear CameraPivot si no existe
            var pivotGO = new GameObject("CameraPivot");
            Undo.RegisterCreatedObjectUndo(pivotGO, "Crear CameraPivot");
            Undo.SetTransformParent(pivotGO.transform, excavator.transform, "CameraPivot parent");
            pivotGO.transform.localPosition = Vector3.zero;
            pivotGO.transform.localRotation = Quaternion.identity;
            // Escala 20x para compensar la escala 0.3 del modelo (efectiva: 20 * 0.3 = 6... 
            // usar identidad y ajustar posicion de camara directamente)
            pivotGO.transform.localScale = new Vector3(20f, 20f, 20f);
            cameraPivot = pivotGO.transform;
            Debug.Log("[RestoreExcavator] CameraPivot creado.");
        }

        // Reparentar camara al pivot si es necesario
        if (mainCam.transform.parent != cameraPivot)
        {
            Undo.SetTransformParent(mainCam.transform, cameraPivot, "Camara a CameraPivot");
        }

        // Posicion local dentro de la cabina
        // (ajustada para escala de prefab: el model scale es 0.3, CameraPivot scale es 20)
        Undo.RecordObject(mainCam.transform, "Posicion camara FPS");
        mainCam.transform.localPosition = new Vector3(-2.26f, 4.10f, -1.98f);
        mainCam.transform.localRotation = Quaternion.Euler(-2.2f, -4.0f, 0.27f);
        mainCam.transform.localScale    = new Vector3(0.05f, 0.05f, 0.05f);

        Debug.Log($"[RestoreExcavator] Camara FPS posicionada. Pivot: {cameraPivot.name}");
    }
}
