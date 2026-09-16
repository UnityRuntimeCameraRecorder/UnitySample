using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityMediaRecorder.Example
{
    // Owns the sample cube's appearance and shadow configuration.
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class SampleCube : MonoBehaviour
    {
        private Material _material;

        // Creates a cube primitive with its dedicated sample component.
        public static SampleCube Create()
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "RecordedCube";
            return cube.AddComponent<SampleCube>();
        }

        // Applies the same position, color and shadows as the original sample cube.
        private void Awake()
        {
            transform.position = Vector3.up * 0.5f;
            Shader shader = Shader.Find("UnitySample/Lit");
            if (shader == null)
                throw new InvalidOperationException("The UnitySample/Lit shader is missing; copy Assets/UnitySample into the Unity project.");
            _material = new Material(shader) { color = new Color(0.15f, 0.55f, 1f) };
            MeshRenderer renderer = GetComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
        }

        // Releases the runtime material owned by this cube.
        private void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }
    }
}
