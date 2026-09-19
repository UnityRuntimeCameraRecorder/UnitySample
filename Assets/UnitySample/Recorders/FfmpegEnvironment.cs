using System;
using System.IO;

namespace UnityRuntimeCameraRecorder.Example
{
    // Resolves the sample's external FFmpeg tools from the process environment.
    internal static class FfmpegEnvironment
    {
        // Requires FFMPEG_PATH to contain the FFmpeg and FFprobe executables.
        internal static string GetExecutablePath()
        {
            string directory = Environment.GetEnvironmentVariable("FFMPEG_PATH");
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidOperationException("Set FFMPEG_PATH to the absolute path of the FFmpeg bin directory.");
            }
            if (!Path.IsPathFullyQualified(directory) || !Directory.Exists(directory))
            {
                throw new DirectoryNotFoundException("FFMPEG_PATH must point to an existing FFmpeg bin directory: " + directory);
            }

            string executable = Path.Combine(directory, "ffmpeg.exe");
            string probe = Path.Combine(directory, "ffprobe.exe");
            if (!File.Exists(executable) || !File.Exists(probe))
            {
                throw new FileNotFoundException("FFMPEG_PATH must contain ffmpeg.exe and ffprobe.exe.", directory);
            }

            return executable;
        }
    }
}
