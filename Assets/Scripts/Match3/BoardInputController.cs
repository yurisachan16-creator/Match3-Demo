using Cysharp.Threading.Tasks;
using Match3.Core.Structs;
using UnityEngine;

namespace Match3.App.Demo
{
    public class BoardInputController : MonoBehaviour
    {
        [SerializeField] private GameController controller;
        [SerializeField] private GameBoardView boardView;
        [SerializeField] private float dragThreshold = 0.25f;
        [SerializeField] private float inputCooldown = 0.08f;
        [SerializeField] private float dragGhostAlpha = 0.5f;

        private bool _isPointerDown;
        private Vector3 _pointerDownWorld;
        private GridPosition _startPos;
        private bool _hasStartPos;
        private float _nextAllowedTime;

        private GameBoardView.DragGhost _ghost;

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

        private void Update()
        {
            if (controller == null || boardView == null)
            {
                return;
            }

            if (controller.IsBusy)
            {
                CancelDrag();
                return;
            }

            if (Time.unscaledTime < _nextAllowedTime)
            {
                return;
            }

            if (TryGetPointerEvent(out var phase, out var screenPos) == false)
            {
                return;
            }

            var world = ScreenToWorld(screenPos);

            if (phase == PointerPhase.Down)
            {
                OnPointerDown(world);
            }
            else if (phase == PointerPhase.Move)
            {
                OnPointerMove(world);
            }
            else if (phase == PointerPhase.Up)
            {
                OnPointerUp(world);
            }
        }

        private void OnPointerDown(Vector3 world)
        {
            _isPointerDown = true;
            _pointerDownWorld = world;

            _hasStartPos = boardView.TryWorldToGrid(world, out _startPos);
            if (_hasStartPos == false)
            {
                return;
            }

            if (controller.HasItemAt(_startPos) == false)
            {
                _hasStartPos = false;
                return;
            }

            boardView.SetHighlight(_startPos, true);
            var c = boardView.GetItemColorAt(_startPos);
            _ghost = boardView.AllocateGhost(c, dragGhostAlpha);
            _ghost.SetWorldPosition(world);
        }

        private void OnPointerMove(Vector3 world)
        {
            if (_isPointerDown == false || _hasStartPos == false)
            {
                return;
            }

            _ghost?.SetWorldPosition(world);

            var delta = world - _pointerDownWorld;
            var absX = Mathf.Abs(delta.x);
            var absY = Mathf.Abs(delta.y);
            var dist = Mathf.Max(absX, absY);

            if (dist < dragThreshold)
            {
                return;
            }

            var dir = absX >= absY
                ? (delta.x >= 0 ? GridPosition.Right : GridPosition.Left)
                : (delta.y >= 0 ? GridPosition.Up : GridPosition.Down);

            var target = new GridPosition(_startPos.RowIndex + dir.RowIndex, _startPos.ColumnIndex + dir.ColumnIndex);
            if (controller.CanSwap(_startPos, target) == false)
            {
                CancelDrag();
                _nextAllowedTime = Time.unscaledTime + inputCooldown;
                return;
            }

            CommitSwap(_startPos, target).Forget();
        }

        private void OnPointerUp(Vector3 world)
        {
            CancelDrag();
            _nextAllowedTime = Time.unscaledTime + inputCooldown;
        }

        private async UniTask CommitSwap(GridPosition a, GridPosition b)
        {
            controller.SetBusy(true);
            CancelDrag();
            _nextAllowedTime = Time.unscaledTime + inputCooldown;
            await controller.SwapAsync(a, b);
            controller.SetBusy(false);
        }

        private void CancelDrag()
        {
            _isPointerDown = false;
            if (_hasStartPos)
            {
                boardView.SetHighlight(_startPos, false);
            }
            _hasStartPos = false;
            if (_ghost != null)
            {
                boardView.RecycleGhost(_ghost);
                _ghost = null;
            }
        }

        private static Vector3 ScreenToWorld(Vector2 screenPos)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                return Vector3.zero;
            }

            var world = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));
            world.z = 0f;
            return world;
        }

        private enum PointerPhase
        {
            Down,
            Move,
            Up
        }

        private static bool TryGetPointerEvent(out PointerPhase phase, out Vector2 screenPos)
        {
            if (Input.touchCount > 0)
            {
                var t = Input.GetTouch(0);
                screenPos = t.position;
                if (t.phase == TouchPhase.Began)
                {
                    phase = PointerPhase.Down;
                    return true;
                }
                if (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary)
                {
                    phase = PointerPhase.Move;
                    return true;
                }
                if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                {
                    phase = PointerPhase.Up;
                    return true;
                }

                phase = default;
                return false;
            }

            if (Input.GetMouseButtonDown(0))
            {
                phase = PointerPhase.Down;
                screenPos = Input.mousePosition;
                return true;
            }

            if (Input.GetMouseButton(0))
            {
                phase = PointerPhase.Move;
                screenPos = Input.mousePosition;
                return true;
            }

            if (Input.GetMouseButtonUp(0))
            {
                phase = PointerPhase.Up;
                screenPos = Input.mousePosition;
                return true;
            }

            phase = default;
            screenPos = default;
            return false;
        }
    }
}

