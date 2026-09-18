using System;

namespace UnityMediaRecorder.Example
{
    // Stores session settings and render measurements, not encoded-video frame counts.
    [Serializable]
    public sealed class RecordingSessionStats
    {
        public string status, startedUtc, finishedUtc, gpu, unityVersion, error;
        public int renderWidth, renderHeight, outputWidth, outputHeight, maximumVideoFps, msaaSamples, vSyncCount;
        public int encodingPreset, quantizationParameter;
        public string rateControl;
        public string videoCodec, qualityPreset, audioCodec, videoDiagnosticsJson;
        public int videoBitRate, maximumVideoBitRate, audioBitRate, audioSampleRate, audioChannels;
        public bool camera1, camera2, screen;
        public float requestedDurationSeconds, captureDurationSeconds, finalizationDurationSeconds;
        public int camera1RenderedFrames, camera2RenderedFrames, screenRenderedFrames;
        public float camera1AverageRenderFps, camera2AverageRenderFps, screenAverageRenderFps;
        public string[] videoFiles;
        public long[] videoFileBytes;
    }
}
