using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Excavator.Engine;

/// <summary>
/// Script de Editor para agregar el sistema de penalidades e informe PDF a la escena.
/// No modifica los scripts ni componentes existentes.
/// </summary>
public static class AddPenaltySystemToScene
{
    [MenuItem("Excavadora/Agregar Sistema de Penalidades y Reporte PDF")]
    public static void AttachSystem()
    {
        GameObject excavator = FindExcavator();
        if (excavator == null)
        {
            EditorUtility.DisplayDialog("Error",
                "No se encontró el GameObject de la Excavadora en la escena activa.", "OK");
            return;
        }

        Undo.RegisterCompleteObjectUndo(excavator, "Agregar Penalty System");

        // 1. PenaltyTracker
        var tracker = excavator.GetComponent<PenaltyTracker>();
        if (tracker == null)
        {
            tracker = Undo.AddComponent<PenaltyTracker>(excavator);
            Debug.Log($"[AddPenaltySystem] PenaltyTracker agregado a '{excavator.name}'.");
        }

        // 2. SimulatorReportManager
        var reportMgr = excavator.GetComponent<SimulatorReportManager>();
        if (reportMgr == null)
        {
            reportMgr = Undo.AddComponent<SimulatorReportManager>(excavator);
            Debug.Log($"[AddPenaltySystem] SimulatorReportManager agregado a '{excavator.name}'.");
        }

        // Conectar referencias
        reportMgr.penaltyTracker = tracker;
        reportMgr.engineController = excavator.GetComponent<EngineController>();
        reportMgr.parkingZone = Object.FindAnyObjectByType<ParkingZone>();

        EditorUtility.SetDirty(excavator);
        var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);

        EditorUtility.DisplayDialog("Sistema de Penalidades Instalado",
            $"Se instaló correctamente el sistema de evaluación en '{excavator.name}'.\n\n" +
            "  ✓ PenaltyTracker (Conteo de colisiones y deducción de puntos)\n" +
            "  ✓ SimulatorReportManager (Reporte en pantalla y exportación a PDF)\n\n" +
            "Controles durante la simulación:\n" +
            "  - Puntos iniciales: 100\n" +
            "  - Tecla P: Abrir / Cerrar Informe de Evaluación en PDF",
            "OK");
    }

    private static GameObject FindExcavator()
    {
        var allGOs = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);

        foreach (var go in allGOs)
            if (go.transform.parent == null && go.name == "Excavator")
                return go;

        foreach (var go in allGOs)
            if (go.transform.parent == null && go.name.ToLower().Contains("excavat") && go.transform.childCount > 1)
                return go;

        return null;
    }
}
