using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace UnityMediaRecorder.Example
{
    // Owns diagnostic overlays and per-camera render measurements.
    public sealed class SampleDiagnostics : MonoBehaviour
    {
        private const int OverlayLayer = 30;
        private Camera _camera;
        private Camera _staticCamera;
        private Camera _screenCamera;
        [SerializeField]
        private Text _fpsText;
        [SerializeField]
        private Text _staticFpsText;
        public Text ScreenFpsText;
        private float _fpsElapsed;
        private int _fpsFrameCount;
        private float _renderFramesPerSecond;
        private long _lastFpsTimestamp;
        private float _staticFpsElapsed;
        private int _staticFpsFrameCount;
        private float _staticRenderFramesPerSecond;
        private long _staticLastFpsTimestamp;
        private int _renderedFramesSinceCapture;
        private int _staticRenderedFramesSinceCapture;
        private int _screenRenderedFramesSinceCapture;
        private bool _captureStarted;
        private double _applicationFpsStart;
        private int _applicationFrameCount;
        private float _applicationFps;
        public int MainRenderedFrames => _renderedFramesSinceCapture;
        public int StaticRenderedFrames => _staticRenderedFramesSinceCapture;
        public int ScreenRenderedFrames => _screenRenderedFramesSinceCapture;

        // Measures application frames using real elapsed time, independently of camera count and time scale.
        private void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (_applicationFpsStart == 0)
            {
                _applicationFpsStart = now;
                return;
            }

            _applicationFrameCount++;
            double elapsed = now - _applicationFpsStart;
            if (elapsed < 0.25)
            {
                return;
            }

            _applicationFps = (float)(_applicationFrameCount / elapsed);
            _applicationFrameCount = 0;
            _applicationFpsStart = now;
            UpdateDiagnosticText();
        }

        // Creates camera-specific overlays and subscribes to actual render completion.
        public void Configure(Camera sceneCamera, Camera overlayCamera, Camera fixedCamera, Camera screenCamera = null)
        {
            _camera = sceneCamera;
            _staticCamera = fixedCamera;
            _screenCamera = screenCamera;
            if (_fpsText == null)
            {
                _fpsText = CreateDiagnosticOverlay(overlayCamera, "MainCameraDiagnostics");
            }

            if (_staticFpsText == null)
            {
                _staticFpsText = CreateDiagnosticOverlay(fixedCamera, "StaticCameraDiagnostics");
            }

            _fpsText.fontSize = _staticFpsText.fontSize = 28;
            Camera.onPostRender -= HandleCameraPostRender;
            Camera.onPostRender += HandleCameraPostRender;
        }

        // Updates the output configuration shown by both camera overlays.
        public void ConfigureCapture(int width, int height, int samples, string description)
        {
            UpdateDiagnosticText();
        }

        // Starts a shared measurement interval independently of recorder internals.
        public void BeginMeasurement(float startTime)
        {
            _captureStarted = true;
            _renderedFramesSinceCapture = 0;
            _staticRenderedFramesSinceCapture = 0;
            _screenRenderedFramesSinceCapture = 0;
            UpdateDiagnosticText();
        }

        // Stops collecting capture totals while retaining live render measurements.
        public void EndMeasurement()
        {
            _captureStarted = false;
        }

        // Disconnects the render callback when the diagnostic component is destroyed.
        private void OnDestroy()
        {
            Camera.onPostRender -= HandleCameraPostRender;
        }

        // Creates one screen-space diagnostic canvas for a specific output camera.
        private static Text CreateDiagnosticOverlay(Camera targetCamera, string objectName)
        {
            GameObject canvasObject = new GameObject($"{objectName}Canvas");
            canvasObject.layer = OverlayLayer;
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = targetCamera;
            canvas.planeDistance = 1f;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            GameObject overlay = new GameObject(objectName);
            overlay.layer = OverlayLayer;
            overlay.transform.SetParent(canvasObject.transform, false);
            Text diagnosticText = overlay.AddComponent<Text>();
            diagnosticText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            diagnosticText.alignment = TextAnchor.UpperLeft;
            diagnosticText.fontSize = 20;
            diagnosticText.color = Color.white;
            diagnosticText.horizontalOverflow = HorizontalWrapMode.Overflow;
            diagnosticText.verticalOverflow = VerticalWrapMode.Overflow;
            diagnosticText.text = "Preparing recorder...";
            RectTransform overlayTransform = diagnosticText.rectTransform;
            overlayTransform.anchorMin = new Vector2(0f, 1f);
            overlayTransform.anchorMax = new Vector2(0f, 1f);
            overlayTransform.pivot = new Vector2(0f, 1f);
            overlayTransform.anchoredPosition = new Vector2(20f, -18f);
            overlayTransform.sizeDelta = new Vector2(1100f, 300f);
            return diagnosticText;
        }

        // Counts completed renders from the example camera inside Unity's normal render loop.
        private void HandleCameraPostRender(Camera renderedCamera)
        {
            if (_captureStarted)
            {
                if (renderedCamera == _camera) _renderedFramesSinceCapture++;
                if (renderedCamera == _staticCamera) _staticRenderedFramesSinceCapture++;
                if (_screenCamera != null && renderedCamera == _screenCamera) _screenRenderedFramesSinceCapture++;
            }

            long timestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            if (renderedCamera == _camera)
            {
                UpdateFrameRateMeasurement(timestamp);
                return;
            }

            if (renderedCamera == _staticCamera)
            {
                UpdateStaticFrameRateMeasurement(timestamp);
            }
        }

        // Updates the measured rate after one static-camera render completes.
        private void UpdateStaticFrameRateMeasurement(long timestamp)
        {
            if (_staticFpsText == null)
            {
                return;
            }

            if (_staticLastFpsTimestamp == 0)
            {
                _staticLastFpsTimestamp = timestamp;
            }

            _staticFpsElapsed += (float)(timestamp - _staticLastFpsTimestamp) / System.Diagnostics.Stopwatch.Frequency;
            _staticLastFpsTimestamp = timestamp;
            _staticFpsFrameCount++;

            if (_staticFpsElapsed >= 0.25f)
            {
                _staticRenderFramesPerSecond = _staticFpsFrameCount / _staticFpsElapsed;
                _staticFpsElapsed = 0f;
                _staticFpsFrameCount = 0;
                UpdateDiagnosticText();
            }
        }

        // Updates the measured rate after one camera render completes.
        private void UpdateFrameRateMeasurement(long timestamp)
        {
            if (_fpsText == null)
            {
                return;
            }

            if (_lastFpsTimestamp == 0)
            {
                _lastFpsTimestamp = timestamp;
            }

            _fpsElapsed += (float)(timestamp - _lastFpsTimestamp) / System.Diagnostics.Stopwatch.Frequency;
            _lastFpsTimestamp = timestamp;
            _fpsFrameCount++;

            if (_fpsElapsed >= 0.25f)
            {
                _renderFramesPerSecond = _fpsFrameCount / _fpsElapsed;
                _fpsElapsed = 0f;
                _fpsFrameCount = 0;
                UpdateDiagnosticText();
            }
        }

        // Formats the current scene and recorder diagnostics into the camera overlay.
        private void UpdateDiagnosticText()
        {
            if (ScreenFpsText != null)
            {
                ScreenFpsText.text = $"{_applicationFps:0.0} FPS  |  {Screen.width} × {Screen.height}";
            }
            if (_fpsText != null && _camera != null)
            {
                _fpsText.text = $"{_applicationFps:0.0} FPS  |  {_camera.pixelWidth} × {_camera.pixelHeight}";
            }

            if (_staticFpsText != null && _staticCamera != null)
            {
                _staticFpsText.text = $"{_applicationFps:0.0} FPS  |  {_staticCamera.pixelWidth} × {_staticCamera.pixelHeight}";
            }
        }
    }
}
