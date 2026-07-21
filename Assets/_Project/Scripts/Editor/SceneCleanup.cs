using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// Script de limpieza UNICO. Elimina todo el contenido generado automaticamente
/// por el CityCircuitBuilder (ya eliminado). Ejecutar UNA SOLA VEZ desde
/// Excavadora → Limpiar Escena. Despues borrar este archivo.
/// </summary>
public static class SceneCleanup
{
    [MenuItem("Excavadora/Limpiar Escena — Borrar Contenido Generado")]
    static void CleanScene()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

        bool ok = EditorUtility.DisplayDialog(
            "Limpiar Escena",
            "Esto eliminara:\n\n" +
            "  • 'CircuitoCiudad_Contenedor' y todos sus hijos\n" +
            "    (todas las barreras y edificios generados)\n\n" +
            "La excavadora, ParkZone, Plane, luces y camara\n" +
            "NO seran tocados.\n\n" +
            "Continuar?",
            "Si, limpiar", "Cancelar");

        if (!ok) return;

        int removed = 0;

        // 1. Borrar el contenedor principal con todos sus hijos
        var container = GameObject.Find("CircuitoCiudad_Contenedor");
        if (container != null)
        {
            int childCount = container.transform.childCount;
            Undo.DestroyObjectImmediate(container);
            removed += childCount + 1;
            Debug.Log($"[SceneCleanup] Eliminado CircuitoCiudad_Contenedor ({childCount} hijos).");
        }

        // 2. Barreras sueltas en la raiz de la escena (sin padre)
        var toDestroy = new List<GameObject>();

#if UNITY_2023_1_OR_NEWER
        var allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
#else
        var allObjects = Object.FindObjectsOfType<GameObject>(true);
#endif

        foreach (var go in allObjects)
        {
            if (go == null || go.transform.parent != null) continue;
            string n = go.name.ToLower();
            if (n.Contains("barrier") || n.Contains("channelizing"))
                toDestroy.Add(go);
        }

        foreach (var go in toDestroy)
        {
            Debug.Log($"[SceneCleanup] Eliminando barrera suelta: {go.name}");
            Undo.DestroyObjectImmediate(go);
            removed++;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        EditorUtility.DisplayDialog(
            "Limpieza completada",
            $"Se eliminaron {removed} objetos generados.\n\n" +
            "Escena guardada. Podes colocar las barreras a mano.\n\n" +
            "Podes borrar el archivo SceneCleanup.cs cuando quieras.",
            "OK");
    }
}
