using UnityEngine;

namespace UnityMediaRecorder.Example
{
    // Connects saved scene objects without constructing any scenery at runtime.
    public sealed class SampleSceneController : MonoBehaviour
    {
        public Camera OrbitView;
        public Camera OverlayView;
        public Camera FixedView;
        public SampleDiagnostics Diagnostics;
        public SampleCaptureController Captures;
        public bool RecordOnPlay;
        private int _previousVSync;
        private int _previousFrameRate;

        // Connects the authored cameras and optionally starts their capture sessions.
        private void Start()
        {
            MediaRecorderLog.Info = Debug.Log;
            MediaRecorderLog.Warning = Debug.LogWarning;
            MediaRecorderLog.Error = Debug.LogException;
            _previousVSync = QualitySettings.vSyncCount;
            _previousFrameRate = Application.targetFrameRate;
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = -1;
            Diagnostics.Configure(OrbitView, OverlayView, FixedView);
            Diagnostics.ConfigureCapture(OrbitView.pixelWidth, OrbitView.pixelHeight,
                Mathf.Max(1, QualitySettings.antiAliasing), "preview");
            Captures.Configure(OrbitView, OverlayView, FixedView,
                OrbitView.GetComponent<OrbitCamera>(), Diagnostics);
            CameraViewSwitcher controls = Diagnostics.GetComponent<CameraViewSwitcher>();
            controls.Captures = Captures;
            controls.Initialize();
            gameObject.AddComponent<SampleCommandLine>();
            if (RecordOnPlay)
            {
                Captures.BeginCapture();
            }
            else OrbitView.GetComponent<OrbitCamera>().BeginOrbit(Time.realtimeSinceStartup);
        }

        // Restores the previous frame pacing when leaving the scene.
        private void OnDestroy()
        {
            QualitySettings.vSyncCount = _previousVSync;
            Application.targetFrameRate = _previousFrameRate;
        }
    }
}
