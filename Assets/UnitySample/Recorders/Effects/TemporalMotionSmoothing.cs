using UnityEngine;

namespace UnityMediaRecorder.Example
{
    // Applies temporally reprojected smoothing and motion-vector blur to the example camera.
    [RequireComponent(typeof(Camera))]
    internal sealed class TemporalMotionSmoothing : MonoBehaviour
    {
        private const float HistoryWeight = 0.08f;
        private const float MotionBlurStrength = 0.35f;
        private Material _material;
        private RenderTexture _history;
        private bool _historyValid;

        // Enables the depth and motion-vector textures required by temporal reprojection.
        private void OnEnable()
        {
            Camera cameraComponent = GetComponent<Camera>();
            cameraComponent.depthTextureMode |= DepthTextureMode.Depth | DepthTextureMode.MotionVectors;
            Shader shader = Shader.Find("UnitySample/TemporalMotionSmoothing");
            if (shader == null)
            {
                Debug.LogWarning("The temporal motion smoothing shader is unavailable; the example will render without it.");
                return;
            }

            _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        // Releases the temporal history and its private material.
        private void OnDisable()
        {
            if (_history != null)
            {
                _history.Release();
                Destroy(_history);
                _history = null;
            }

            if (_material != null)
            {
                Destroy(_material);
                _material = null;
            }

            _historyValid = false;
        }

        // Reprojects the previous image and integrates motion blur into the camera output.
        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (_material == null)
            {
                Graphics.Blit(source, destination);
                return;
            }

            EnsureHistory(source);
            if (!_historyValid)
            {
                Graphics.Blit(source, destination);
                Graphics.Blit(source, _history);
                _historyValid = true;
                return;
            }

            _material.SetTexture("_HistoryTex", _history);
            _material.SetFloat("_HistoryWeight", HistoryWeight);
            _material.SetFloat("_MotionBlurStrength", MotionBlurStrength);
            Graphics.Blit(source, destination, _material);
            Graphics.Blit(destination, _history);
        }

        // Recreates the temporal history when the capture resolution or format changes.
        private void EnsureHistory(RenderTexture source)
        {
            if (_history != null &&
                _history.width == source.width &&
                _history.height == source.height &&
                _history.format == source.format)
            {
                return;
            }

            if (_history != null)
            {
                _history.Release();
                Destroy(_history);
            }

            _history = new RenderTexture(source.width, source.height, 0, source.format)
            {
                name = "TemporalMotionHistory",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            _history.Create();
            _historyValid = false;
        }
    }
}
