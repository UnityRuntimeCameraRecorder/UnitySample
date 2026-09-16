using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace UnityMediaRecorder.Example
{
    // Coordinates capture sessions without constructing scenery or overlays.
    public sealed class SampleCaptureController : MonoBehaviour
    {
        private Camera _camera;
        private Camera _overlayCamera;
        private Camera _staticCamera;
        private OrbitCamera _orbitCamera;
        private TemporalMotionSmoothing _mainTemporalSmoothing;
        private TemporalMotionSmoothing _staticTemporalSmoothing;
        private SampleDiagnostics _diagnostics;

        // Connects scene-owned cameras to independent recording sessions.
        public void Configure(Camera sceneCamera, Camera overlayCamera, Camera fixedCamera,
            OrbitCamera orbit, SampleDiagnostics diagnostics)
        {
            _camera = sceneCamera;
            _overlayCamera = overlayCamera;
            _staticCamera = fixedCamera;
            _orbitCamera = orbit;
            _diagnostics = diagnostics;
            _mainTemporalSmoothing = sceneCamera.GetComponent<TemporalMotionSmoothing>();
            _staticTemporalSmoothing = fixedCamera.GetComponent<TemporalMotionSmoothing>();
            _benchmarkMode = Environment.GetEnvironmentVariable("CAPTURE_BENCHMARK_MODE") ?? string.Empty;
            _recorder = CreateRecorder();
            _staticRecorder = CreateRecorder();
        }

        // Creates a recorder and subscribes to its session lifecycle.
        private UnityMediaRecorder CreateRecorder()
        {
            var recorder = gameObject.AddComponent<UnityMediaRecorder>();
            recorder.CaptureStarted += HandleCaptureStarted;
            recorder.RecordingCompleted += HandleRecordingCompleted;
            recorder.RecordingFailed += HandleRecordingFailed;
            return recorder;
        }

        // Starts the selected capture or controlled benchmark workflow.
        public void BeginCapture()
        {
            StartCoroutine(RecordOrbit());
        }

        // Disconnects recorder callbacks and releases capture-owned GPU targets.
        private void OnDestroy()
        {
            DisconnectRecorder(_recorder);
            DisconnectRecorder(_staticRecorder);
            ReleasePreparedTarget();
        }

        // Disconnects one recorder without depending on component destruction order.
        private void DisconnectRecorder(UnityMediaRecorder recorder)
        {
            if (recorder == null) return;
            recorder.CaptureStarted -= HandleCaptureStarted;
            recorder.RecordingCompleted -= HandleRecordingCompleted;
            recorder.RecordingFailed -= HandleRecordingFailed;
        }

        private const float CaptureDurationSeconds = 10f;
        private RenderTexture _preparedTarget;
        private RenderTexture _staticPreparedTarget;
        private UnityMediaRecorder _recorder;
        private UnityMediaRecorder _staticRecorder;
        private bool _captureStarted;
        private int _startedRecorderCount;
        private int _completedRecorderCount;
        private float _captureStartTime;
        private string _benchmarkMode;

        // Starts recording, completes the ten-second staged revolution and requests finalization.
        private IEnumerator RecordOrbit()
        {
            yield return null;
            string directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "UnitySample");
            Directory.CreateDirectory(directory);
            int width = ReadPositiveEnvironmentInteger("CAPTURE_WIDTH", Math.Max(2, Screen.width & ~1)) & ~1;
            int height = ReadPositiveEnvironmentInteger("CAPTURE_HEIGHT", Math.Max(2, Screen.height & ~1)) & ~1;
            int frameRate = ReadPositiveEnvironmentInteger("CAPTURE_FRAME_RATE", 60);
            int antiAliasingSamples = ReadPositiveEnvironmentInteger("CAPTURE_MSAA", 4);
            _diagnostics.ConfigureCapture(width, height, antiAliasingSamples,
                _benchmarkMode.StartsWith("dual-nvenc", StringComparison.OrdinalIgnoreCase) ? $"{frameRate} FPS" : "1 PNG/s");
            string profileName = Environment.GetEnvironmentVariable("CAPTURE_PROFILE") ?? $"{width}x{height}_{frameRate}fps";
            string baseName = $"CubeOrbit_{profileName}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}";
            _preparedTarget = new RenderTexture(
                width,
                height,
                24,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            _preparedTarget.antiAliasing = antiAliasingSamples;
            _preparedTarget.Create();
            _staticPreparedTarget = new RenderTexture(
                width,
                height,
                24,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            _staticPreparedTarget.antiAliasing = antiAliasingSamples;
            _staticPreparedTarget.Create();
            _camera.targetTexture = _preparedTarget;
            _overlayCamera.targetTexture = _preparedTarget;
            _staticCamera.targetTexture = _staticPreparedTarget;
            if (!string.IsNullOrEmpty(_benchmarkMode))
            {
                yield return RunBenchmarkMode(
                    directory,
                    baseName,
                    width,
                    height,
                    frameRate,
                    antiAliasingSamples);
                yield break;
            }

            _recorder.StartPngSequence(
                _overlayCamera,
                CreatePngSequenceSettings(
                    Path.Combine(directory, $"{baseName}_MainCamera_Frames"),
                    width,
                    height,
                    antiAliasingSamples,
                    0.0),
                _preparedTarget);
            _staticRecorder.StartPngSequence(
                _staticCamera,
                CreatePngSequenceSettings(
                    Path.Combine(directory, $"{baseName}_StaticCamera_Frames"),
                    width,
                    height,
                    antiAliasingSamples,
                    0.5),
                _staticPreparedTarget);
            while (!_captureStarted)
            {
                yield return null;
            }

            yield return new WaitForSecondsRealtime(CaptureDurationSeconds);
            _recorder.StopPngSequence();
            _staticRecorder.StopPngSequence();
        }

        // Runs one controlled render or NVENC benchmark selected through the environment.
        private IEnumerator RunBenchmarkMode(
            string directory,
            string baseName,
            int width,
            int height,
            int frameRate,
            int antiAliasingSamples)
        {
            bool singleCamera = string.Equals(_benchmarkMode, "single-render", StringComparison.OrdinalIgnoreCase);
            bool useNvenc = _benchmarkMode.StartsWith("dual-nvenc", StringComparison.OrdinalIgnoreCase);
            bool disableTemporal = string.Equals(
                _benchmarkMode,
                "dual-nvenc-no-temporal",
                StringComparison.OrdinalIgnoreCase);
            _staticCamera.enabled = !singleCamera;
            if (disableTemporal)
            {
                _mainTemporalSmoothing.enabled = false;
                _staticTemporalSmoothing.enabled = false;
            }

            if (useNvenc)
            {
                _recorder.StartRecording(
                    _overlayCamera,
                    _camera.GetComponent<AudioListener>(),
                    CreateRecordingSettings(
                        directory,
                        $"{baseName}_{_benchmarkMode}_MainCamera",
                        width,
                        height,
                        frameRate,
                        antiAliasingSamples),
                    _preparedTarget);
                _staticRecorder.StartRecording(
                    _staticCamera,
                    _camera.GetComponent<AudioListener>(),
                    CreateRecordingSettings(
                        directory,
                        $"{baseName}_{_benchmarkMode}_StaticCamera",
                        width,
                        height,
                        frameRate,
                        antiAliasingSamples),
                    _staticPreparedTarget);
                while (!_captureStarted)
                {
                    yield return null;
                }
            }
            else
            {
                BeginMeasurement();
            }

            yield return new WaitForSecondsRealtime(CaptureDurationSeconds);
            ReportBenchmarkMeasurement();
            if (useNvenc)
            {
                _recorder.StopRecording();
                _staticRecorder.StopRecording();
            }
            else
            {
                ReleasePreparedTarget();
                if (!Application.isEditor)
                {
                    Application.Quit();
                }
            }
        }

        // Creates one independent output configuration for a synchronized camera recording.
        private static RecordingSettings CreateRecordingSettings(
            string directory,
            string baseName,
            int width,
            int height,
            int frameRate,
            int antiAliasingSamples)
        {
            return new RecordingSettings
            {
                FfmpegPath = Environment.GetEnvironmentVariable("FFMPEG_PATH"),
                TemporaryContainerPath = Path.Combine(directory, $"{baseName}.mkv.tmp"),
                ArchivePath = Path.Combine(directory, $"{baseName}.mkv"),
                KeepIntermediateFile = false,
                GeneratePreviewImage = false,
                OutputPath = Path.Combine(directory, $"{baseName}.mp4"),
                Width = width,
                Height = height,
                MaximumFrameRate = frameRate,
                AntiAliasingSamples = antiAliasingSamples,
                EncodingQuality = VideoEncodingQuality.Balanced,
                FlipVertically = SystemInfo.graphicsUVStartsAtTop
            };
        }

        // Creates a one-image-per-second PNG sequence matching its associated video output.
        private static PngSequenceSettings CreatePngSequenceSettings(
            string outputDirectory,
            int width,
            int height,
            int antiAliasingSamples,
            double initialDelaySeconds)
        {
            return new PngSequenceSettings
            {
                OutputDirectory = outputDirectory,
                FileNamePrefix = "frame_",
                Width = width,
                Height = height,
                CapturesPerSecond = 1.0,
                InitialDelaySeconds = initialDelaySeconds,
                AntiAliasingSamples = antiAliasingSamples,
                FlipVertically = false
            };
        }

        // Reads a positive integer override while retaining a safe default for missing or invalid values.
        private static int ReadPositiveEnvironmentInteger(string name, int defaultValue)
        {
            string value = Environment.GetEnvironmentVariable(name);
            return int.TryParse(value, out int parsed) && parsed > 0 ? parsed : defaultValue;
        }

        // Marks the instant at which media inputs are connected and capture is active.
        private void HandleCaptureStarted()
        {
            _startedRecorderCount++;
            if (_startedRecorderCount < 2)
            {
                return;
            }

            BeginMeasurement();
            Debug.Log("Sample capture started.");
        }

        // Resets both render counters and starts a common measurement interval.
        private void BeginMeasurement()
        {
            _captureStarted = true;
            _captureStartTime = Time.realtimeSinceStartup;
            _orbitCamera.BeginOrbit(_captureStartTime);
            _diagnostics.BeginMeasurement(_captureStartTime);
        }

        // Writes the average render rate for the selected benchmark interval.
        private void ReportBenchmarkMeasurement()
        {
            float elapsed = Math.Max(0.001f, Time.realtimeSinceStartup - _captureStartTime);
            float mainAverageFps = _diagnostics.MainRenderedFrames / elapsed;
            float staticAverageFps = _diagnostics.StaticRenderedFrames / elapsed;
            Debug.Log(
                $"CAPTURE_BENCHMARK mode={_benchmarkMode}; elapsed={elapsed:0.000}; " +
                $"mainFps={mainAverageFps:0.00}; mainFrames={_diagnostics.MainRenderedFrames}; " +
                $"staticFps={staticAverageFps:0.00}; staticFrames={_diagnostics.StaticRenderedFrames}");
        }

        // Reports the completed files produced on the Desktop.
        private void HandleRecordingCompleted()
        {
            _completedRecorderCount++;
            if (_completedRecorderCount < 2)
            {
                return;
            }

            float elapsed = Math.Max(0.001f, Time.realtimeSinceStartup - _captureStartTime);
            float mainAverageFps = _diagnostics.MainRenderedFrames / elapsed;
            float staticAverageFps = _diagnostics.StaticRenderedFrames / elapsed;
            Debug.Log(
                $"Unity render averages over {elapsed:0.000} s: " +
                $"main camera={mainAverageFps:0.00} FPS ({_diagnostics.MainRenderedFrames} frames), " +
                $"static camera={staticAverageFps:0.00} FPS ({_diagnostics.StaticRenderedFrames} frames).");
            Debug.Log("Sample capture completed in Desktop/UnitySample.");
            _diagnostics.EndMeasurement();
            ReleasePreparedTarget();
            if (!Application.isEditor)
            {
                Application.Quit();
            }
        }

        // Reports a recording failure through the Unity console.
        private void HandleRecordingFailed(Exception exception)
        {
            Debug.LogException(exception);
            StopAllCoroutines();
            _diagnostics.EndMeasurement();
            ReleasePreparedTarget();
            if (!Application.isEditor)
            {
                Application.Quit(1);
            }
        }

        // Releases the camera target owned by this example after recording ends.
        private void ReleasePreparedTarget()
        {
            if (_preparedTarget == null)
            {
                return;
            }

            if (_camera != null && _camera.targetTexture == _preparedTarget)
            {
                _camera.targetTexture = null;
            }
            if (_overlayCamera != null && _overlayCamera.targetTexture == _preparedTarget)
            {
                _overlayCamera.targetTexture = null;
            }
            if (_staticCamera != null && _staticCamera.targetTexture == _staticPreparedTarget)
            {
                _staticCamera.targetTexture = null;
            }
            _preparedTarget.Release();
            Destroy(_preparedTarget);
            _preparedTarget = null;
            if (_staticPreparedTarget != null)
            {
                _staticPreparedTarget.Release();
                Destroy(_staticPreparedTarget);
                _staticPreparedTarget = null;
            }
        }
    }
}
