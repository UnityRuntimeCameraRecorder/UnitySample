using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace UnityRuntimeCameraRecorder.Example
{
    // Coordinates capture sessions without constructing scenery or overlays.
    public sealed class SampleCaptureController : MonoBehaviour
    {
        private const float VideoFpsWindowStartSeconds = 1f;
        private const float MaximumVideoFpsMeasurementSeconds = 5f;

        // Carries FFprobe results from a worker thread back to the Unity thread.
        private struct VideoMeasurements
        {
            // Allocates one measurement slot for every completed video.
            public VideoMeasurements(int count)
            {
                EncodedFrames = new long[count];
                MeasuredFrames = new long[count];
                DurationSeconds = new float[count];
                MeasuredDurationSeconds = new float[count];
                ActualFps = new float[count];
                WindowEndSeconds = 0;
            }

            public long[] EncodedFrames;
            public long[] MeasuredFrames;
            public float[] DurationSeconds;
            public float[] MeasuredDurationSeconds;
            public float[] ActualFps;
            public float WindowEndSeconds;
        }
        [SerializeField]
        private string _mode = "dual-nvenc";
        private string _ffmpegDirectory;
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
            if (!IsCapturing)
            {
                _qualityPreset = preset;
            }
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
        private bool _singleVideoOutput;
        private bool _generateStatistics;
        private UnityRuntimeCameraRecorder _screenRecorder;
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
        public bool IsScreenRecording => IsCapturing && !_measurementEnded && _recordScreen;

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
        private UnityRuntimeCameraRecorder CreateRecorder()
        {
            var recorder = gameObject.AddComponent<UnityRuntimeCameraRecorder>();
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
                    _ffmpegDirectory = FfmpegEnvironment.GetBinDirectory();
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
        public void BeginCapture(bool camera1, bool camera2, bool screen = false, bool singleVideoOutput = false)
        {
            if (IsCapturing || (!camera1 && !camera2 && !screen))
            {
                return;
            }

            _recordMain = camera1;
            _recordFixed = camera2;
            _recordScreen = screen;
            int selectedSources = (camera1 ? 1 : 0) + (camera2 ? 1 : 0) + (screen ? 1 : 0);
            _singleVideoOutput = singleVideoOutput && selectedSources >= 2;
            _expectedRecorderCount = _singleVideoOutput ? 1 : (camera1 ? 1 : 0) + (camera2 ? 1 : 0) + (screen ? 1 : 0);
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

            StatusText = "Finalizing MP4 files — packaging video and audio…";
            HandleFinalizationStarted();
            StopAllCoroutines();
            StopSelectedRecorders();
        }

        // Enables per-video statistics for subsequent recording sessions.
        public void SetGenerateStatistics(bool enabled)
        {
            if (!IsCapturing)
            {
                _generateStatistics = enabled;
            }
        }

        // Stops each selected recorder so its writer can finalize independently.
        private void StopSelectedRecorders()
        {
            if (_singleVideoOutput)
            {
                _recorder.StopRecording();
                return;
            }
            if (_recordMain)
            {
                _recorder.StopRecording();
            }

            if (_recordFixed)
            {
                _staticRecorder.StopRecording();
            }

            if (_recordScreen)
            {
                _screenRecorder.StopRecording();
            }
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
        private void DisconnectRecorder(UnityRuntimeCameraRecorder recorder)
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
        private UnityRuntimeCameraRecorder _recorder;
        private UnityRuntimeCameraRecorder _staticRecorder;
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
                encodingPreset = _expectedRecorderCount >= 2 ? 4 : RecordingQualityProfile.FromPreset(_qualityPreset, width, height, frameRate).NativeEncodingPreset,
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
                _recorder.StartImageSequence(_overlayCamera, CreateImageSequenceSettings(Path.Combine(directory, $"{baseName}_MainCamera_Frames"), width, height, antiAliasingSamples, 0.0), _preparedTarget);
            }

            if (_recordFixed)
            {
                _staticRecorder.StartImageSequence(_staticCamera, CreateImageSequenceSettings(Path.Combine(directory, $"{baseName}_StaticCamera_Frames"), width, height, antiAliasingSamples, 0.5), _staticPreparedTarget);
            }

            while (!_captureStarted)
            {
                yield return null;
            }

            if (!_stopAfterDuration)
            {
                while (IsCapturing && !_measurementEnded)
                {
                    yield return null;
                }
                yield break;
            }
            yield return new WaitForSecondsRealtime(CaptureDurationSeconds);
            _recorder.StopImageSequence();
            _staticRecorder.StopImageSequence();
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
                StartSelectedRecorders(directory, baseName, width, height, frameRate, antiAliasingSamples);

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
                while (IsCapturing && !_measurementEnded)
                {
                    yield return null;
                }
                yield break;
            }
            yield return new WaitForSecondsRealtime(CaptureDurationSeconds);
            ReportBenchmarkMeasurement();
            if (useNvenc)
            {
                StatusText = "Finalizing MP4 files — packaging video and audio…";
                StopCapture();
            }
            else
            {
                ReleasePreparedTarget();
            }
        }

        // Starts one independent recorder for every selected video source.
        private void StartSelectedRecorders(string directory, string baseName, int width, int height, int frameRate, int antiAliasingSamples)
        {
            AudioListener listener = _camera.GetComponent<AudioListener>();
            if (_singleVideoOutput)
            {
                RecordingSettings settings = CreateRecordingSettings(directory, $"{baseName}_VideoSequence", width, height, frameRate, antiAliasingSamples);
                var sources = new List<VideoSequenceSource>();
                if (_recordMain)
                {
                    sources.Add(VideoSequenceSource.FromCamera(_camera));
                }
                if (_recordFixed)
                {
                    sources.Add(VideoSequenceSource.FromCamera(_staticCamera));
                }
                if (_recordScreen)
                {
                    sources.Add(VideoSequenceSource.FromScreen());
                }
                var sequence = new VideoSequenceSettings
                {
                    Sources = sources,
                    Order = VideoSequenceOrder.Random,
                    MinimumShotDurationSeconds = 4f,
                    MaximumShotDurationSeconds = 8f,
                    CrossFadeDurationSeconds = 0.5f,
                    Transitions = new[]
                    {
                        VideoSequenceTransition.CrossFade,
                        VideoSequenceTransition.NoTransition
                    }
                };
                _recorder.StartRecording(sequence, listener, settings);
                return;
            }
            if (_recordMain)
            {
                RecordingSettings settings = CreateRecordingSettings(directory, $"{baseName}_MainCamera", width, height, frameRate, antiAliasingSamples);
                _recorder.StartRecording(CreateSingleSourceSequence(VideoSequenceSource.FromCamera(_overlayCamera)), listener, settings);
            }

            if (_recordFixed)
            {
                RecordingSettings settings = CreateRecordingSettings(directory, $"{baseName}_StaticCamera", width, height, frameRate, antiAliasingSamples);
                _staticRecorder.StartRecording(CreateSingleSourceSequence(VideoSequenceSource.FromCamera(_staticCamera)), listener, settings);
            }

            if (_recordScreen)
            {
                RecordingSettings settings = CreateRecordingSettings(directory, $"{baseName}_Screen", width, height, frameRate, antiAliasingSamples);
                _screenRecorder.StartRecording(CreateSingleSourceSequence(VideoSequenceSource.FromScreen()), listener, settings);
            }
        }

        // Wraps one explicit source in the same configuration used by multi-source recordings.
        private static VideoSequenceSettings CreateSingleSourceSequence(VideoSequenceSource source)
        {
            return new VideoSequenceSettings
            {
                Sources = new[] { source }
            };
        }

        // Creates one independent output configuration for a synchronized camera recording.
        private RecordingSettings CreateRecordingSettings(string directory, string baseName, int width, int height, int frameRate, int antiAliasingSamples)
        {
            _videoFiles.Add(Path.Combine(directory, $"{baseName}.mp4"));
            return new RecordingSettings
            {
                FfmpegPath = Path.Combine(_ffmpegDirectory, "ffmpeg.exe"),
                TemporaryContainerPath = Path.Combine(directory, $"{baseName}.mkv.tmp"),
                GenerateStatistics = _generateStatistics,
                OutputPath = Path.Combine(directory, $"{baseName}.mp4"),
                Width = width,
                Height = height,
                MaximumFrameRate = frameRate,
                SourceAntiAliasingSamples = antiAliasingSamples,
                QualityPreset = _qualityPreset,
                VideoStreamFormat = _videoCodec,
                OptimizeForConcurrentEncoding = _expectedRecorderCount >= 2
            };
        }

        // Creates a one-image-per-second image sequence matching its associated video output.
        private static ImageSequenceSettings CreateImageSequenceSettings(string outputDirectory, int width, int height, int antiAliasingSamples, double initialDelaySeconds)
        {
            return new ImageSequenceSettings
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
            if (_startedRecorderCount != _expectedRecorderCount)
            {
                return;
            }

            BeginMeasurement();
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
            if (_completedRecorderCount != _expectedRecorderCount)
            {
                return;
            }

            FinishCompletedRecording();
        }

        // Waits for background FFprobe work while keeping the Unity frame loop responsive.
        private IEnumerator CompleteRecordingWithoutBlocking()
        {
            float statisticsStartTime = Time.realtimeSinceStartup;
            string[] videoFiles = _videoFiles.ToArray();
            string probePath = Path.Combine(_ffmpegDirectory, "ffprobe.exe");
            float requestedDuration = _sessionStats.requestedDurationSeconds;
            Task<VideoMeasurements> task = Task.Run(() => MeasureCompletedVideos(probePath, videoFiles, requestedDuration));
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                Debug.LogWarning("Cannot measure completed videos: " + task.Exception?.GetBaseException().Message);
            }
            else
            {
                ApplyVideoMeasurements(task.Result);
            }

            _sessionStats.statisticsGenerationDurationSeconds = Time.realtimeSinceStartup - statisticsStartTime;
            FinishCompletedRecording();
        }

        // Writes final statistics and releases recording resources on the Unity thread.
        private void FinishCompletedRecording()
        {
            float elapsed = Math.Max(0.001f, Time.realtimeSinceStartup - _captureStartTime);
            float mainAverageFps = _diagnostics.MainRenderedFrames / elapsed;
            float staticAverageFps = _diagnostics.StaticRenderedFrames / elapsed;
            Debug.Log($"Unity render averages over {elapsed:0.000} s: " + $"main camera={mainAverageFps:0.00} FPS ({_diagnostics.MainRenderedFrames} frames), " + $"static camera={staticAverageFps:0.00} FPS ({_diagnostics.StaticRenderedFrames} frames).");
            Debug.Log("Sample capture completed in " + Path.Combine(Path.GetDirectoryName(Application.dataPath), "output"));
            StatusText = "Recording complete — MP4 files saved to output";
            _diagnostics.EndMeasurement();
            ReleasePreparedTarget();
        }

        // Measures completed MP4 files on a worker thread and returns plain data.
        private static VideoMeasurements MeasureCompletedVideos(string probePath, string[] videoFiles, float requestedDuration)
        {
            var result = new VideoMeasurements(videoFiles.Length);
            float maximumEnd = VideoFpsWindowStartSeconds + MaximumVideoFpsMeasurementSeconds;
            float requestedEnd = requestedDuration > VideoFpsWindowStartSeconds ? Math.Min(requestedDuration, maximumEnd) : 0;
            result.WindowEndSeconds = requestedEnd > 0 ? requestedEnd : maximumEnd;
            Parallel.For(0, videoFiles.Length, i =>
            {
                if (!TryProbeVideo(probePath, videoFiles[i], requestedEnd, out long frames, out long measuredFrames, out float duration, out float measuredDuration))
                {
                    return;
                }

                result.EncodedFrames[i] = frames;
                result.MeasuredFrames[i] = measuredFrames;
                result.DurationSeconds[i] = duration;
                result.MeasuredDurationSeconds[i] = measuredDuration;
                result.ActualFps[i] = measuredFrames / measuredDuration;
            });

            return result;
        }

        // Copies worker results into the serializable session report on the Unity thread.
        private void ApplyVideoMeasurements(VideoMeasurements result)
        {
            _sessionStats.videoEncodedFrames = result.EncodedFrames;
            _sessionStats.videoMeasuredFrames = result.MeasuredFrames;
            _sessionStats.videoDurationSeconds = result.DurationSeconds;
            _sessionStats.videoMeasuredDurationSeconds = result.MeasuredDurationSeconds;
            _sessionStats.videoActualFps = result.ActualFps;
            _sessionStats.videoFpsWindowStartSeconds = VideoFpsWindowStartSeconds;
            _sessionStats.videoFpsWindowEndSeconds = result.WindowEndSeconds;
        }

        // Reads actual MP4 stream values from FFprobe without changing the recording.
        private static bool TryProbeVideo(string probePath, string videoPath, float requestedEnd, out long frames, out long measuredFrames, out float duration, out float measuredDuration)
        {
            frames = 0;
            measuredFrames = 0;
            duration = 0;
            measuredDuration = 0;
            if (!File.Exists(probePath) || !File.Exists(videoPath))
            {
                return false;
            }

            var startInfo = new System.Diagnostics.ProcessStartInfo(probePath)
            {
                Arguments = CreateProbeArguments(videoPath, requestedEnd),
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            using (System.Diagnostics.Process process = System.Diagnostics.Process.Start(startInfo))
            {
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return process.ExitCode == 0 && TryParseProbeOutput(output, requestedEnd, out frames, out measuredFrames, out duration, out measuredDuration);
            }
        }

        // Limits FFprobe input to the short interval used by the FPS calculation.
        private static string CreateProbeArguments(string videoPath, float requestedEnd)
        {
            float maximumEnd = VideoFpsWindowStartSeconds + MaximumVideoFpsMeasurementSeconds;
            float probeEnd = requestedEnd > VideoFpsWindowStartSeconds ? requestedEnd : maximumEnd;
            string interval = probeEnd.ToString(CultureInfo.InvariantCulture);
            return "-v error -read_intervals 0%" + interval + " -select_streams v:0 " +
                "-show_entries stream=duration,nb_frames:frame=best_effort_timestamp_time " +
                "-of default=noprint_wrappers=1 " + QuoteArgument(videoPath);
        }

        // Counts all frames and those after the first second until the requested end.
        private static bool TryParseProbeOutput(string output, float requestedEnd, out long frames, out long measuredFrames, out float duration, out float measuredDuration)
        {
            frames = 0;
            measuredFrames = 0;
            duration = 0;
            measuredDuration = 0;
            var timestamps = new List<float>();
            foreach (string line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.StartsWith("best_effort_timestamp_time=", StringComparison.Ordinal))
                {
                    if (float.TryParse(line.Substring(27), NumberStyles.Float, CultureInfo.InvariantCulture, out float timestamp))
                    {
                        timestamps.Add(timestamp);
                    }
                }
                else if (line.StartsWith("nb_frames=", StringComparison.Ordinal))
                {
                    long.TryParse(line.Substring(10), NumberStyles.Integer, CultureInfo.InvariantCulture, out frames);
                }
                else if (line.StartsWith("duration=", StringComparison.Ordinal))
                {
                    float.TryParse(line.Substring(9), NumberStyles.Float, CultureInfo.InvariantCulture, out duration);
                }
            }

            float maximumEnd = VideoFpsWindowStartSeconds + MaximumVideoFpsMeasurementSeconds;
            float end = requestedEnd > VideoFpsWindowStartSeconds ? Math.Min(requestedEnd, duration) : Math.Min(duration, maximumEnd);
            measuredDuration = end - VideoFpsWindowStartSeconds;
            measuredFrames = timestamps.FindAll(timestamp => timestamp >= VideoFpsWindowStartSeconds && timestamp < end).Count;
            return frames > 0 && measuredDuration > 0;
        }

        // Quotes one process argument for paths that may contain spaces.
        private static string QuoteArgument(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        // Reports a recording failure through the Unity console.
        private void HandleRecordingFailed(Exception exception)
        {
            Debug.LogException(exception);
            HandleFinalizationStarted();
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
                if (status == "failed" && _sessionStats.finalizationDurationSeconds <= 0)
                {
                    _sessionStats.finalizationDurationSeconds = _measurementEnded ? Time.realtimeSinceStartup - _finalizationStartTime : 0;
                }
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
