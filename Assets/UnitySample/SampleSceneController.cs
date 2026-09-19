using UnityEngine;

namespace UnityRuntimeCameraRecorder.Example
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
        public AudioClip BackgroundMusic;
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
            PlayBackgroundMusic();
            CameraViewSwitcher controls = Diagnostics.GetComponent<CameraViewSwitcher>();
            Diagnostics.Configure(OrbitView, OverlayView, FixedView, controls.ScreenCamera);
            Diagnostics.ConfigureCapture(OrbitView.pixelWidth, OrbitView.pixelHeight, Mathf.Max(1, QualitySettings.antiAliasing), "preview");
            Captures.Configure(OrbitView, OverlayView, FixedView, OrbitView.GetComponent<OrbitCamera>(), Diagnostics);
            controls.Captures = Captures;
            controls.Initialize();
            gameObject.AddComponent<SampleCommandLine>();
            if (RecordOnPlay)
            {
                Captures.BeginCapture();
            }
            else
            {
                OrbitView.GetComponent<OrbitCamera>().BeginOrbit(Time.realtimeSinceStartup);
            }
        }

        // Plays the scene's background track once through the normal audio mix.
        private void PlayBackgroundMusic()
        {
            if (BackgroundMusic == null)
            {
                return;
            }

            AudioSource music = gameObject.AddComponent<AudioSource>();
            music.playOnAwake = false;
            music.spatialBlend = 0f;
            music.clip = BackgroundMusic;
            music.Play();
        }

        // Restores the previous frame pacing when leaving the scene.
        private void OnDestroy()
        {
            QualitySettings.vSyncCount = _previousVSync;
            Application.targetFrameRate = _previousFrameRate;
        }
    }
}
