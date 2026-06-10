using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BackgroundSpawner))]
public class BackgroundSpawnerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        var spawner = (BackgroundSpawner)target;

        if (GUILayout.Button("Populate Preview", GUILayout.Height(30)))
        {
            Undo.RegisterFullObjectHierarchyUndo(spawner.gameObject, "Populate Preview");
            spawner.PopulatePreview();
        }

        if (GUILayout.Button("Clear Preview", GUILayout.Height(24)))
        {
            Undo.RegisterFullObjectHierarchyUndo(spawner.gameObject, "Clear Preview");
            spawner.ClearPreview();
        }
    }
}
