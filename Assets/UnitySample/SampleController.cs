using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace UnityMediaRecorder.Example
{
    // Builds and wires independent sample components.
    public sealed class SampleController : MonoBehaviour
    {
        private const int OverlayLayer = 30;
        private int _previousTargetFrameRate;
        private int _previousVSyncCount;

        // Creates the sample automatically when an empty scene enters Play mode.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            new GameObject("UnitySample").AddComponent<SampleController>();
        }

        // Builds and connects independent scene, diagnostic and recording components.
        private void Start()
        {
            MediaRecorderLog.Info = Debug.Log;
            MediaRecorderLog.Warning = Debug.LogWarning;
            MediaRecorderLog.Error = Debug.LogException;
            _previousVSyncCount = QualitySettings.vSyncCount;
            _previousTargetFrameRate = Application.targetFrameRate;
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = -1;
            if (Camera.main != null) Camera.main.gameObject.SetActive(false);
            gameObject.AddComponent<SampleEnvironment>().Build();
            BuildCameras();
        }

        // Creates the two scene views and connects their diagnostics and capture controller.
        private void BuildCameras()
        {
            var orbitObject = new GameObject("OrbitCamera");
            orbitObject.tag = "MainCamera";
            var orbit = orbitObject.AddComponent<OrbitCamera>();
            Camera sceneCamera = orbit.Configure(OverlayLayer);
            orbitObject.AddComponent<TemporalMotionSmoothing>();
            orbitObject.AddComponent<AudioListener>();
            var overlayObject = new GameObject("OverlayCamera");
            overlayObject.transform.SetParent(orbitObject.transform, false);
            Camera overlay = overlayObject.AddComponent<OverlayCamera>().Configure(sceneCamera, OverlayLayer);
            var fixedObject = new GameObject("StaticCamera");
            Camera fixedCamera = fixedObject.AddComponent<FixedCamera>().Configure(sceneCamera.backgroundColor);
            fixedObject.AddComponent<TemporalMotionSmoothing>();
            var diagnostics = gameObject.AddComponent<SampleDiagnostics>();
            diagnostics.Configure(sceneCamera, overlay, fixedCamera);
            var capture = gameObject.AddComponent<SampleCaptureController>();
            capture.Configure(sceneCamera, overlay, fixedCamera, orbit, diagnostics);
            capture.BeginCapture();
        }

        // Restores the host application's render pacing when the sample is destroyed.
        private void OnDestroy()
        {
            QualitySettings.vSyncCount = _previousVSyncCount;
            Application.targetFrameRate = _previousTargetFrameRate;
        }

    }
}
