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
        [SerializeField]
        private string _mode = "dual-nvenc";
        private string _ffmpegPath;
        [SerializeField, Min(2)]
        private int _width = 3840;
        [SerializeField, Min(2)]
        private int _height = 2160;
        [SerializeField, Range(1, 60)]
        private int _frameRate = 60;
        [SerializeField]
        private int _msaa = 4;
        private int _renderWidth = 2560, _renderHeight = 1440;
        private RecordingQualityPreset _qualityPreset = RecordingQualityPreset.High;
        public void SetQualityPreset(RecordingQualityPreset preset)
        {
            if (!IsCapturing) _qualityPreset = preset;
        }
        private FFmpegMediaWriter.VideoStreamFormat _videoCodec = FFmpegMediaWriter.VideoStreamFormat.H264;
        [SerializeField, Min(0.1f)]
        private float CaptureDurationSeconds = 10f;
        private bool _stopAfterDuration;
        private Camera _camera;
        private Camera _overlayCamera;
        private Camera _staticCamera;
        private OrbitCamera _orbitCamera;
        private TemporalMotionSmoothing _mainTemporalSmoothing;
        private TemporalMotionSmoothing _staticTemporalSmoothing;
        private SampleDiagnostics _diagnostics;
        private bool _recordMain = true;
        private bool _recordFixed = true;
        private bool _recordScreen = true;
        private UnityMediaRecorder _screenRecorder;
        private int _expectedRecorderCount = 1;
        private RenderTexture _switchingTarget, _groundTarget, _previousGroundTarget;
        private Camera _groundCamera, _outputCamera;
        private readonly List<RenderTexture> _switchingSources = new List<RenderTexture>();
        private const float CameraSwitchSeconds = 4f;

        private UnityEngine.Rendering.CommandBuffer _compositionCommands;
        private int _compositionIndex = -1;

        // Composes the selected source on the recorder camera's actual GPU target.
        private void LateUpdate()
        {
            if (_outputCamera == null || _switchingSources.Count == 0) return;
            int index = ActiveSourceIndex;
            if (_compositionIndex == index) return;
            _compositionIndex = index;
            _compositionCommands.Clear();
            _compositionCommands.Blit(_switchingSources[index], UnityEngine.Rendering.BuiltinRenderTextureType.CameraTarget);
        }
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
        private int ActiveSourceIndex => _switchingSources.Count == 0 ? 0 : _captureStarted ? (int)((Time.realtimeSinceStartup - _captureStartTime) / CameraSwitchSeconds) % _switchingSources.Count : 0;
        public bool IsScreenRecording => IsCapturing && !_measurementEnded && _recordScreen && _switchingSources.Count > 0 && ActiveSourceIndex == _switchingSources.Count - 1;

        // Samples the final application image, including overlay UI, before the encoder samples its target.
        private IEnumerator CaptureScreenSource()
        {
            var endOfFrame = new WaitForEndOfFrame();
            while (IsCapturing && !_measurementEnded)
            {
                yield return endOfFrame;
                if (!IsScreenRecording || _outputCamera == null) continue;
                if (_groundTarget == null || _groundTarget.width != Screen.width || _groundTarget.height != Screen.height)
                {
                    if (_groundTarget != null) { _groundTarget.Release(); Destroy(_groundTarget); }
                    _groundTarget = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                    _groundTarget.Create();
                    _switchingSources[_switchingSources.Count - 1] = _groundTarget;
                    _compositionIndex = -1;
                }
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = null;
                ScreenCapture.CaptureScreenshotIntoRenderTexture(_groundTarget);
                // Screen capture is upright already; compensate for the recorder's camera-target flip.
                if (SystemInfo.graphicsUVStartsAtTop)
                    Graphics.Blit(_groundTarget, _outputCamera.targetTexture, new Vector2(1f, -1f), new Vector2(0f, 1f));
                else
                    Graphics.Blit(_groundTarget, _outputCamera.targetTexture);
                RenderTexture.active = previous;
            }
        }

        // Selects the output codec before a new recording session begins.
        public void SetVideoCodec(FFmpegMediaWriter.VideoStreamFormat codec)
        {
            if (!IsCapturing)
            {
                _videoCodec = codec;
            }
        }

        // Changes automatic recording duration before a session starts.
        public void SetDuration(float seconds)
        {
            if (!IsCapturing)
            {
                CaptureDurationSeconds = seconds;
                _stopAfterDuration = true;
            }
        }

        // Selects the sample count for recording targets before a session starts.
        public void SetAntiAliasing(int samples)
        {
            if (!IsCapturing)
            {
                _msaa = samples;
            }
        }

        // Changes video output dimensions and frame-rate ceiling only between sessions.
        public void SetOutputSettings(int width, int height, int frameRate)
        {
            if (IsCapturing)
            {
                return;
            }

            _width = width;
            _height = height;
            _frameRate = frameRate;
        }

        // Selects camera render dimensions independently of encoded video dimensions.
        public void SetRenderResolution(int width, int height)
        {
            if (IsCapturing)
            {
                return;
            }

            _renderWidth = width;
            _renderHeight = height;
        }

        // Connects scene-owned cameras to independent recording sessions.
        public void Configure(Camera sceneCamera, Camera overlayCamera, Camera fixedCamera, OrbitCamera orbit, SampleDiagnostics diagnostics)
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
            if (IsCapturing)
            {
                return;
            }

            if (string.IsNullOrEmpty(_benchmarkMode) || _benchmarkMode.StartsWith("dual-nvenc", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    _ffmpegPath = FfmpegEnvironment.GetExecutablePath();
                }
                catch (Exception exception)
                {
                    StatusText = "Recording failed — " + exception.Message;
                    Debug.LogError(StatusText);
                    return;
                }
            }

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
            if (IsCapturing || (!camera1 && !camera2 && !screen))
            {
                return;
            }

            _recordMain = camera1;
            _recordFixed = camera2;
            _recordScreen = screen;
            _expectedRecorderCount = 1;
            _benchmarkMode = "dual-nvenc";
            BeginCapture();
        }

        // Stops active video inputs and lets their independent writers finalize the files.
        public void StopCapture()
        {
            if (!IsCapturing)
            {
                return;
            }

            StatusText = "Finalizing MP4 file — packaging video and audio…";
            HandleFinalizationStarted();
            StopAllCoroutines();
            _recorder.StopRecording();
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
            if (recorder == null)
            {
                return;
            }

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
            int width = ReadPositiveEnvironmentInteger("CAPTURE_WIDTH", _width);
            int height = ReadPositiveEnvironmentInteger("CAPTURE_HEIGHT", _height);
            int frameRate = ReadPositiveEnvironmentInteger("CAPTURE_FRAME_RATE", _frameRate);
            int antiAliasingSamples = ReadPositiveEnvironmentInteger("CAPTURE_MSAA", _msaa);
            _diagnostics.ConfigureCapture(width, height, antiAliasingSamples, _benchmarkMode.StartsWith("dual-nvenc", StringComparison.OrdinalIgnoreCase) ? $"{frameRate} FPS" : "1 PNG/s");
            string profileName = Environment.GetEnvironmentVariable("CAPTURE_PROFILE") ?? $"{width}x{height}_{frameRate}fps";
            string baseName = $"UnitySample_{profileName}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}";
            _statsPath = Path.Combine(directory, baseName + "_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".stats.json");
            _sessionStats = new RecordingSessionStats
            {
                status = "preparing",
                startedUtc = DateTime.UtcNow.ToString("O"),
                gpu = SystemInfo.graphicsDeviceName,
                unityVersion = Application.unityVersion,
                renderWidth = _renderWidth,
                renderHeight = _renderHeight,
                outputWidth = width,
                outputHeight = height,
                maximumVideoFps = frameRate,
                msaaSamples = antiAliasingSamples,
                vSyncCount = QualitySettings.vSyncCount,
                encodingPreset = RecordingQualityProfile.FromPreset(_qualityPreset, width, height, frameRate).NativeEncodingPreset,
                qualityPreset = _qualityPreset.ToString(),
                rateControl = "cqp",
                quantizationParameter = RecordingQualityProfile.FromPreset(_qualityPreset, width, height, frameRate).QuantizationParameter,
                videoBitRate = RecordingQualityProfile.FromPreset(_qualityPreset, width, height, frameRate).VideoBitRate,
                maximumVideoBitRate = RecordingQualityProfile.FromPreset(_qualityPreset, width, height, frameRate).MaximumVideoBitRate,
                audioCodec = "aac",
                audioSampleRate = 48000,
                audioChannels = 2,
                audioBitRate = RecordingQualityProfile.FromPreset(_qualityPreset, width, height, frameRate).AudioBitRate,
                videoCodec = _videoCodec == FFmpegMediaWriter.VideoStreamFormat.Hevc ? "hevc" : "h264",
                camera1 = _recordMain,
                camera2 = _recordFixed,
                screen = _recordScreen,
                requestedDurationSeconds = _stopAfterDuration ? CaptureDurationSeconds : 0
            };
            WriteSessionStats("preparing");
            _preparedTarget = new RenderTexture(_renderWidth, _renderHeight, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            _preparedTarget.antiAliasing = antiAliasingSamples;
            _preparedTarget.Create();
            _staticPreparedTarget = new RenderTexture(_renderWidth, _renderHeight, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
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
                yield return RunBenchmarkMode(directory, baseName, width, height, frameRate, antiAliasingSamples);
                yield break;
            }

            if (_recordMain)
            {
                _recorder.StartPngSequence(_overlayCamera, CreatePngSequenceSettings(Path.Combine(directory, $"{baseName}_MainCamera_Frames"), width, height, antiAliasingSamples, 0.0), _preparedTarget);
            }

            if (_recordFixed)
            {
                _staticRecorder.StartPngSequence(_staticCamera, CreatePngSequenceSettings(Path.Combine(directory, $"{baseName}_StaticCamera_Frames"), width, height, antiAliasingSamples, 0.5), _staticPreparedTarget);
            }

            while (!_captureStarted)
            {
                yield return null;
            }

            if (!_stopAfterDuration)
            {
                while (IsCapturing && !_measurementEnded) yield return null;
                yield break;
            }
            yield return new WaitForSecondsRealtime(CaptureDurationSeconds);
            _recorder.StopPngSequence();
            _staticRecorder.StopPngSequence();
        }

        // Runs one controlled render or NVENC benchmark selected through the environment.
        private IEnumerator RunBenchmarkMode(string directory, string baseName, int width, int height, int frameRate, int antiAliasingSamples)
        {
            bool singleCamera = string.Equals(_benchmarkMode, "single-render", StringComparison.OrdinalIgnoreCase);
            bool useNvenc = _benchmarkMode.StartsWith("dual-nvenc", StringComparison.OrdinalIgnoreCase);
            bool disableTemporal = string.Equals(_benchmarkMode, "dual-nvenc-no-temporal", StringComparison.OrdinalIgnoreCase);
            _staticCamera.enabled = !singleCamera;
            if (disableTemporal)
            {
                _mainTemporalSmoothing.enabled = false;
                _staticTemporalSmoothing.enabled = false;
            }

            if (useNvenc)
            {
                _switchingTarget = new RenderTexture(_renderWidth, _renderHeight, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                _switchingTarget.Create();
                _groundCamera = _diagnostics.GetComponent<CameraViewSwitcher>().ScreenCamera;
                _switchingSources.Clear();
                if (_recordMain) _switchingSources.Add(_preparedTarget);
                if (_recordFixed) _switchingSources.Add(_staticPreparedTarget);
                if (_recordScreen)
                {
                    if (_groundCamera == null)
                    {
                        HandleRecordingFailed(new InvalidOperationException("The ground camera is not configured."));
                        yield break;
                    }
                    _previousGroundTarget = _groundCamera.targetTexture;
                    _groundTarget = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                    _groundTarget.Create();
                    _groundCamera.targetTexture = null;
                    _switchingSources.Add(_groundTarget);
                }
                var outputObject = new GameObject("RecordingOutputCamera");
                _outputCamera = outputObject.AddComponent<Camera>();
                _outputCamera.depth = 1000;
                _outputCamera.cullingMask = 0;
                _outputCamera.clearFlags = CameraClearFlags.Nothing;
                _outputCamera.targetTexture = _switchingTarget;
                _compositionCommands = new UnityEngine.Rendering.CommandBuffer { name = "Compose recording camera" };
                _compositionIndex = -1;
                _outputCamera.AddCommandBuffer(UnityEngine.Rendering.CameraEvent.AfterEverything, _compositionCommands);
                LateUpdate();
                if (_recordScreen) StartCoroutine(CaptureScreenSource());
                _recorder.StartRecording(_outputCamera, _camera.GetComponent<AudioListener>(), CreateRecordingSettings(directory, $"{baseName}_CameraCycle", width, height, frameRate, 1), _switchingTarget);

                while (!_captureStarted)
                {
                    yield return null;
                }
            }
            else
            {
                BeginMeasurement();
            }

            if (!_stopAfterDuration)
            {
                while (IsCapturing && !_measurementEnded) yield return null;
                yield break;
            }
            yield return new WaitForSecondsRealtime(CaptureDurationSeconds);
            ReportBenchmarkMeasurement();
            if (useNvenc)
            {
                StatusText = "Finalizing MP4 file — packaging video and audio…";
                StopCapture();
            }
            else
            {
                ReleasePreparedTarget();
            }
        }

        // Creates one independent output configuration for a synchronized camera recording.
        private RecordingSettings CreateRecordingSettings(string directory, string baseName, int width, int height, int frameRate, int antiAliasingSamples)
        {
            _videoFiles.Add(Path.Combine(directory, $"{baseName}.mp4"));
            return new RecordingSettings
            {
                FfmpegPath = _ffmpegPath,
                TemporaryContainerPath = Path.Combine(directory, $"{baseName}.mkv.tmp"),
                ArchivePath = Path.Combine(directory, $"{baseName}.mkv"),
                KeepIntermediateFile = Environment.GetEnvironmentVariable("CAPTURE_KEEP_INTERMEDIATE") == "1",
                GeneratePreviewImage = false,
                OutputPath = Path.Combine(directory, $"{baseName}.mp4"),
                Width = width,
                Height = height,
                MaximumFrameRate = frameRate,
                AntiAliasingSamples = antiAliasingSamples,
                QualityPreset = _qualityPreset,
                VideoStreamFormat = _videoCodec,
                FlipVertically = SystemInfo.graphicsUVStartsAtTop
            };
        }

        // Creates a one-image-per-second PNG sequence matching its associated video output.
        private static PngSequenceSettings CreatePngSequenceSettings(string outputDirectory, int width, int height, int antiAliasingSamples, double initialDelaySeconds)
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
            Debug.Log($"CAPTURE_BENCHMARK mode={_benchmarkMode}; elapsed={elapsed:0.000}; " + $"mainFps={mainAverageFps:0.00}; mainFrames={_diagnostics.MainRenderedFrames}; " + $"staticFps={staticAverageFps:0.00}; staticFrames={_diagnostics.StaticRenderedFrames}");
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
            _sessionStats.videoDiagnosticsJson = _recorder?.LastVideoDiagnosticsJson;
            WriteSessionStats("completed");
            float elapsed = Math.Max(0.001f, Time.realtimeSinceStartup - _captureStartTime);
            float mainAverageFps = _diagnostics.MainRenderedFrames / elapsed;
            float staticAverageFps = _diagnostics.StaticRenderedFrames / elapsed;
            Debug.Log($"Unity render averages over {elapsed:0.000} s: " + $"main camera={mainAverageFps:0.00} FPS ({_diagnostics.MainRenderedFrames} frames), " + $"static camera={staticAverageFps:0.00} FPS ({_diagnostics.StaticRenderedFrames} frames).");
            Debug.Log("Sample capture completed in " + Path.Combine(Path.GetDirectoryName(Application.dataPath), "output"));
            StatusText = "Recording complete — MP4 file saved to output";
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
            if (_measurementEnded || _sessionStats == null)
            {
                return;
            }

            _measurementEnded = true;
            _finalizationStartTime = Time.realtimeSinceStartup;
            _diagnostics.EndMeasurement();
            float elapsed = _captureStarted ? Mathf.Max(0, _finalizationStartTime - _captureStartTime) : 0;
            _sessionStats.captureDurationSeconds = elapsed;
            _sessionStats.camera1RenderedFrames = _diagnostics.MainRenderedFrames;
            _sessionStats.camera2RenderedFrames = _diagnostics.StaticRenderedFrames;
            _sessionStats.screenRenderedFrames = _diagnostics.ScreenRenderedFrames;
            _sessionStats.camera1AverageRenderFps = elapsed > 0 ? _sessionStats.camera1RenderedFrames / elapsed : 0;
            _sessionStats.camera2AverageRenderFps = elapsed > 0 ? _sessionStats.camera2RenderedFrames / elapsed : 0;
            _sessionStats.screenAverageRenderFps = elapsed > 0 ? _sessionStats.screenRenderedFrames / elapsed : 0;
            WriteSessionStats("finalizing");
        }

        // Writes a readable session report without letting a statistics I/O error interrupt recording.
        private void WriteSessionStats(string status, string error = null)
        {
            if (_sessionStats == null)
            {
                return;
            }

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
                {
                    if (File.Exists(_videoFiles[i]))
                    {
                        _sessionStats.videoFileBytes[i] = new FileInfo(_videoFiles[i]).Length;
                    }
                }

                File.WriteAllText(_statsPath, JsonUtility.ToJson(_sessionStats, true));
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Cannot write session statistics: " + exception.Message);
            }
        }

        // Releases the camera target owned by this example after recording ends.
        private void ReleasePreparedTarget()
        {
            if (_compositionCommands != null)
            {
                if (_outputCamera != null) _outputCamera.RemoveCommandBuffer(UnityEngine.Rendering.CameraEvent.AfterEverything, _compositionCommands);
                _compositionCommands.Release(); _compositionCommands = null;
            }
            if (_outputCamera != null) _outputCamera.targetTexture = null;
            if (_outputCamera != null) { Destroy(_outputCamera.gameObject); _outputCamera = null; }
            if (_groundTarget != null)
            {
                if (_groundCamera != null && _groundCamera.targetTexture == null) _groundCamera.targetTexture = _previousGroundTarget;
                _groundTarget.Release(); Destroy(_groundTarget); _groundTarget = null;
            }
            if (_switchingTarget != null) { _switchingTarget.Release(); Destroy(_switchingTarget); _switchingTarget = null; }
            _switchingSources.Clear();
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
