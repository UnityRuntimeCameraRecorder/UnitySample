using System;
using System.IO;

namespace UnityRuntimeCameraRecorder.Example
{
    // Reads the sample's external FFmpeg executable exclusively from the process environment.
    internal static class FfmpegEnvironment
    {
        // Requires an absolute existing executable path in FFMPEG_PATH before recording starts.
        internal static string GetExecutablePath()
        {
            string executable = Environment.GetEnvironmentVariable("FFMPEG_PATH");
            if (string.IsNullOrWhiteSpace(executable))
            {
                throw new InvalidOperationException("Set FFMPEG_PATH to the full path of the FFmpeg executable.");
            }
            if (!Path.IsPathFullyQualified(executable) || !File.Exists(executable))
            {
                throw new FileNotFoundException("FFMPEG_PATH must point to an existing executable using its full path.", executable);
            }
            return executable;
        }
    }
}
