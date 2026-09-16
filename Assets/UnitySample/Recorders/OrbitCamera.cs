using UnityEngine;

namespace UnityMediaRecorder.Example
{
    // Controls the sample camera's two eased half-turns independently of recording.
    [RequireComponent(typeof(Camera))]
    public sealed class OrbitCamera : MonoBehaviour
    {
        private const float Radius = 5f;
        private bool _started;
        private float _startTime;

        // Configures the scene camera and its initial orbit position.
        public Camera Configure(int overlayLayer)
        {
            Camera camera = GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.06f, 0.1f);
            camera.cullingMask &= ~(1 << overlayLayer);
            SetPosition(0f);
            return camera;
        }

        // Starts movement using the caller's shared real-time origin.
        public void BeginOrbit(float startTime)
        {
            _startTime = startTime;
            _started = true;
        }

        // Holds for three seconds between two two-second eased half-turns.
        private void LateUpdate()
        {
            float elapsed = _started ? Time.realtimeSinceStartup - _startTime : 0f;
            float degrees;
            if (elapsed < 3f) degrees = 0f;
            else if (elapsed < 5f) degrees = 180f * EvaluateTransition(elapsed - 3f);
            else if (elapsed < 8f) degrees = 180f;
            else degrees = 180f + 180f * EvaluateTransition(elapsed - 8f);
            SetPosition(degrees);
        }

        // Positions the camera on its orbit while looking at the cube.
        private void SetPosition(float degrees)
        {
            float angle = degrees * Mathf.Deg2Rad;
            transform.position = new Vector3(Mathf.Sin(angle) * Radius, 2.5f, Mathf.Cos(angle) * Radius);
            transform.LookAt(Vector3.up * 0.5f);
        }

        // Integrates quintic easing to produce a smooth velocity ramp.
        private static float IntegratedEaseInOut(float progress)
        {
            float squared = progress * progress;
            float fourth = squared * squared;
            return progress * progress * fourth - 3f * progress * fourth + 2.5f * fourth;
        }

        // Evaluates a two-second move with half-second acceleration and deceleration.
        private static float EvaluateTransition(float elapsed)
        {
            const float ramp = 0.5f;
            const float duration = 2f;
            const float speed = 2f / 3f;
            float clamped = Mathf.Clamp(elapsed, 0f, duration);
            if (clamped < ramp)
                return speed * ramp * IntegratedEaseInOut(clamped / ramp);
            if (clamped <= duration - ramp)
                return 1f / 6f + speed * (clamped - ramp);
            return 1f - speed * ramp * IntegratedEaseInOut((duration - clamped) / ramp);
        }
    }
}
