using UnityEngine;
using UnityEditor;
using Excavator.Engine;

/// <summary>
/// Script de Editor que configura automaticamente la excavadora en la escena.
/// Menu: Excavadora → Configurar Excavadora en Escena
/// </summary>
public class ExcavatorSetup : EditorWindow
{
    [MenuItem("Excavadora/Configurar Excavadora en Escena")]
    static void SetupExcavator()
    {
        // Buscar prefab Bull Dozer 2 o Excavator
        string[] guids = AssetDatabase.FindAssets("Bull Dozer 2 t:Prefab");
        GameObject prefab = null;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) break;
        }

        if (prefab == null)
        {
            guids = AssetDatabase.FindAssets("Excavator t:Prefab");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null) break;
            }
        }

        if (prefab == null)
        {
            EditorUtility.DisplayDialog("Error",
                "No se encontro el prefab de la excavadora.", "OK");
            return;
        }

        GameObject excavator = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        excavator.name = "Excavadora";
        excavator.transform.position   = Vector3.zero;
        excavator.transform.rotation   = Quaternion.identity;
        excavator.transform.localScale = Vector3.one;

        // --- Componentes de movimiento y brazo ---
        if (excavator.GetComponent<ExcavatorMovement>() == null)
            excavator.AddComponent<ExcavatorMovement>();

        if (excavator.GetComponent<ExcavatorArm>() == null)
            excavator.AddComponent<ExcavatorArm>();

        // --- Sistema de motor ---
        if (excavator.GetComponent<KeyboardIgnitionInput>() == null)
            excavator.AddComponent<KeyboardIgnitionInput>();

        if (excavator.GetComponent<EngineController>() == null)
            excavator.AddComponent<EngineController>();

        // --- HUD ---
        if (excavator.GetComponent<EngineHUD>() == null)
            excavator.AddComponent<EngineHUD>();

        // --- Sonido procedural ---
        // Necesita AudioSource (se auto-agrega por RequireComponent)
        if (excavator.GetComponent<ProceduralEngineSound>() == null)
            excavator.AddComponent<ProceduralEngineSound>();

        // --- Piso si no hay uno ---
        if (GameObject.Find("Piso") == null && Object.FindAnyObjectByType<Terrain>() == null)
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Piso";
            floor.transform.position   = Vector3.zero;
            floor.transform.localScale = new Vector3(10, 1, 10);
        }

        // --- Camara ---
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            mainCam.transform.position = excavator.transform.position + new Vector3(-8, 5, -8);
            mainCam.transform.LookAt(excavator.transform.position + Vector3.up);
        }

        Selection.activeGameObject = excavator;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        EditorUtility.DisplayDialog("Excavadora Configurada",
            "Sistema completo instalado. Controles:\n\n" +
            "  MOTOR:\n" +
            "    C          → Activar/desactivar contacto\n" +
            "    I (mantener) → Arrancar motor\n\n" +
            "  MOVIMIENTO (solo con motor encendido):\n" +
            "    W / S      → Avanzar / Retroceder\n" +
            "    A / D      → Girar\n\n" +
            "  BRAZO (solo con motor encendido):\n" +
            "    Q / E      → Girar cabina\n" +
            "    R / F      → Boom sube / baja\n" +
            "    T / G      → Stick extiende / retrae\n" +
            "    Y / H      → Bucket abre / cierra\n\n" +
            "Dale Play para probar!", "OK");
    }

    [MenuItem("Excavadora/Agregar Motor a Excavadora Existente")]
    static void AddEngineToExisting()
    {
        GameObject sel = Selection.activeGameObject;
        if (sel == null)
        {
            EditorUtility.DisplayDialog("Selecciona un GameObject",
                "Selecciona la excavadora en la jerarquia primero.", "OK");
            return;
        }

        bool changed = false;

        if (sel.GetComponent<KeyboardIgnitionInput>() == null)
        { sel.AddComponent<KeyboardIgnitionInput>(); changed = true; }

        if (sel.GetComponent<EngineController>() == null)
        { sel.AddComponent<EngineController>(); changed = true; }

        if (sel.GetComponent<EngineHUD>() == null)
        { sel.AddComponent<EngineHUD>(); changed = true; }

        if (sel.GetComponent<ProceduralEngineSound>() == null)
        { sel.AddComponent<ProceduralEngineSound>(); changed = true; }

        if (changed)
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("Motor agregado",
                "Sistema de motor agregado a: " + sel.name + "\n\n" +
                "Los componentes se auto-conectan entre si al iniciar el juego.", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Ya configurado",
                sel.name + " ya tiene todos los componentes del motor.", "OK");
        }
    }
}
