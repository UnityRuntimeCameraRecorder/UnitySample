using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace UnityMediaRecorder.Example
{
    // Adds the stationary screen view to the saved sample scene.
    public static class GroundCameraEditor
    {
        public static void AddSavedCamera()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/RecordingSample.unity");
            var controls = Object.FindFirstObjectByType<CameraViewSwitcher>();
            var root = GameObject.Find("GroundCamera") ?? new GameObject("GroundCamera");
            var camera = root.GetComponent<Camera>();
            if (camera == null)
            {
                camera = root.AddComponent<Camera>();
            }
            camera.CopyFrom(controls.Camera1);
            camera.targetTexture = null;
            camera.depth = 10f;
            camera.enabled = true;
            camera.cullingMask &= ~(1 << 30);
            root.transform.position = new Vector3(1f, 0.3f, -2.5f);
            root.transform.LookAt(GameObject.Find("Cube").transform.position);
            root.tag = "MainCamera";
            controls.Camera1.tag = "Untagged";
            controls.ScreenCamera = camera;

            const string previewPath = "Assets/UnitySample/Scene/GeneratedAssets/OrbitPreview.renderTexture";
            var orbitPreview = AssetDatabase.LoadAssetAtPath<RenderTexture>(previewPath);
            if (orbitPreview == null)
            {
                orbitPreview = new RenderTexture(1920, 1080, 24) { name = "OrbitPreview", antiAliasing = 4 };
                AssetDatabase.CreateAsset(orbitPreview, previewPath);
            }
            controls.Camera1.targetTexture = orbitPreview;
            GameObject.Find("OverlayCamera").GetComponent<Camera>().targetTexture = orbitPreview;
            controls.Camera2.cullingMask &= ~(1 << 30);
            var fixedOverlayObject = GameObject.Find("FixedOverlayCamera");
            if (fixedOverlayObject == null)
            {
                fixedOverlayObject = new GameObject("FixedOverlayCamera", typeof(Camera), typeof(OverlayCamera));
            }
            fixedOverlayObject.transform.SetParent(controls.Camera2.transform, false);
            var fixedOverlay = fixedOverlayObject.GetComponent<OverlayCamera>().Configure(controls.Camera2, 30);
            fixedOverlay.targetTexture = controls.Camera2.targetTexture;
            fixedOverlay.depth = controls.Camera2.depth + 0.1f;
            GameObject.Find("StaticCameraDiagnosticsCanvas").GetComponent<Canvas>().worldCamera = fixedOverlay;

            var canvas = controls.transform.Find("ApplicationCanvas");
            var fpsCanvasObject = GameObject.Find("ScreenFPSCanvas");
            if (fpsCanvasObject == null)
            {
                fpsCanvasObject = new GameObject("ScreenFPSCanvas", typeof(Canvas), typeof(CanvasScaler));
            }
            var fpsCanvas = fpsCanvasObject.GetComponent<Canvas>();
            fpsCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            fpsCanvas.sortingOrder = 10;
            var scaler = fpsCanvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var fpsObject = GameObject.Find("ScreenFPS");
            if (fpsObject == null)
            {
                fpsObject = new GameObject("ScreenFPS", typeof(RectTransform), typeof(Text));
            }
            fpsObject.transform.SetParent(fpsCanvasObject.transform, false);
            var fps = fpsObject.GetComponent<Text>();
            fps.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            fps.fontSize = 28;
            fps.color = Color.white;
            fps.alignment = TextAnchor.UpperLeft;
            fps.raycastTarget = false;
            fps.text = "FPS";
            var fpsRect = fps.rectTransform;
            fpsRect.anchorMin = fpsRect.anchorMax = fpsRect.pivot = new Vector2(0f, 1f);
            fpsRect.anchoredPosition = new Vector2(20f, -18f);
            fpsRect.sizeDelta = new Vector2(650f, 40f);
            controls.GetComponent<SampleDiagnostics>().ScreenFpsText = fps;
            foreach (string name in new[] { "MainCameraDiagnostics", "StaticCameraDiagnostics" })
            {
                var label = GameObject.Find(name).GetComponent<Text>();
                label.font = fps.font;
                label.fontSize = fps.fontSize;
                label.color = fps.color;
                label.alignment = fps.alignment;
                label.raycastTarget = false;
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = label.rectTransform.pivot = new Vector2(0f, 1f);
                label.rectTransform.anchoredPosition = fpsRect.anchoredPosition;
                label.rectTransform.sizeDelta = fpsRect.sizeDelta;
            }

            var original = GameObject.Find("Camera2Button");
            var buttonObject = GameObject.Find("ScreenCameraButton") ?? Object.Instantiate(original, original.transform.parent);
            buttonObject.name = "ScreenCameraButton";
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(16f, 16f);
            string[] leftControls = { "Camera2Button", "Camera1Button", "VSync", "AntiAliasing", "Fullscreen", "RenderResolution" };
            float[] heights = { 84f, 152f, 220f, 272f, 324f, 376f };
            for (int i = 0; i < leftControls.Length; i++)
            {
                var control = canvas.Find(leftControls[i]) as RectTransform;
                if (control != null)
                {
                    control.anchoredPosition = new Vector2(16f, heights[i]);
                }
            }
            buttonObject.GetComponentInChildren<Text>().text = "Screen view";
            var button = buttonObject.GetComponent<Button>();
            while (button.onClick.GetPersistentEventCount() > 0)
            {
                UnityEventTools.RemovePersistentListener(button.onClick, 0);
            }
            UnityEventTools.AddPersistentListener(button.onClick, controls.SelectScreenCamera);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
