using UnityEngine;
using UnityEngine.UI;

namespace UnityRuntimeCameraRecorder.Example
{
    // Connects editable Canvas controls to preview selection and recording sessions.
    public sealed class CameraViewSwitcher : MonoBehaviour
    {
        public Camera Camera1, Camera2;
        public Camera ScreenCamera;
        public SampleCaptureController Captures;
        public RawImage Preview;
        public Toggle RecordCamera1, RecordCamera2, RecordScreen;
        public Toggle SingleVideoOutput;
        public Toggle VSync;
        public Toggle Fullscreen;
        public Dropdown AntiAliasing;
        public Dropdown OutputFrameRate, OutputResolution;
        public Dropdown RenderResolution;
        public Dropdown VideoCodec;
        public Dropdown RecordingQuality;
        private RenderTexture _originalFixedPreview, _fixedPreview;
        private int _previousAntiAliasing;
        public Button RecordButton;
        public Text RecordButtonLabel, FullscreenButtonLabel;
        private int _selectedCamera = 2;
        private Button[] _cameraSelectionButtons;
        private bool _quitAfterRecording;
        private Text _recordingStatus;
        // Applies the authored VSync choice after scene initialization.
        public void Initialize()
        {
#if !UNITY_EDITOR
            // Start consistently even if Unity retained display preferences from a previous run.
            if (Fullscreen != null)
            {
                Fullscreen.SetIsOnWithoutNotify(false);
            }

            if (RenderResolution != null)
            {
                RenderResolution.SetValueWithoutNotify(0);
            }

            Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
#endif
            _previousAntiAliasing = QualitySettings.antiAliasing;
            Text gpu = transform.Find("ApplicationCanvas/GPU")?.GetComponent<Text>();
            if (gpu != null)
            {
                gpu.text = "GPU: " + SystemInfo.graphicsDeviceName;
            }

            if (gpu != null)
            {
                gpu.rectTransform.anchorMin = gpu.rectTransform.anchorMax = new Vector2(.5f, 1);
                gpu.rectTransform.pivot = new Vector2(.5f, 1);
                gpu.rectTransform.anchoredPosition = new Vector2(0, -16);
            }

            Transform canvas = transform.Find("ApplicationCanvas");
            InitializeButtonStyles(canvas);
            _recordingStatus = canvas?.Find("RecordingStatus")?.GetComponent<Text>();
            if (_recordingStatus == null && canvas != null)
            {
                var label = new GameObject("RecordingStatus", typeof(RectTransform), typeof(Text));
                label.transform.SetParent(canvas, false);
                _recordingStatus = label.GetComponent<Text>();
                _recordingStatus.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                _recordingStatus.fontSize = 18;
                _recordingStatus.alignment = TextAnchor.MiddleCenter;
                _recordingStatus.raycastTarget = false;
                var rect = _recordingStatus.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0);
                rect.pivot = new Vector2(.5f, 0);
                rect.anchoredPosition = new Vector2(0, 16);
                rect.sizeDelta = new Vector2(700, 64);
            }

            if (VSync != null)
            {
                SetVSync(VSync.isOn);
            }

            if (AntiAliasing != null)
            {
                SetAntiAliasing(AntiAliasing.value);
            }

            ApplyOutputSettings();
            if (VideoCodec != null)
            {
                SetVideoCodec(VideoCodec.value);
            }

            InitializeRecordingQuality();
            InitializeSingleOutputToggle();

            if (RenderResolution != null)
            {
                SetRenderResolution(RenderResolution.value);
            }

            ApplyCameraSelectionHighlight();
        }

        // Adds consistent hover feedback and finds the three preview-selection buttons.
        private void InitializeButtonStyles(Transform canvas)
        {
            if (canvas == null)
            {
                return;
            }

            foreach (Button button in canvas.GetComponentsInChildren<Button>(true))
            {
                ColorBlock colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(.78f, .9f, 1f, 1f);
                colors.pressedColor = new Color(.55f, .76f, 1f, 1f);
                colors.selectedColor = Color.white;
                colors.colorMultiplier = 1.3f;
                colors.fadeDuration = .08f;
                button.colors = colors;
            }

            _cameraSelectionButtons = new[]
            {
                canvas.Find("Camera1Button")?.GetComponent<Button>(),
                canvas.Find("Camera2Button")?.GetComponent<Button>(),
                canvas.Find("ScreenCameraButton")?.GetComponent<Button>()
            };
        }

