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
        [SerializeField] private string _mode = "dual-nvenc";
        [SerializeField] private string _ffmpegPath = @"C:\src\ffmpeg-9.0.1\bin\ffmpeg.exe";
        [SerializeField, Min(2)] private int _width = 3840;
        [SerializeField, Min(2)] private int _height = 2160;
        [SerializeField, Range(1, 60)] private int _frameRate = 60;
        [SerializeField] private int _msaa = 4;
        private int _renderWidth = 2560, _renderHeight = 1440;
        private int _encodingPreset = 4;
        [SerializeField, Min(0.1f)] private float CaptureDurationSeconds = 10f;
        private Camera _camera;
        private Camera _overlayCamera;
        private Camera _staticCamera;
        private OrbitCamera _orbitCamera;
        private TemporalMotionSmoothing _mainTemporalSmoothing;
        private TemporalMotionSmoothing _staticTemporalSmoothing;
        private SampleDiagnostics _diagnostics;
        private bool _recordMain = true;
        private bool _recordFixed = true;
        private bool _recordScreen;
        private UnityMediaRecorder _screenRecorder;
        private int _expectedRecorderCount = 2;
        private RenderTexture _previousMainTarget;
        private RenderTexture _previousOverlayTarget;
        private RenderTexture _previousFixedTarget;
        private RecordingSessionStats _sessionStats;
        private string _statsPath;
        private readonly List<string> _videoFiles = new List<string>();
        private bool _measurementEnded;
        private float _finalizationStartTime;
        public bool IsCapturing { get; private set; }
        public string StatusText { get; private set; } = "";
        public bool IsScreenRecording => IsCapturing && _recordScreen;

        // Selects the NVENC preset for future recordings.
        public void SetEncodingPreset(int preset)
        {
            if (!IsCapturing) _encodingPreset = Mathf.Clamp(preset, 1, 7);
        }

        // Changes automatic recording duration before a session starts.
        public void SetDuration(float seconds)
        {
            if (!IsCapturing) CaptureDurationSeconds = seconds;
        }

        // Selects the sample count for recording targets before a session starts.
        public void SetAntiAliasing(int samples)
        {
            if (!IsCapturing) _msaa = samples;
        }

        // Changes video output dimensions and frame-rate ceiling only between sessions.
        public void SetOutputSettings(int width, int height, int frameRate)
        {
            if (IsCapturing) return;
            _width = width;
            _height = height;
            _frameRate = frameRate;
        }

        // Selects camera render dimensions independently of encoded video dimensions.
        public void SetRenderResolution(int width, int height)
        {
            if (IsCapturing) return;
            _renderWidth = width;
            _renderHeight = height;
        }

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
            _benchmarkMode = Environment.GetEnvironmentVariable("CAPTURE_BENCHMARK_MODE") ?? _mode;
            _recorder = CreateRecorder();
            _staticRecorder = CreateRecorder();
            _screenRecorder = CreateRecorder();
        }

        // Creates a recorder and subscribes to its session lifecycle.
        private UnityMediaRecorder CreateRecorder()
        {
            var recorder = gameObject.AddComponent<UnityMediaRecorder>();
            recorder.CaptureStarted += HandleCaptureStarted;
            recorder.FinalizationStarted += HandleFinalizationStarted;
            recorder.RecordingCompleted += HandleRecordingCompleted;
            recorder.RecordingFailed += HandleRecordingFailed;
            return recorder;
        }

        // Starts the selected capture or controlled benchmark workflow.
        public void BeginCapture()
        {
            if (IsCapturing) return;
            IsCapturing = true;
            _sessionStats = null;
            _videoFiles.Clear();
            _measurementEnded = false;
            StatusText = "Preparing recording…";
            _captureStarted = false;
            _startedRecorderCount = 0;
            _completedRecorderCount = 0;
            StartCoroutine(RecordOrbit());
        }

        // Records only the cameras selected by the screen controls.
        public void BeginCapture(bool camera1, bool camera2, bool screen = false)
        {
            if (IsCapturing || (!camera1 && !camera2 && !screen)) return;
            _recordMain = camera1;
            _recordFixed = camera2;
            _recordScreen = screen;
            _expectedRecorderCount = (camera1 ? 1 : 0) + (camera2 ? 1 : 0) + (screen ? 1 : 0);
            _benchmarkMode = "dual-nvenc";
            BeginCapture();
        }

        // Stops active video inputs and lets their independent writers finalize the files.
        public void StopCapture()
        {
            if (!IsCapturing) return;
            StatusText = "Finalizing MP4 files — packaging video and audio…";
            HandleFinalizationStarted();
            StopAllCoroutines();
            if (_recordMain) _recorder.StopRecording();
            if (_recordFixed) _staticRecorder.StopRecording();
            if (_recordScreen) _screenRecorder.StopRecording();
        }

        // Disconnects recorder callbacks and releases capture-owned GPU targets.
        private void OnDestroy()
        {
            DisconnectRecorder(_recorder);
            DisconnectRecorder(_staticRecorder);
            DisconnectRecorder(_screenRecorder);
            ReleasePreparedTarget();
        }

        // Disconnects one recorder without depending on component destruction order.
        private void DisconnectRecorder(UnityMediaRecorder recorder)
        {
            if (recorder == null) return;
            recorder.CaptureStarted -= HandleCaptureStarted;
            recorder.FinalizationStarted -= HandleFinalizationStarted;
            recorder.RecordingCompleted -= HandleRecordingCompleted;
            recorder.RecordingFailed -= HandleRecordingFailed;
        }

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
            string directory = Path.Combine(Path.GetDirectoryName(Application.dataPath), "output");
            Directory.CreateDirectory(directory);
            int width = ReadPositiveEnvironmentInteger("CAPTURE_WIDTH", _width) & ~1;
            int height = ReadPositiveEnvironmentInteger("CAPTURE_HEIGHT", _height) & ~1;
            int frameRate = ReadPositiveEnvironmentInteger("CAPTURE_FRAME_RATE", _frameRate);
            int antiAliasingSamples = ReadPositiveEnvironmentInteger("CAPTURE_MSAA", _msaa);
            _diagnostics.ConfigureCapture(width, height, antiAliasingSamples,
                _benchmarkMode.StartsWith("dual-nvenc", StringComparison.OrdinalIgnoreCase) ? $"{frameRate} FPS" : "1 PNG/s");
            string profileName = Environment.GetEnvironmentVariable("CAPTURE_PROFILE") ?? $"{width}x{height}_{frameRate}fps";
            string baseName = $"UnitySample_{profileName}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}";
            _statsPath = Path.Combine(directory, baseName + "_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".stats.json");
            _sessionStats = new RecordingSessionStats
            {
                status = "preparing", startedUtc = DateTime.UtcNow.ToString("O"),
                gpu = SystemInfo.graphicsDeviceName, unityVersion = Application.unityVersion,
                renderWidth = _renderWidth, renderHeight = _renderHeight,
                outputWidth = width, outputHeight = height, maximumVideoFps = frameRate,
                msaaSamples = antiAliasingSamples, vSyncCount = QualitySettings.vSyncCount,
                encodingPreset = _encodingPreset,
                camera1 = _recordMain, camera2 = _recordFixed, screen = _recordScreen,
                requestedDurationSeconds = CaptureDurationSeconds
            };
            WriteSessionStats("preparing");
            _preparedTarget = new RenderTexture(
                _renderWidth,
                _renderHeight,
                24,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            _preparedTarget.antiAliasing = antiAliasingSamples;
            _preparedTarget.Create();
            _staticPreparedTarget = new RenderTexture(
                _renderWidth,
                _renderHeight,
                24,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            _staticPreparedTarget.antiAliasing = antiAliasingSamples;
            _staticPreparedTarget.Create();
            _previousMainTarget = _camera.targetTexture;
            _previousOverlayTarget = _overlayCamera.targetTexture;
            _previousFixedTarget = _staticCamera.targetTexture;
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

            if (_recordMain) _recorder.StartPngSequence(
                _overlayCamera,
                CreatePngSequenceSettings(
                    Path.Combine(directory, $"{baseName}_MainCamera_Frames"),
                    width,
                    height,
                    antiAliasingSamples,
                    0.0),
                _preparedTarget);
            if (_recordFixed) _staticRecorder.StartPngSequence(
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
                if (_recordMain) _recorder.StartRecording(
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
                if (_recordFixed) _staticRecorder.StartRecording(
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
                if (_recordScreen)
                {
                    RecordingSettings screenSettings = CreateRecordingSettings(directory,
                        $"{baseName}_Screen", width, height,
                        frameRate, 1);
                    screenSettings.CaptureScreen = true;
                    screenSettings.FlipVertically = false;
                    _screenRecorder.StartRecording(_camera,
                        _camera.GetComponent<AudioListener>(), screenSettings);
                }
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
                StatusText = "Finalizing MP4 files — packaging video and audio…";
                if (_recordMain) _recorder.StopRecording();
                if (_recordFixed) _staticRecorder.StopRecording();
                if (_recordScreen) _screenRecorder.StopRecording();
            }
            else
            {
                ReleasePreparedTarget();
            }
        }

        // Creates one independent output configuration for a synchronized camera recording.
        private RecordingSettings CreateRecordingSettings(
            string directory,
            string baseName,
            int width,
            int height,
            int frameRate,
            int antiAliasingSamples)
        {
            _videoFiles.Add(Path.Combine(directory, $"{baseName}.mp4"));
            return new RecordingSettings
            {
                FfmpegPath = Environment.GetEnvironmentVariable("FFMPEG_PATH") ?? _ffmpegPath,
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
                NativeEncodingPreset = _encodingPreset,
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
            if (_startedRecorderCount < _expectedRecorderCount)
            {
                return;
            }

            BeginMeasurement();
            WriteSessionStats("recording");
            StatusText = $"Recording — NVIDIA video encoding • {_width} × {_height} • up to {_frameRate} FPS • {_expectedRecorderCount} output(s)";
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

        // Reports the completed files produced next to the application.
        private void HandleRecordingCompleted()
        {
            _completedRecorderCount++;
            if (_completedRecorderCount < _expectedRecorderCount)
            {
                return;
            }

            HandleFinalizationStarted();
            WriteSessionStats("completed");

            float elapsed = Math.Max(0.001f, Time.realtimeSinceStartup - _captureStartTime);
            float mainAverageFps = _diagnostics.MainRenderedFrames / elapsed;
            float staticAverageFps = _diagnostics.StaticRenderedFrames / elapsed;
            Debug.Log(
                $"Unity render averages over {elapsed:0.000} s: " +
                $"main camera={mainAverageFps:0.00} FPS ({_diagnostics.MainRenderedFrames} frames), " +
                $"static camera={staticAverageFps:0.00} FPS ({_diagnostics.StaticRenderedFrames} frames).");
            Debug.Log("Sample capture completed in " + Path.Combine(Path.GetDirectoryName(Application.dataPath), "output"));
            StatusText = "Recording complete — MP4 files saved to output";
            _diagnostics.EndMeasurement();
            ReleasePreparedTarget();
        }

        // Reports a recording failure through the Unity console.
        private void HandleRecordingFailed(Exception exception)
        {
            Debug.LogException(exception);
            HandleFinalizationStarted();
            WriteSessionStats("failed", exception.Message);
            StatusText = "Recording failed — see the Unity Console for details";
            StopAllCoroutines();
            _diagnostics.EndMeasurement();
            ReleasePreparedTarget();
        }

        // Freezes render counters when input capture ends, excluding MP4 finalization from render averages.
        private void HandleFinalizationStarted()
        {
            if (_measurementEnded || _sessionStats == null) return;
            _measurementEnded = true;
            _finalizationStartTime = Time.realtimeSinceStartup;
            _diagnostics.EndMeasurement();
            float elapsed = _captureStarted ? Mathf.Max(0, _finalizationStartTime - _captureStartTime) : 0;
            _sessionStats.captureDurationSeconds = elapsed;
            _sessionStats.camera1RenderedFrames = _diagnostics.MainRenderedFrames;
            _sessionStats.camera2RenderedFrames = _diagnostics.StaticRenderedFrames;
            _sessionStats.camera1AverageRenderFps = elapsed > 0 ? _sessionStats.camera1RenderedFrames / elapsed : 0;
            _sessionStats.camera2AverageRenderFps = elapsed > 0 ? _sessionStats.camera2RenderedFrames / elapsed : 0;
            WriteSessionStats("finalizing");
        }

        // Writes a readable session report without letting a statistics I/O error interrupt recording.
        private void WriteSessionStats(string status, string error = null)
        {
            if (_sessionStats == null) return;
            _sessionStats.status = status;
            _sessionStats.error = error;
            _sessionStats.videoFiles = _videoFiles.ToArray();
            _sessionStats.videoFileBytes = new long[_videoFiles.Count];
            if (status == "completed" || status == "failed")
            {
                _sessionStats.finishedUtc = DateTime.UtcNow.ToString("O");
                _sessionStats.finalizationDurationSeconds = _measurementEnded ? Time.realtimeSinceStartup - _finalizationStartTime : 0;
            }
            try
            {
                for (int i = 0; i < _videoFiles.Count; i++)
                    if (File.Exists(_videoFiles[i])) _sessionStats.videoFileBytes[i] = new FileInfo(_videoFiles[i]).Length;
                File.WriteAllText(_statsPath, JsonUtility.ToJson(_sessionStats, true));
            }
            catch (Exception exception) { Debug.LogWarning("Cannot write session statistics: " + exception.Message); }
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
                _camera.targetTexture = _previousMainTarget;
            }
            if (_overlayCamera != null && _overlayCamera.targetTexture == _preparedTarget)
            {
                _overlayCamera.targetTexture = _previousOverlayTarget;
            }
            if (_staticCamera != null && _staticCamera.targetTexture == _staticPreparedTarget)
            {
                _staticCamera.targetTexture = _previousFixedTarget;
            }
            _preparedTarget.Release();
            Destroy(_preparedTarget);
            _preparedTarget = null;
            IsCapturing = false;
            if (_staticPreparedTarget != null)
            {
                _staticPreparedTarget.Release();
                Destroy(_staticPreparedTarget);
                _staticPreparedTarget = null;
            }
        }
    }
}
