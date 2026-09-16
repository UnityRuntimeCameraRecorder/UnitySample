using UnityEngine;

namespace UnityMediaRecorder.Example
{
    // Configures the sample's stationary view independently of recording.
    [RequireComponent(typeof(Camera))]
    public sealed class FixedCamera : MonoBehaviour
    {
        // Sets the fixed viewpoint and scene background.
        public Camera Configure(Color background)
        {
            Camera camera = GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            transform.position = new Vector3(-5.2f, 3.2f, -5.2f);
            transform.LookAt(Vector3.up * 0.55f);
            return camera;
        }
    }
}
