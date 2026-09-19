using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace UnityRuntimeCameraRecorder.Example
{
    // Builds the sample's airborne particles and humidity mist.
    internal static class SampleAtmosphere
    {
        // Creates softly drifting airborne particles around the recorded subject.
        public static void CreateFloatingParticles()
        {
            GameObject particleObject = new GameObject("FloatingAirParticles");
            particleObject.transform.position = new Vector3(0f, 2f, 0f);
            ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.prewarm = true;
            main.maxParticles = 300;
            main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 14f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.015f, 0.08f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.075f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.75f, 0.88f, 1f, 0.18f),
                new Color(1f, 0.88f, 0.58f, 0.42f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 36f;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(10f, 4f, 10f);

            ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(0.9f, 1.55f);
            velocity.y = new ParticleSystem.MinMaxCurve(-0.04f, 0.08f);
            velocity.z = new ParticleSystem.MinMaxCurve(0.24f, 0.58f);

            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(0.12f, 0.32f);
            noise.frequency = 0.18f;
            noise.scrollSpeed = 0.12f;
            noise.damping = true;

            ParticleSystemRenderer renderer = particleObject.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = -10;
            Shader shader = Shader.Find("UnitySample/Particle");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "The UnitySample/Particle shader is missing; copy Assets/UnitySample into the Unity project.");
            }

            renderer.material = new Material(shader);
            particles.Play();
        }

        // Creates broad translucent mist layers that drift slowly near the ground.
        public static void CreateHumidityMist()
        {
            GameObject mistObject = new GameObject("HumidityMist");
            mistObject.transform.position = new Vector3(0f, 0.8f, 0f);
            ParticleSystem mist = mistObject.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = mist.main;
            main.loop = true;
            main.prewarm = true;
            main.maxParticles = 90;
            main.startLifetime = new ParticleSystem.MinMaxCurve(12f, 22f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.01f, 0.045f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 2.6f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.62f, 0.72f, 0.8f, 0.012f),
                new Color(0.78f, 0.84f, 0.88f, 0.045f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = mist.emission;
            emission.rateOverTime = 5f;

            ParticleSystem.ShapeModule shape = mist.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(12f, 1.6f, 12f);

            ParticleSystem.VelocityOverLifetimeModule velocity = mist.velocityOverLifetime;
            velocity.enabled = true;
            velocity.x = new ParticleSystem.MinMaxCurve(0.015f, 0.045f);
            velocity.y = new ParticleSystem.MinMaxCurve(-0.005f, 0.012f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.02f, 0.02f);

            ParticleSystem.NoiseModule noise = mist.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(0.025f, 0.07f);
            noise.frequency = 0.06f;
            noise.scrollSpeed = 0.018f;
            noise.damping = true;

            ParticleSystemRenderer renderer = mistObject.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = -20;
            Shader shader = Shader.Find("UnitySample/Particle");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "The UnitySample/Particle shader is missing; copy Assets/UnitySample into the Unity project.");
            }

            renderer.material = new Material(shader);
            mist.Play();
        }
    }
}
