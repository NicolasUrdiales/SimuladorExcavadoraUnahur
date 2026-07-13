using UnityEngine;
using UnityEditor;

/// <summary>
/// Script de Editor que configura automaticamente la excavadora en la escena.
/// Uso: Menu Unity -> Excavadora -> Configurar Excavadora en Escena
/// </summary>
public class ExcavatorSetup : EditorWindow
{
    [MenuItem("Excavadora/Configurar Excavadora en Escena")]
    static void SetupExcavator()
    {
        // Buscar el prefab Bull Dozer 2
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

        // Instanciar el prefab en la escena
        GameObject excavator = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        excavator.name = "Excavadora";
        excavator.transform.position = Vector3.zero;
        excavator.transform.rotation = Quaternion.identity;
        excavator.transform.localScale = Vector3.one;

        // Agregar scripts (se auto-configuran solos)
        if (excavator.GetComponent<ExcavatorMovement>() == null)
            excavator.AddComponent<ExcavatorMovement>();

        if (excavator.GetComponent<ExcavatorArm>() == null)
            excavator.AddComponent<ExcavatorArm>();

        // Crear piso si no hay uno
        if (GameObject.Find("Piso") == null && Object.FindAnyObjectByType<Terrain>() == null)
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Piso";
            floor.transform.position = Vector3.zero;
            floor.transform.localScale = new Vector3(10, 1, 10);
        }

        // Posicionar camara
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
            "Controles:\n" +
            "  W/S - Avanzar/Retroceder\n" +
            "  A/D - Girar\n" +
            "  Q/E - Girar cabina\n" +
            "  R/F - Boom sube/baja\n" +
            "  T/G - Stick extiende/retrae\n" +
            "  Y/H - Bucket abre/cierra\n\n" +
            "Dale Play para probar!", "OK");
    }
}
