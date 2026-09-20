using System;
using System.Diagnostics;

namespace UnityRuntimeCameraRecorder.Example
{
    // Reads the installed NVIDIA display driver version on Windows.
    internal static class NvidiaDriverInfo
    {
        // Returns the public NVIDIA driver version or an empty string when unavailable.
        public static string GetVersion()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "nvidia-smi.exe",
                    Arguments = "--query-gpu=driver_version --format=csv,noheader,nounits",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                using (Process process = Process.Start(startInfo))
                {
                    string version = process?.StandardOutput.ReadLine()?.Trim();
                    if (process != null && process.WaitForExit(2000) && process.ExitCode == 0)
                    {
                        return version ?? string.Empty;
                    }
                }
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning("Unable to read the NVIDIA driver version: " + exception.Message);
            }
#endif
            return string.Empty;
        }
    }
}
