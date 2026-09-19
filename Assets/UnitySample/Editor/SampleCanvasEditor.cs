using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace UnityRuntimeCameraRecorder.Example
{
    // Authors editable Canvas controls with persistent callbacks.
    public static class SampleCanvasEditor
    {
        public static void AddSavedRecordingQualityControl()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/RecordingSample.unity");
            var controls = Object.FindFirstObjectByType<CameraViewSwitcher>();
            var canvas = controls.transform.Find("ApplicationCanvas");
            if (controls.RecordingQuality == null)
            {
                controls.RecordingQuality = CreateDropdown(canvas, "RecordingQuality", new Vector2(1, 0), new Vector2(-16, 328), new[] { "Quality: Low", "Quality: Medium", "Quality: High" }, 2, controls.SetRecordingQuality);
            }
            var manualPreset = canvas.Find("EncodingPreset");
            if (manualPreset != null)
            {
                Object.DestroyImmediate(manualPreset.gameObject);
            }
            controls.RecordingQuality.GetComponent<RectTransform>().anchoredPosition = new Vector2(-16, 328);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        }

        // Adds only the codec control while preserving other authored Canvas elements.
        public static void AddSavedVideoCodecControl()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/RecordingSample.unity");
            var controls = Object.FindFirstObjectByType<CameraViewSwitcher>();
            var canvas = controls.transform.Find("ApplicationCanvas");
            controls.VideoCodec = CreateDropdown(canvas, "VideoCodec", new Vector2(1, 0), new Vector2(-16, 380), new[] { "Video codec: H.264", "Video codec: HEVC" }, 0, controls.SetVideoCodec);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        }
        // Updates only the saved sample UI from a batch-mode invocation.
        public static void UpdateSavedControls()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/RecordingSample.unity");
            Build(Object.FindFirstObjectByType<CameraViewSwitcher>());
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        }

        // Restores the authored default without invoking runtime rendering changes.
        [MenuItem("UnitySample/Reset anti-aliasing to MSAA 4x")]
        public static void ResetAntiAliasing()
        {
            if (EditorApplication.isPlaying)
            {
                return;
            }

            CameraViewSwitcher controls = Object.FindFirstObjectByType<CameraViewSwitcher>();
            if (controls == null || controls.AntiAliasing == null)
            {
                return;
            }

            Undo.RecordObject(controls.AntiAliasing, "Reset anti-aliasing");
            controls.AntiAliasing.SetValueWithoutNotify(2);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(controls.gameObject.scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(controls.gameObject.scene);
        }

        // Places recording controls together at the lower-right edge of the saved Canvas.
        [MenuItem("UnitySample/Align recording controls right")]
        public static void AlignRecordingControlsRight()
        {
            if (EditorApplication.isPlaying)
            {
                return;
            }

            Transform canvas = GameObject.Find("ApplicationCanvas")?.transform;
            if (canvas == null)
            {
                return;
            }

            string[] names =
            {
                "ExportImageButton",
                "RecordButton",
                "RecordScreen",
                "RecordCamera2",
                "RecordCamera1"
            };
            float[] heights =
            {
                16,
                84,
                152,
                196,
                240
            };
            for (int i = 0; i < names.Length; i++)
            {
                RectTransform rect = canvas.Find(names[i]) as RectTransform;
                if (rect == null)
                {
                    continue;
                }

                Undo.RecordObject(rect, "Align recording controls right");
                rect.anchorMin = rect.anchorMax = new Vector2(1, 0);
                rect.pivot = new Vector2(1, 0);
                rect.anchoredPosition = new Vector2(-16, heights[i]);
                if (i <= 1)
                {
                    rect.sizeDelta = new Vector2(248, rect.sizeDelta.y);
                    RectTransform label = rect.Find("Label") as RectTransform;
                    if (label != null)
                    {
                        Undo.RecordObject(label, "Resize record button label");
                        label.sizeDelta = new Vector2(248, label.sizeDelta.y);
                    }
                }
            }

            for (int i = 0; i < 2; i++)
            {
                RectTransform rect = canvas.Find(i == 0 ? "Camera1Button" : "Camera2Button") as RectTransform;
                if (rect == null)
                {
                    continue;
                }

                Undo.RecordObject(rect, "Stack camera buttons");
                rect.anchorMin = rect.anchorMax = Vector2.zero;
                rect.pivot = Vector2.zero;
                rect.anchoredPosition = new Vector2(16, i == 0 ? 84 : 16);
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(canvas.gameObject.scene);
        }

        // Creates screen controls while preserving existing user-adjusted positions.
        public static void Build(CameraViewSwitcher controls)
        {
            foreach (string name in new[]
            {
                "MainCameraDiagnosticsCanvas",
                "StaticCameraDiagnosticsCanvas"
            }

            )
            {
                GameObject diagnostics = GameObject.Find(name);
                if (diagnostics == null)
                {
                    continue;
                }

                foreach (Text text in diagnostics.GetComponentsInChildren<Text>(true))
                {
                    Undo.RecordObject(text, "Simplify saved diagnostics");
                    text.text = "";
                    text.fontSize = 28;
                    EditorUtility.SetDirty(text);
                }
            }

            GameObject root = GameObject.Find("ApplicationCanvas");
            if (root == null)
            {
                root = new GameObject("ApplicationCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                root.transform.SetParent(controls.transform, false);
                root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                CanvasScaler scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280, 720);
                scaler.matchWidthOrHeight = 1f;
            }

            RectTransform preview = Rect(root.transform, "CameraPreview", Vector2.zero, Vector2.zero, Vector2.zero);
            preview.anchorMax = Vector2.one;
            preview.offsetMin = preview.offsetMax = Vector2.zero;
            controls.Preview = Get<RawImage>(preview.gameObject);
            controls.Preview.raycastTarget = false;
            controls.Preview.enabled = false;
            preview.SetAsFirstSibling();
            Button(root.transform, "Camera1Button", "Camera 1", new Vector2(16, 16), false, controls.SelectCamera1);
            Button(root.transform, "Camera2Button", "Camera 2", new Vector2(208, 16), false, controls.SelectCamera2);
            bool newVSync = root.transform.Find("VSync") == null;
            controls.VSync = Checkbox(root.transform, "VSync", "VSync", 152, true);
            if (newVSync)
            {
                ((RectTransform)controls.VSync.transform).anchoredPosition = new Vector2(16, 152);
            }

            if (controls.VSync.onValueChanged.GetPersistentEventCount() == 0)
            {
                UnityEventTools.AddPersistentListener(controls.VSync.onValueChanged, controls.SetVSync);
            }

            controls.AntiAliasing = CreateAntiAliasingDropdown(root.transform, controls);
            controls.RenderResolution = CreateDropdown(root.transform, "RenderResolution", Vector2.zero, new Vector2(16, 308), new[] { "Render: Full HD", "Render: 4K" }, 0, controls.SetRenderResolution);
            controls.MotionBlur = Checkbox(root.transform, "MotionBlur", "Motion blur", 444, true);
            if (controls.MotionBlur.onValueChanged.GetPersistentEventCount() == 0)
            {
                UnityEventTools.AddPersistentListener(controls.MotionBlur.onValueChanged, controls.SetMotionBlur);
            }
            bool newFullscreen = root.transform.Find("Fullscreen") == null;
            controls.Fullscreen = Checkbox(root.transform, "Fullscreen", "Fullscreen", 256, false);
            if (newFullscreen)
            {
                ((RectTransform)controls.Fullscreen.transform).anchoredPosition = new Vector2(16, 256);
            }

            if (controls.Fullscreen.onValueChanged.GetPersistentEventCount() == 0)
            {
                UnityEventTools.AddPersistentListener(controls.Fullscreen.onValueChanged, controls.SetFullscreen);
            }

            controls.OutputFrameRate = CreateDropdown(root.transform, "OutputFrameRate", new Vector2(1, 0), new Vector2(-16, 292), new[] { "Output: 30 FPS", "Output: 60 FPS" }, 1, controls.SetOutputFrameRate);
            controls.OutputResolution = CreateDropdown(root.transform, "OutputResolution", new Vector2(1, 0), new Vector2(-16, 344), new[] { "Output: Full HD", "Output: 4K" }, 1, controls.SetOutputResolution);
            controls.RecordingQuality = CreateDropdown(root.transform, "RecordingQuality", new Vector2(1, 0), new Vector2(-16, 396), new[] { "Quality: Low", "Quality: Medium", "Quality: High" }, 2, controls.SetRecordingQuality);
            controls.VideoCodec = CreateDropdown(root.transform, "VideoCodec", new Vector2(1, 0), new Vector2(-16, 448), new[] { "Video codec: H.264", "Video codec: HEVC" }, 0, controls.SetVideoCodec);
            Text gpu = Label(root.transform, "GPU", "", Vector2.zero, new Vector2(620, 28));
            gpu.fontSize = 18;
            gpu.alignment = TextAnchor.MiddleCenter;
            gpu.rectTransform.anchorMin = gpu.rectTransform.anchorMax = new Vector2(.5f, 1);
            gpu.rectTransform.pivot = new Vector2(.5f, 1);
            gpu.rectTransform.anchoredPosition = new Vector2(0, -16);
            Text status = Label(root.transform, "RecordingStatus", "", Vector2.zero, new Vector2(700, 64));
            status.fontSize = 18;
            status.alignment = TextAnchor.MiddleCenter;
            status.rectTransform.anchorMin = status.rectTransform.anchorMax = new Vector2(.5f, 0);
            status.rectTransform.pivot = new Vector2(.5f, 0);
            status.rectTransform.anchoredPosition = new Vector2(0, 16);
            controls.RecordCamera1 = Checkbox(root.transform, "RecordCamera1", "Record camera 1", 172, true);
            controls.RecordCamera2 = Checkbox(root.transform, "RecordCamera2", "Record camera 2", 128, true);
            controls.RecordScreen = Checkbox(root.transform, "RecordScreen", "Record screen", 84, true);
            controls.RecordButton = Button(root.transform, "RecordButton", "Record", new Vector2(680, 84), false, controls.ToggleRecording);
            controls.ExportImageButton = Button(root.transform, "ExportImageButton", "Export JPG", new Vector2(680, 16), false, controls.ExportSelectedImageSecond);
            controls.RecordButtonLabel = controls.RecordButton.GetComponentInChildren<Text>();
            Transform oldFullscreen = root.transform.Find("FullscreenButton");
            if (oldFullscreen != null)
            {
                oldFullscreen.gameObject.SetActive(false);
            }

            controls.FullscreenButtonLabel = null;
            Button(root.transform, "QuitButton", "Quit", new Vector2(-196, -16), true, controls.Quit);
            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            }

            EditorUtility.SetDirty(controls);
        }

        // Authors an editable dropdown with a standard Unity toggle template.
        private static Dropdown CreateAntiAliasingDropdown(Transform parent, CameraViewSwitcher controls)
        {
            return CreateDropdown(parent, "AntiAliasing", Vector2.zero, new Vector2(16, 204), new[] { "AA: Off", "AA: MSAA 2×", "AA: MSAA 4×", "AA: MSAA 8×" }, 2, controls.SetAntiAliasing);
        }

        // Authors a reusable saved dropdown while retaining the selected option and position.
        private static Dropdown CreateDropdown(Transform parent, string name, Vector2 anchor, Vector2 position, string[] options, int initial, UnityAction<int> action)
        {
            bool created = parent.Find(name) == null;
            RectTransform rect = Rect(parent, name, anchor, position, new Vector2(248, 44));
            if (created)
            {
                rect.pivot = new Vector2(anchor.x, 0);
            }

            Image background = Get<Image>(rect.gameObject);
            background.color = new Color(.08f, .1f, .15f, .95f);
            Dropdown dropdown = Get<Dropdown>(rect.gameObject);
            dropdown.targetGraphic = background;
            dropdown.captionText = Label(rect, "Caption", "", new Vector2(10, 0), new Vector2(228, 44));
            RectTransform template = Rect(rect, "Template", Vector2.zero, new Vector2(0, 44), new Vector2(248, 44));
            template.sizeDelta = new Vector2(template.sizeDelta.x, 44);
            Get<Image>(template.gameObject).color = new Color(.08f, .1f, .15f, 1);
            RectTransform item = Rect(template, "Item", Vector2.zero, Vector2.zero, new Vector2(248, 44));
            Toggle toggle = Get<Toggle>(item.gameObject);
            Image itemBackground = Get<Image>(item.gameObject);
            itemBackground.color = new Color(.15f, .2f, .3f, 1);
            toggle.targetGraphic = itemBackground;
            Text checkmark = Label(item, "Checkmark", "•", Vector2.zero, new Vector2(24, 44));
            toggle.graphic = checkmark;
            dropdown.itemText = Label(item, "Label", "", new Vector2(24, 0), new Vector2(224, 44));
            dropdown.template = template;
            template.gameObject.SetActive(false);
            int selected = created ? initial : dropdown.value;
            dropdown.ClearOptions();
            dropdown.AddOptions(new System.Collections.Generic.List<string>(options));
            dropdown.SetValueWithoutNotify(selected);
            if (dropdown.onValueChanged.GetPersistentEventCount() == 0)
            {
                UnityEventTools.AddPersistentListener(dropdown.onValueChanged, action);
            }

            return dropdown;
        }

        // Positions a rectangle only when initially created.
        private static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                return (RectTransform)existing;
            }

            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0, anchor.y);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        // Creates a button and its saved click callback.
        private static Button Button(Transform parent, string name, string caption, Vector2 position, bool topRight, UnityAction action)
        {
            RectTransform rect = Rect(parent, name, topRight ? Vector2.one : Vector2.zero, position, new Vector2(180, 56));
            Image image = Get<Image>(rect.gameObject);
            image.color = new Color(.08f, .1f, .15f, .9f);
            Button button = Get<Button>(rect.gameObject);
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(.78f, .9f, 1f, 1f);
            colors.pressedColor = new Color(.55f, .76f, 1f, 1f);
            colors.selectedColor = Color.white;
            colors.colorMultiplier = 1.3f;
            colors.fadeDuration = .08f;
            button.colors = colors;
            if (button.onClick.GetPersistentEventCount() == 0)
            {
                UnityEventTools.AddPersistentListener(button.onClick, action);
            }

            Text label = Label(rect, "Label", caption, Vector2.zero, rect.sizeDelta);
            label.rectTransform.sizeDelta = rect.sizeDelta;
            label.alignment = TextAnchor.MiddleCenter;
            return button;
        }

        // Creates a large checkbox and editable text label.
        private static Toggle Checkbox(Transform parent, string name, string caption, float y, bool initialValue)
        {
            bool created = parent.Find(name) == null;
            RectTransform rect = Rect(parent, name, Vector2.zero, new Vector2(416, y), new Vector2(248, 36));
            Toggle toggle = Get<Toggle>(rect.gameObject);
            RectTransform box = Rect(rect, "Box", Vector2.zero, Vector2.zero, new Vector2(36, 36));
            Image background = Get<Image>(box.gameObject);
            background.color = new Color(.1f, .13f, .2f, .95f);
            Text mark = Label(box, "Checkmark", "X", Vector2.zero, new Vector2(36, 36));
            mark.alignment = TextAnchor.MiddleCenter;
            toggle.targetGraphic = background;
            toggle.graphic = mark;
            if (created)
            {
                toggle.isOn = initialValue;
            }

            Label(rect, "Label", caption, new Vector2(48, 0), new Vector2(200, 36));
            return toggle;
        }

        // Creates text without intercepting pointer events.
        private static Text Label(Transform parent, string name, string caption, Vector2 position, Vector2 size)
        {
            RectTransform rect = Rect(parent, name, Vector2.zero, position, size);
            Text label = Get<Text>(rect.gameObject);
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 22;
            label.color = Color.white;
            label.text = caption;
            label.alignment = TextAnchor.MiddleLeft;
            label.raycastTarget = false;
            return label;
        }

        // Retrieves or adds a real Unity component using Unity null semantics.
        private static T Get<T>(GameObject owner)
            where T : Component
        {
            T component = owner.GetComponent<T>();
            return component != null ? component : owner.AddComponent<T>();
        }
    }
}