        // Highlights only the button that controls the current live preview.
        private void ApplyCameraSelectionHighlight()
        {
            if (_cameraSelectionButtons == null)
            {
                return;
            }

            for (int index = 0; index < _cameraSelectionButtons.Length; index++)
            {
                Button button = _cameraSelectionButtons[index];
                if (button?.targetGraphic != null)
                {
                    button.targetGraphic.color = index == _selectedCamera
                        ? new Color(.08f, .32f, .55f, .95f)
                        : new Color(.08f, .1f, .15f, .9f);
                }
            }
        }

        // Creates the optional single-output camera sequence control when the scene has not authored it.
        private void InitializeSingleOutputToggle()
        {
            if (SingleVideoOutput != null || RecordCamera1 == null)
            {
                return;
            }
            Vector2 singleOutputPosition = RecordScreen.GetComponent<RectTransform>().anchoredPosition;
            ShiftControlUp(VideoCodec);
            ShiftControlUp(RecordingQuality);
            ShiftControlUp(OutputResolution);
            ShiftControlUp(OutputFrameRate);
            ShiftControlUp(RecordCamera1);
            ShiftControlUp(RecordCamera2);
            ShiftControlUp(RecordScreen);
            SingleVideoOutput = Instantiate(RecordScreen, RecordScreen.transform.parent);
            SingleVideoOutput.name = "SingleVideoOutput";
            SingleVideoOutput.SetIsOnWithoutNotify(false);
            SingleVideoOutput.onValueChanged = new Toggle.ToggleEvent();
            RectTransform rect = SingleVideoOutput.GetComponent<RectTransform>();
            rect.anchoredPosition = singleOutputPosition;
            Text label = SingleVideoOutput.transform.Find("Label")?.GetComponent<Text>();
            if (label != null)
            {
                label.text = "One video output";
            }
        }

        // Moves one recording control upward to make room for the single-output toggle.
        private static void ShiftControlUp(Component control)
        {
            if (control != null)
            {
                control.GetComponent<RectTransform>().anchoredPosition += new Vector2(0, 44);
            }
        }

        private void InitializeRecordingQuality()
        {
            if (RecordingQuality == null && OutputResolution != null)
            {
                RecordingQuality = Instantiate(OutputResolution, OutputResolution.transform.parent);
                RecordingQuality.name = "RecordingQuality";
                RecordingQuality.GetComponent<RectTransform>().anchoredPosition = new Vector2(-16, 328);
            }
            if (RecordingQuality == null) return;
            // A cloned dropdown retains authored callbacks; quality must have its own callbacks.
            RecordingQuality.onValueChanged = new Dropdown.DropdownEvent();
            RecordingQuality.ClearOptions();
            RecordingQuality.AddOptions(new System.Collections.Generic.List<string> { "Quality: Low", "Quality: Medium", "Quality: High" });
            RecordingQuality.SetValueWithoutNotify(2);
            RecordingQuality.onValueChanged.AddListener(SetRecordingQuality);
            SetRecordingQuality(2);
        }

        public void SetRecordingQuality(int option)
        {
            Captures?.SetQualityPreset((RecordingQualityPreset)Mathf.Clamp(option, 0, 2));
        }

        // Applies the selected output frame-rate ceiling for future recordings.
        public void SetOutputFrameRate(int option)
        {
            ApplyOutputSettings();
        }

        // Applies the selected output dimensions for future recordings.
        public void SetOutputResolution(int option)
        {
            ApplyOutputSettings();
        }

        // Applies the selected codec to future camera and screen recordings.
        public void SetVideoCodec(int option)
        {
            Captures?.SetVideoCodec(option == 0 ? FFmpegMediaWriter.VideoStreamFormat.H264 : FFmpegMediaWriter.VideoStreamFormat.Hevc);
        }

        // Passes output preferences to the capture coordinator without changing preview resolution.
        private void ApplyOutputSettings()
        {
            if (Captures == null || OutputFrameRate == null || OutputResolution == null)
            {
                return;
            }

            bool fullHd = OutputResolution.value == 0;
            Captures.SetOutputSettings(fullHd ? 1920 : 3840, fullHd ? 1080 : 2160, OutputFrameRate.value == 0 ? 30 : 60);
        }

