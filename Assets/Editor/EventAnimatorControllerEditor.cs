using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EventAnimatorController))]
public class EventAnimatorControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        var controller = (EventAnimatorController)target;

        if (GUILayout.Button("Fetch References From Children", GUILayout.Height(28)))
        {
            Undo.RecordObject(controller, "Fetch Event Animator References");
            controller.FetchFromChildren();
            EditorUtility.SetDirty(controller);
        }
    }
}
