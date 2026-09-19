using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace UnityRuntimeCameraRecorder.Example
{
    // Exports a short high-quality JPEG sequence from one sample camera.
    public sealed class SamplePngExporter : MonoBehaviour
    {
        public bool IsExporting { get; private set; }

        // Starts a one-second PNG export unless another export is active.
        public void ExportOneSecond(Camera source, int framesPerSecond, int antiAliasingSamples)
        {
            if (IsExporting)
            {
                return;
            }

            StartCoroutine(ExportRoutine(source, framesPerSecond, antiAliasingSamples));
        }

        // Captures for one second and then drains the asynchronous PNG writer.
        private IEnumerator ExportRoutine(Camera source, int framesPerSecond, int antiAliasingSamples)
        {
            IsExporting = true;
            var recorder = gameObject.AddComponent<UnityRuntimeCameraRecorder>();
            string root = Path.Combine(Path.GetDirectoryName(Application.dataPath), "output");
            string directory = Path.Combine(root, $"JPG_{source.name}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}");
            int width = source.targetTexture != null ? source.targetTexture.width : Screen.width;
            int height = source.targetTexture != null ? source.targetTexture.height : Screen.height;
            var settings = new ImageSequenceSettings
            {
                OutputDirectory = directory,
                FileNamePrefix = "frame_",
                Width = width,
                Height = height,
                CapturesPerSecond = framesPerSecond,
                AntiAliasingSamples = antiAliasingSamples,
                EncoderThreadCount = Mathf.Clamp(SystemInfo.processorCount, 2, 8),
                MaximumQueuedFrames = framesPerSecond,
                MaximumFrameCount = framesPerSecond,
                FileFormat = ImageSequenceFormat.Jpeg,
                JpegQuality = 95
            };

            recorder.StartPngSequence(source, settings);
            while (recorder.CapturedPngFrameCount < framesPerSecond)
            {
                yield return null;
            }
            recorder.StopPngSequence();
            Destroy(recorder);
            IsExporting = false;
            Debug.Log($"JPEG export completed: {directory}");
        }
    }
}