        // Changes application render size and window dimensions without changing video output preferences.
        public void SetRenderResolution(int option)
        {
            if (Captures != null && Captures.IsCapturing)
            {
                return;
            }

            int width = option == 0 ? 1920 : 3840;
            int height = option == 0 ? 1080 : 2160;
#if UNITY_EDITOR
            System.Type editor = System.Type.GetType("UnityRuntimeCameraRecorder.Example.SampleGameViewResolution, Assembly-CSharp-Editor");
            editor?.GetMethod("Apply").Invoke(null, new object[] { width, height });
#else
            Screen.SetResolution(width, height, Fullscreen != null && Fullscreen.isOn ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
#endif
            if (Captures != null)
            {
                Captures.SetRenderResolution(width, height);
            }

            if (_fixedPreview != null)
            {
                _fixedPreview.Release();
                _fixedPreview.width = width;
                _fixedPreview.height = height;
                _fixedPreview.Create();
            }
        }

        // Switches presentation mode while keeping the selected application resolution.
        public void SetFullscreen(bool enabled)
        {
#if UNITY_EDITOR
            System.Type type = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            UnityEditor.EditorWindow view = UnityEditor.EditorWindow.GetWindow(type);
            view.maximized = enabled;
#else
            bool fullHd = RenderResolution == null || RenderResolution.value == 0;
            Screen.SetResolution(fullHd ? 1920 : 3840, fullHd ? 1080 : 2160, enabled ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
#endif
        }

        // Applies MSAA to both previews and future recording targets while idle.
        public void SetAntiAliasing(int option)
        {
            if (Captures != null && Captures.IsCapturing)
            {
                return;
            }

            int samples = new[]
            {
                1,
                2,
                4,
                8
            }[Mathf.Clamp(option, 0, 3)];
            QualitySettings.antiAliasing = samples == 1 ? 0 : samples;
            if (Captures != null)
            {
                Captures.SetAntiAliasing(samples);
            }

            SampleDiagnostics diagnostics = GetComponent<SampleDiagnostics>();
            if (diagnostics != null && Camera1 != null)
            {
                diagnostics.ConfigureCapture(Camera1.pixelWidth, Camera1.pixelHeight, samples, "preview");
            }

            if (Camera1 != null)
            {
                Camera1.allowMSAA = samples > 1;
            }

            if (Camera2 == null)
            {
                return;
            }

            Camera2.allowMSAA = samples > 1;
            if (_originalFixedPreview == null)
            {
                _originalFixedPreview = Camera2.targetTexture;
            }

            if (_originalFixedPreview == null)
            {
                return;
            }

            Camera2.targetTexture = _originalFixedPreview;
            if (_fixedPreview != null)
            {
                _fixedPreview.Release();
                Destroy(_fixedPreview);
            }

            _fixedPreview = new RenderTexture(_originalFixedPreview.descriptor)
            {
                antiAliasing = samples,
                name = "FixedPreviewMSAA"
            };
            if (RenderResolution != null)
            {
                _fixedPreview.width = RenderResolution.value == 0 ? 1920 : 3840;
                _fixedPreview.height = RenderResolution.value == 0 ? 1080 : 2160;
            }

            _fixedPreview.Create();
            Camera2.targetTexture = _fixedPreview;
        }

        // Restores global quality and releases the runtime-only fixed preview texture.
        private void OnDestroy()
        {
            QualitySettings.antiAliasing = _previousAntiAliasing;
            if (_fixedPreview == null)
            {
                return;
            }

            if (Camera2 != null)
            {
                Camera2.targetTexture = _originalFixedPreview;
            }

            _fixedPreview.Release();
            Destroy(_fixedPreview);
        }

        // Enables display synchronization or allows uncapped rendering.
        public void SetVSync(bool enabled)
        {
            QualitySettings.vSyncCount = enabled ? 1 : 0;
            Application.targetFrameRate = -1;
#if UNITY_EDITOR
            System.Type gameViewType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            var property = gameViewType?.GetProperty("vSyncEnabled", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (property == null || !property.CanWrite)
            {
                Debug.LogWarning("Cannot synchronize the editor Game view VSync setting in this Unity version.");
                return;
            }

            foreach (UnityEngine.Object view in Resources.FindObjectsOfTypeAll(gameViewType))
            {
                property.SetValue(view, enabled);
            }
#endif
        }

        // Updates the preview, recording controls and deferred application exit.
        private void Update()
        {
            Camera selected = _selectedCamera == 0 ? Camera1 : _selectedCamera == 1 ? Camera2 : ScreenCamera;
            if (Preview != null)
            {
                Preview.texture = selected != null ? selected.targetTexture : null;
                Preview.enabled = Preview.texture != null;
            }

            SampleDiagnostics diagnostics = GetComponent<SampleDiagnostics>();
            if (diagnostics != null && diagnostics.ScreenFpsText != null)
            {
                diagnostics.ScreenFpsText.enabled = selected == ScreenCamera;
            }

            bool busy = Captures != null && Captures.IsCapturing;
            if (_recordingStatus != null)
            {
                _recordingStatus.text = Captures != null ? Captures.StatusText : "";
            }

            if (AntiAliasing != null)
            {
                AntiAliasing.interactable = !busy;
            }

            if (OutputFrameRate != null)
            {
                OutputFrameRate.interactable = !busy;
            }

            if (OutputResolution != null)
            {
                OutputResolution.interactable = !busy;
            }

            if (RecordingQuality != null) RecordingQuality.interactable = !busy;
            if (VideoCodec != null)
            {
                VideoCodec.interactable = !busy;
            }

            if (RenderResolution != null)
            {
                RenderResolution.interactable = !busy;
            }

            if (RecordCamera1 != null)
            {
                RecordCamera1.interactable = !busy;
            }

            if (RecordCamera2 != null)
            {
                RecordCamera2.interactable = !busy;
            }

            if (RecordScreen != null)
            {
                RecordScreen.interactable = !busy;
            }

            if (SingleVideoOutput != null)
            {
                SingleVideoOutput.interactable = !busy;
            }

            if (RecordButton != null)
            {
                int selectedSources = (RecordCamera1.isOn ? 1 : 0) + (RecordCamera2.isOn ? 1 : 0) + (RecordScreen.isOn ? 1 : 0);
                bool validSelection = SingleVideoOutput != null && SingleVideoOutput.isOn ? selectedSources >= 2 : selectedSources >= 1;
                RecordButton.interactable = Captures != null && (busy || validSelection);
            }

            if (RecordButtonLabel != null)
            {
                RecordButtonLabel.text = busy ? "Stop recording" : "Record";
            }

            if (FullscreenButtonLabel != null)
            {
                FullscreenButtonLabel.text = IsFullscreen() ? "Windowed" : "Fullscreen";
            }

            if (!_quitAfterRecording || busy)
            {
                return;
            }

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // Selects the orbit camera without altering recording.
        public void SelectCamera1()
        {
            _selectedCamera = 0;
            ApplyCameraSelectionHighlight();
        }

        // Selects the fixed camera without altering recording.
        public void SelectCamera2()
        {
            _selectedCamera = 1;
            ApplyCameraSelectionHighlight();
        }

        // Returns to the stationary ground-level view used for screen recordings.
        public void SelectScreenCamera()
        {
            _selectedCamera = 2;
            ApplyCameraSelectionHighlight();
        }

        // Starts selected recordings or finalizes the active session.
        public void ToggleRecording()
        {
            if (Captures == null)
            {
                return;
            }

            if (Captures.IsCapturing)
            {
                Captures.StopCapture();
            }
            else
            {
                bool singleOutput = SingleVideoOutput != null && SingleVideoOutput.isOn;
                Captures.BeginCapture(RecordCamera1.isOn, RecordCamera2.isOn, RecordScreen.isOn, singleOutput);
            }
        }

        // Finalizes media before leaving the application or Play mode.
        public void Quit()
        {
            _quitAfterRecording = true;
            if (Captures != null)
            {
                Captures.StopCapture();
            }
        }

        // Changes window mode or maximizes the editor Game view.
        public void ToggleFullscreen()
        {
#if UNITY_EDITOR
            UnityEditor.EditorWindow window = UnityEditor.EditorWindow.focusedWindow;
            if (window != null)
            {
                window.maximized = !window.maximized;
            }
#else
            Screen.fullScreen = !Screen.fullScreen;
#endif
        }

        // Returns presentation mode for the button label.
        private static bool IsFullscreen()
        {
#if UNITY_EDITOR
            UnityEditor.EditorWindow window = UnityEditor.EditorWindow.focusedWindow;
            return window != null && window.maximized;
#else
            return Screen.fullScreen;
#endif
        }
    }
}
