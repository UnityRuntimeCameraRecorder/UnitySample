using UnityEngine;

namespace UnityMediaRecorder.Example
{
    // Draws diagnostics after scene post-processing without producing a separate recording.
    [RequireComponent(typeof(Camera))]
    public sealed class OverlayCamera : MonoBehaviour
    {
        // Matches the scene projection and renders only the diagnostic layer.
        public Camera Configure(Camera sceneCamera, int overlayLayer)
        {
            Camera camera = GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Depth;
            camera.cullingMask = 1 << overlayLayer;
            camera.depth = sceneCamera.depth + 1f;
            camera.fieldOfView = sceneCamera.fieldOfView;
            camera.nearClipPlane = sceneCamera.nearClipPlane;
            camera.farClipPlane = sceneCamera.farClipPlane;
            return camera;
        }
    }
}
