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
        private Text _fpsText;
        private Text _staticFpsText;
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
        private bool _captureStarted;
        private float _captureStartTime;
        private int _captureWidth;
        private int _captureHeight;
        private int _antiAliasingSamples;
        private string _captureDescription;
        public int MainRenderedFrames => _renderedFramesSinceCapture;
        public int StaticRenderedFrames => _staticRenderedFramesSinceCapture;

        // Creates camera-specific overlays and subscribes to actual render completion.
        public void Configure(Camera sceneCamera, Camera overlayCamera, Camera fixedCamera)
        {
            _camera = sceneCamera;
            _staticCamera = fixedCamera;
            _fpsText = CreateDiagnosticOverlay(overlayCamera, "MainCameraDiagnostics");
            _staticFpsText = CreateDiagnosticOverlay(fixedCamera, "StaticCameraDiagnostics");
            Camera.onPostRender += HandleCameraPostRender;
        }

        // Updates the output configuration shown by both camera overlays.
        public void ConfigureCapture(int width, int height, int samples, string description)
        {
            _captureWidth = width;
            _captureHeight = height;
            _antiAliasingSamples = samples;
            _captureDescription = description;
            UpdateDiagnosticText();
        }

        // Starts a shared measurement interval independently of recorder internals.
        public void BeginMeasurement(float startTime)
        {
            _captureStarted = true;
            _captureStartTime = startTime;
            _renderedFramesSinceCapture = 0;
            _staticRenderedFramesSinceCapture = 0;
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
            if (_captureStarted)
            {
                _staticRenderedFramesSinceCapture++;
            }
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
            if (_captureStarted)
            {
                _renderedFramesSinceCapture++;
            }
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
            float frameTimeMilliseconds = _renderFramesPerSecond > 0f
                ? 1000f / _renderFramesPerSecond
                : 0f;
            float elapsed = _captureStarted ? Time.realtimeSinceStartup - _captureStartTime : 0f;
            string commonDiagnostics =
                $"Capture: {_captureWidth}x{_captureHeight} @ {_captureDescription}  |  MSAA: {_antiAliasingSamples}x\n" +
                $"VSync: {(QualitySettings.vSyncCount > 0 ? "On" : "Off")}  |  Elapsed: {elapsed:0.0} s\n" +
                $"GPU: {SystemInfo.graphicsDeviceName}";
            string mainDiagnostics =
                $"Render: {_renderFramesPerSecond:0.0} FPS  ({frameTimeMilliseconds:0.0} ms)\n" +
                $"Rendered: {_renderedFramesSinceCapture}\n" +
                commonDiagnostics;
            _fpsText.text = $"View: Main Camera\n{mainDiagnostics}";
            if (_staticFpsText != null)
            {
                float staticFrameTimeMilliseconds = _staticRenderFramesPerSecond > 0f
                    ? 1000f / _staticRenderFramesPerSecond
                    : 0f;
                string staticDiagnostics =
                    $"Render: {_staticRenderFramesPerSecond:0.0} FPS  ({staticFrameTimeMilliseconds:0.0} ms)\n" +
                    $"Rendered: {_staticRenderedFramesSinceCapture}\n" +
                    commonDiagnostics;
                _staticFpsText.text = $"View: Static Camera\n{staticDiagnostics}";
            }
        }
    }
}
