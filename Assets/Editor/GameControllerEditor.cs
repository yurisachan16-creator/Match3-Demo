using Match3.App.Demo;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GameController))]
public class GameControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var controller = (GameController)target;

        EditorGUILayout.Space(8);

        using (new EditorGUI.DisabledScope(Application.isPlaying == false))
        {
            if (GUILayout.Button("Rebuild Board"))
            {
                controller.RebuildBoard();
            }

            if (GUILayout.Button("Debug Swap"))
            {
                controller.DebugSwap();
            }

            if (GUILayout.Button("Print Board"))
            {
                controller.PrintBoard();
            }
        }
    }
}

