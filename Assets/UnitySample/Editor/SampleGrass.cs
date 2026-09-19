using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace UnityRuntimeCameraRecorder.Example
{
    // Builds the sample's wind-animated grass.
    internal static class SampleGrass
    {
        // Builds clustered crossed-quad grass blades animated entirely by the GPU.
        public static void Create()
        {
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var ultraviolet = new List<Vector2>();
            var triangles = new List<int>();
            var random = new System.Random(7319);
            Vector2[] patchCenters =
            {
                new Vector2(-2.7f, -1.8f),
                new Vector2(-2.2f, 1.7f),
                new Vector2(2.5f, -1.5f),
                new Vector2(2.8f, 1.5f),
                new Vector2(-0.2f, 3.1f),
                new Vector2(0.3f, -3.2f)
            };

            foreach (Vector2 center in patchCenters)
            {
                for (int blade = 0; blade < 125; blade++)
                {
                    float radius = Mathf.Sqrt((float)random.NextDouble()) * 1.25f;
                    float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                    var position = new Vector3(
                        center.x + Mathf.Cos(angle) * radius,
                        0f,
                        center.y + Mathf.Sin(angle) * radius);
                    float height = Mathf.Lerp(0.5f, 1.25f, (float)random.NextDouble());
                    float width = Mathf.Lerp(0.022f, 0.052f, (float)random.NextDouble());
                    float rotation = (float)random.NextDouble() * Mathf.PI;
                    Color variation = Color.Lerp(
                        new Color(0.65f, 0.82f, 0.48f),
                        new Color(0.9f, 1f, 0.62f),
                        (float)random.NextDouble());
                    AddGrassBlade(vertices, colors, ultraviolet, triangles, position, width, height, rotation, variation);
                }
            }

            var mesh = new Mesh { name = "ProceduralGrassPatches" };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetUVs(0, ultraviolet);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            Bounds bounds = mesh.bounds;
            bounds.Expand(new Vector3(0.5f, 0.2f, 0.5f));
            mesh.bounds = bounds;

            GameObject grass = new GameObject("WindGrassPatches");
            grass.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = grass.AddComponent<MeshRenderer>();
            Shader shader = Shader.Find("UnitySample/WindGrass");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "The UnitySample/WindGrass shader is missing; copy Assets/UnitySample into the Unity project.");
            }

            renderer.material = new Material(shader);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }

        // Adds one crossed pair of tapered grass cards to the shared procedural mesh.
        private static void AddGrassBlade(
            List<Vector3> vertices,
            List<Color> colors,
            List<Vector2> ultraviolet,
            List<int> triangles,
            Vector3 position,
            float width,
            float height,
            float rotation,
            Color color)
        {
            const int verticalSegments = 6;
            for (int card = 0; card < 2; card++)
            {
                float angle = rotation + card * Mathf.PI * 0.5f;
                Vector3 side = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * width;
                int first = vertices.Count;
                for (int segment = 0; segment <= verticalSegments; segment++)
                {
                    float vertical = segment / (float)verticalSegments;
                    Vector3 center = position + Vector3.up * height * vertical;
                    vertices.Add(center - side);
                    vertices.Add(center + side);
                    colors.Add(color);
                    colors.Add(color);
                    ultraviolet.Add(new Vector2(0f, vertical));
                    ultraviolet.Add(new Vector2(1f, vertical));
                }

                for (int segment = 0; segment < verticalSegments; segment++)
                {
                    int lowerLeft = first + segment * 2;
                    int upperLeft = lowerLeft + 2;
                    triangles.Add(lowerLeft);
                    triangles.Add(upperLeft);
                    triangles.Add(lowerLeft + 1);
                    triangles.Add(lowerLeft + 1);
                    triangles.Add(upperLeft);
                    triangles.Add(upperLeft + 1);
                }
            }
        }
    }
}
