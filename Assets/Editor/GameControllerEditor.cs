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

            if (GUILayout.Button("Dump Debug Log"))
            {
                Debug.Log(Match3DebugLog.Dump());
            }

            if (GUILayout.Button("Clear Debug Log"))
            {
                Match3DebugLog.Clear();
            }

            var tester = controller.GetComponent<Match3StabilityTester>();
            if (tester != null)
            {
                if (GUILayout.Button("Run Stability Test"))
                {
                    tester.Run();
                }
            }
        }
    }
}
