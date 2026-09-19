using UnityEngine;

namespace UnityRuntimeCameraRecorder.Example
{
    // Moves the sample's second view independently of recording.
    [RequireComponent(typeof(Camera))]
    public sealed class FixedCamera : MonoBehaviour
    {
        private float _startTime;
        private float _motionSeed;

        private void Start()
        {
            _startTime = Time.realtimeSinceStartup;
            _motionSeed = Random.Range(0f, 1000f);
        }

        // Orbits with smooth random changes in height and distance while keeping the cube in view.
        private void LateUpdate()
        {
            float elapsed = Time.realtimeSinceStartup - _startTime;
            float angle = -135f * Mathf.Deg2Rad + elapsed * 12f * Mathf.Deg2Rad;
            float blend = Mathf.SmoothStep(0f, 1f, elapsed / 2f);
            float radius = Mathf.Lerp(3.8f,
                Mathf.Lerp(1.8f, 5.8f, Mathf.PerlinNoise(_motionSeed, elapsed * 0.18f)), blend);
            float height = Mathf.Lerp(1.8f,
                Mathf.Lerp(0.9f, 2.7f, Mathf.PerlinNoise(_motionSeed + 37f, elapsed * 0.22f)), blend);
            transform.position = new Vector3(Mathf.Sin(angle) * radius, height, Mathf.Cos(angle) * radius);
            transform.LookAt(Vector3.up * 0.55f);
        }

        // Sets the initial viewpoint and scene background.
        public Camera Configure(Color background)
        {
            Camera camera = GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            transform.position = new Vector3(-2.69f, 1.8f, -2.69f);
            transform.LookAt(Vector3.up * 0.55f);
            return camera;
        }
    }
}
