using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace UnityMediaRecorder.Example
{
    // Builds the sample world independently of cameras and recording.
    public sealed class SampleEnvironment : MonoBehaviour
    {
        // Creates the cube, floor, lighting and atmospheric scenery.
        public void Build()
        {
            SampleCube.Create();

            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = Vector3.one * 2f;
            ApplyExampleMaterial(floor, new Color(0.18f, 0.2f, 0.24f));
            floor.GetComponent<Renderer>().receiveShadows = true;

            GameObject lightObject = new GameObject("KeyLight");
            Light keyLight = lightObject.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.intensity = 1.2f;
            keyLight.shadows = LightShadows.Soft;
            keyLight.shadowStrength = 0.75f;
            keyLight.shadowBias = 0.035f;
            keyLight.shadowNormalBias = 0.25f;
            lightObject.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            RenderSettings.ambientLight = new Color(0.25f, 0.25f, 0.3f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.16f, 0.2f, 0.25f);
            RenderSettings.fogDensity = 0.022f;
            SampleAtmosphere.CreateFloatingParticles();
            SampleAtmosphere.CreateHumidityMist();
            SampleGrass.Create();
        }

        // Assigns the example shader explicitly so standalone shader stripping cannot produce magenta objects.
        private static void ApplyExampleMaterial(GameObject target, Color color)
        {
            Shader shader = Shader.Find("UnitySample/Lit");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "The UnitySample/Lit shader is missing; copy Assets/UnitySample into the Unity project.");
            }

            var material = new Material(shader);
            material.color = color;
            target.GetComponent<Renderer>().material = material;
        }
    }
}
