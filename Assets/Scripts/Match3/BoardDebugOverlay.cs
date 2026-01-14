using Match3.Core.Structs;
using UnityEngine;

namespace Match3.App.Demo
{
    public class BoardDebugOverlay : MonoBehaviour
    {
        [SerializeField] private GameController controller;
        [SerializeField] private GameBoardView boardView;
        [SerializeField] private bool showOverlay = true;
        [SerializeField] private bool showIds = true;
        [SerializeField] private bool showMismatchesOnly;
        [SerializeField] private int fontSize = 12;

        private GUIStyle _style;

        private void Awake()
        {
            if (controller == null)
            {
                controller = GetComponent<GameController>();
            }

            if (boardView == null)
            {
                boardView = GetComponent<GameBoardView>();
            }
        }

        private void OnGUI()
        {
            if (showOverlay == false || boardView == null || controller == null || controller.Board == null)
            {
                return;
            }

            _style ??= new GUIStyle(GUI.skin.label);
            _style.fontSize = fontSize;
            _style.alignment = TextAnchor.MiddleCenter;

            var cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            for (int r = 0; r < boardView.RowCount; r++)
            {
                for (int c = 0; c < boardView.ColumnCount; c++)
                {
                    var pos = new GridPosition(r, c);
                    if (boardView.TryGetCellDebugInfo(pos, out var id, out var hasItem, out var viewHas, out var colorMatch, out var world) == false)
                    {
                        continue;
                    }

                    bool mismatch = hasItem != viewHas || (hasItem && colorMatch == false);
                    if (showMismatchesOnly && mismatch == false)
                    {
                        continue;
                    }

                    var sp = cam.WorldToScreenPoint(world);
                    if (sp.z < 0)
                    {
                        continue;
                    }

                    float x = sp.x;
                    float y = Screen.height - sp.y;
                    var rect = new Rect(x - 16, y - 10, 32, 20);

                    if (mismatch)
                    {
                        _style.normal.textColor = Color.red;
                    }
                    else
                    {
                        _style.normal.textColor = Color.white;
                    }

                    if (showIds)
                    {
                        GUI.Label(rect, hasItem ? id.ToString() : ".", _style);
                    }
                }
            }
        }
    }
}

