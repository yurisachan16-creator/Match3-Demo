using UnityEngine;

namespace Match3.App.Demo
{
    public class FpsMonitor : MonoBehaviour
    {
        [SerializeField] private bool showOnGui = true;
        [SerializeField] private float sampleInterval = 0.5f;
        [SerializeField] private float warnFpsThreshold = 45f;
        [SerializeField] private float warnDuration = 2f;

        private float _timeLeft;
        private int _frames;
        private float _accumulated;

        private float _avgFps;
        private float _lowFpsTime;
        private float _lastWarnTime;

        private void OnEnable()
        {
            _timeLeft = sampleInterval;
            _frames = 0;
            _accumulated = 0f;
            _avgFps = 0f;
            _lowFpsTime = 0f;
            _lastWarnTime = 0f;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f)
            {
                return;
            }

            _timeLeft -= dt;
            _accumulated += 1f / dt;
            _frames++;

            if (_timeLeft > 0f)
            {
                return;
            }

            _avgFps = _frames > 0 ? _accumulated / _frames : 0f;

            if (_avgFps > 0f && _avgFps < warnFpsThreshold)
            {
                _lowFpsTime += sampleInterval;
                if (_lowFpsTime >= warnDuration && Time.unscaledTime - _lastWarnTime > warnDuration)
                {
                    _lastWarnTime = Time.unscaledTime;
                    Debug.LogWarning($"Low FPS detected: {_avgFps:F1} (threshold {warnFpsThreshold:F0})");
                }
            }
            else
            {
                _lowFpsTime = 0f;
            }

            _timeLeft = sampleInterval;
            _frames = 0;
            _accumulated = 0f;
        }

        private void OnGUI()
        {
            if (showOnGui == false)
            {
                return;
            }

            GUI.Label(new Rect(10, 10, 220, 24), $"FPS: {_avgFps:F1}");
        }
    }
}

