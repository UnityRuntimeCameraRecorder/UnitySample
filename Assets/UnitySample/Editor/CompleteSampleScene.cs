using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityRuntimeCameraRecorder.Example
{
    // Authors and saves the complete sample as normal editable scene objects and assets.
    public static class CompleteSampleScene
    {
        private const string AssetRoot = "Assets/UnitySample/Scene/GeneratedAssets";
        // Adds only the camera preview controls while preserving all other scene settings.
        [MenuItem("UnitySample/Add camera view buttons")]
        public static void AddCameraViewButtons()
        {
            if (EditorApplication.isPlaying)
            {
                throw new InvalidOperationException("Leave Play mode before editing the scene.");
            }

            CameraViewSwitcher switcher = Component<CameraViewSwitcher>(FindOrCreate("Diagnostics"));
            switcher.Camera1 = GameObject.Find("OrbitCamera").GetComponent<Camera>();
            switcher.Camera2 = GameObject.Find("FixedCamera").GetComponent<Camera>();
            Camera presentation = Component<Camera>(FindOrCreate("ScreenPresentationCamera"));
            presentation.transform.SetParent(switcher.transform, false);
            presentation.clearFlags = CameraClearFlags.Nothing;
            presentation.cullingMask = 0;
            presentation.depth = 100f;
            SampleCanvasEditor.Build(switcher);
            EditorUtility.SetDirty(switcher);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }

        // Completes the currently open sample scene without generating scenery during Play mode.
        [MenuItem("UnitySample/Complete saved scene")]
        public static void Complete()
        {
            if (EditorApplication.isPlaying)
            {
                throw new InvalidOperationException("Leave Play mode before editing the scene.");
            }

            if (!AssetDatabase.IsValidFolder(AssetRoot))
            {
                AssetDatabase.CreateFolder("Assets/UnitySample/Scene", "GeneratedAssets");
            }

            Transform scenery = FindOrCreate("Scene").transform;
            Transform recorders = FindOrCreate("Recorders").transform;
            Transform diagnosticRoot = FindOrCreate("Diagnostics").transform;
            BuildWorld(scenery);
            BuildRecorders(recorders, diagnosticRoot);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("Complete editable sample scene saved: cube, grass, atmosphere, cameras, effects and diagnostics.");
        }

        // Reuses scene objects by name so completing the sample does not duplicate them.
        private static GameObject FindOrCreate(string name)
        {
            return GameObject.Find(name) ?? new GameObject(name);
        }

        // Configures the saved subject, ground, lighting and atmosphere.
        private static void BuildWorld(Transform parent)
        {
            GameObject cube = GameObject.Find("Cube");
            if (cube == null)
            {
                cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "Cube";
            }

            cube.transform.SetParent(parent, true);
            cube.transform.position = Vector3.up * 0.5f;
            cube.GetComponent<Renderer>().sharedMaterial = MaterialAsset("CubeBlue", "UnitySample/Lit", new Color(0.15f, 0.55f, 1f));
            cube.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.On;
            GameObject floor = GameObject.Find("Floor");
            if (floor == null)
            {
                floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                floor.name = "Floor";
            }

            floor.transform.SetParent(parent, true);
            floor.transform.localScale = Vector3.one * 2f;
            floor.GetComponent<Renderer>().sharedMaterial = MaterialAsset("Ground", "UnitySample/Lit", new Color(0.18f, 0.2f, 0.24f));
            floor.GetComponent<Renderer>().receiveShadows = true;
            Light light = Component<Light>(FindOrCreate("Directional Light"));
            light.transform.SetParent(parent, true);
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.75f;
            light.shadowBias = 0.035f;
            light.shadowNormalBias = 0.25f;
            light.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            RenderSettings.ambientLight = new Color(0.25f, 0.25f, 0.3f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.16f, 0.2f, 0.25f);
            RenderSettings.fogDensity = 0.022f;
            BuildAtmosphere(parent);
        }

        // Bakes grass geometry and saves the particle systems as editable scene components.
        private static void BuildAtmosphere(Transform parent)
        {
            if (GameObject.Find("WindGrassPatches") == null)
            {
                SampleGrass.Create();
            }

            if (GameObject.Find("FloatingAirParticles") == null)
            {
                SampleAtmosphere.CreateFloatingParticles();
            }

            if (GameObject.Find("HumidityMist") == null)
            {
                SampleAtmosphere.CreateHumidityMist();
            }

            foreach (string name in new[]
            {
                "WindGrassPatches",
                "FloatingAirParticles",
                "HumidityMist"
            }

            )
            {
                GameObject item = GameObject.Find(name);
                item.transform.SetParent(parent, true);
                Renderer renderer = item.GetComponent<Renderer>();
                if (!AssetDatabase.Contains(renderer.sharedMaterial))
                {
                    AssetDatabase.CreateAsset(renderer.sharedMaterial, AssetRoot + "/" + name + ".mat");
                }

                MeshFilter mesh = item.GetComponent<MeshFilter>();
                if (mesh != null && !AssetDatabase.Contains(mesh.sharedMesh))
                {
                    AssetDatabase.CreateAsset(mesh.sharedMesh, AssetRoot + "/Grass.asset");
                }

                ParticleSystem particles = item.GetComponent<ParticleSystem>();
                if (particles != null)
                {
                    particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }

        // Creates persistent materials rather than leaving transient material instances in the scene.
        private static Material MaterialAsset(string name, string shaderName, Color color)
        {
            string path = AssetRoot + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find(shaderName);
                if (shader == null)
                {
                    throw new InvalidOperationException("Missing shader: " + shaderName);
                }

                material = new Material(shader)
                {
                    color = color
                };
                AssetDatabase.CreateAsset(material, path);
            }

            return material;
        }

        // Connects saved recording cameras, post-processing and existing diagnostic canvases.
        private static void BuildRecorders(Transform parent, Transform diagnosticRoot)
        {
            GameObject mainObject = GameObject.Find("Main Camera") ?? FindOrCreate("OrbitCamera");
            mainObject.name = "OrbitCamera";
            mainObject.tag = "MainCamera";
            mainObject.transform.SetParent(parent, true);
            OrbitCamera orbit = Component<OrbitCamera>(mainObject);
            Camera main = orbit.Configure(30);
            main.depth = 0f;
            Component<AudioListener>(mainObject);
            Component<TemporalMotionSmoothing>(mainObject);
            Camera overlay = CreateOverlay(mainObject, main);
            GameObject fixedObject = FindOrCreate("FixedCamera");
            fixedObject.transform.SetParent(parent, true);
            Camera fixedView = Component<FixedCamera>(fixedObject).Configure(main.backgroundColor);
            Component<TemporalMotionSmoothing>(fixedObject);
            fixedView.depth = 1f;
            RenderTexture preview = AssetDatabase.LoadAssetAtPath<RenderTexture>(AssetRoot + "/FixedPreview.renderTexture");
            if (preview == null)
            {
                preview = new RenderTexture(1920, 1080, 24)
                {
                    name = "FixedPreview"
                };
                AssetDatabase.CreateAsset(preview, AssetRoot + "/FixedPreview.renderTexture");
            }

            fixedView.targetTexture = preview;
            SampleDiagnostics diagnostics = Component<SampleDiagnostics>(diagnosticRoot.gameObject);
            diagnostics.Configure(main, overlay, fixedView);
            foreach (string name in new[]
            {
                "MainCameraDiagnosticsCanvas",
                "StaticCameraDiagnosticsCanvas"
            }

            )
            {
                GameObject.Find(name).transform.SetParent(diagnosticRoot, true);
            }

            SampleCaptureController captures = Component<SampleCaptureController>(parent.gameObject);
            SampleSceneController controller = Component<SampleSceneController>(FindOrCreate("SampleController"));
            controller.OrbitView = main;
            controller.OverlayView = overlay;
            controller.FixedView = fixedView;
            controller.Diagnostics = diagnostics;
            controller.Captures = captures;
            controller.RecordOnPlay = false;
            CameraViewSwitcher switcher = Component<CameraViewSwitcher>(diagnosticRoot.gameObject);
            switcher.Camera1 = main;
            switcher.Camera2 = fixedView;
            EditorUtility.SetDirty(switcher);
            EditorUtility.SetDirty(diagnostics);
            EditorUtility.SetDirty(controller);
        }

        // Creates a saved overlay camera that draws text after the main image effect.
        private static Camera CreateOverlay(GameObject parent, Camera sceneCamera)
        {
            GameObject overlay = FindOrCreate("OverlayCamera");
            overlay.transform.SetParent(parent.transform, false);
            return Component<OverlayCamera>(overlay).Configure(sceneCamera, 30);
        }

        // Retrieves or adds one component without creating duplicate behaviours.
        private static T Component<T>(GameObject owner)
            where T : UnityEngine.Component
        {
            T existing = owner.GetComponent<T>();
            return existing != null ? existing : owner.AddComponent<T>();
        }
    }
}
