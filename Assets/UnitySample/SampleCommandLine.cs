using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace UnityRuntimeCameraRecorder.Example
{
    // Applies optional startup switches to the same controls used by the Canvas.
    public sealed class SampleCommandLine : MonoBehaviour
    {
        // Waits for scene initialization, validates all arguments and optionally records once.
        private IEnumerator Start()
        {
            yield return null;
            string[] args = Environment.GetCommandLineArgs();
            var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var valid = new Dictionary<string, string[]>
            {
                {
                    "--render",
                    new[]
                    {
                        "fullhd",
                        "4k"
                    }
                },
                {
                    "--resolution",
                    new[]
                    {
                        "fullhd",
                        "4k"
                    }
                },
                {
                    "--fps",
                    new[]
                    {
                        "30",
                        "60"
                    }
                },
                {
                    "--codec",
                    new[] { "h264", "hevc" }
                },
                {
                    "--quality",
                    new[] { "low", "medium", "high" }
                },
                {
                    "--fullscreen",
                    new[]
                    {
                        "on",
                        "off"
                    }
                },
                {
                    "--vsync",
                    new[]
                    {
                        "on",
                        "off"
                    }
                },
                {
                    "--aa",
                    new[]
                    {
                        "off",
                        "2",
                        "4",
                        "8"
                    }
                },
                {
                    "--camera",
                    new[]
                    {
                        "1",
                        "2"
                    }
                },
                {
                    "--record",
                    null
                },
                {
                    "--duration",
                    null
                },
                {
                    "--purge",
                    Array.Empty<string>()
                },
                {
                    "--quit-after-recording",
                    Array.Empty<string>()
                },
                {
                    "--single-output",
                    Array.Empty<string>()
                }
            };
            string error = null;
            for (int i = 1; i < args.Length; i++)
            {
                string key = args[i].ToLowerInvariant();
                if (!key.StartsWith("--", StringComparison.Ordinal))
                {
                    continue; // Leave Unity's own single-dash arguments untouched.
                }

                if (!valid.TryGetValue(key, out string[] choices))
                {
                    error = "Unknown switch: " + key;
                    break;
                }

                if (options.ContainsKey(key))
                {
                    error = "Duplicate switch: " + key;
                    break;
                }

                if (choices != null && choices.Length == 0)
                {
                    options.Add(key, "true");
                    continue;
                }

                if (++i >= args.Length)
                {
                    error = "Missing value for " + key;
                    break;
                }

                string value = args[i].ToLowerInvariant();
                if (key == "--record" && !IsValidRecordingSources(value))
                {
                    error = "Recording sources must be camera1, camera2 or screen, separated by commas (or both/all).";
                    break;
                }

                if (choices != null && Array.IndexOf(choices, value) < 0)
                {
                    error = "Invalid value for " + key;
                    break;
                }

                if (key == "--duration" && (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float seconds) || float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0.1f))
                {
                    error = "Duration must be at least 0.1 seconds.";
                    break;
                }

                options.Add(key, value);
            }

            if (options.ContainsKey("--quit-after-recording") && !options.ContainsKey("--record"))
            {
                error = "--quit-after-recording requires --record.";
            }
            if (options.ContainsKey("--single-output") && !options.ContainsKey("--record"))
            {
                error = "--single-output requires --record.";
            }

            if (error != null)
            {
                Debug.LogError("Command line: " + error);
                if (!Application.isEditor)
                {
                    Application.Quit(1);
                }

                yield break;
            }

            var scene = GetComponent<SampleSceneController>();
            if (options.ContainsKey("--purge"))
            {
                try
                {
                    PurgeOutput();
                }
                catch (Exception exception)
                {
                    Debug.LogError("Cannot purge output: " + exception.Message);
                    if (!Application.isEditor)
                    {
                        Application.Quit(1);
                    }

                    yield break;
                }
            }

            var ui = scene.Diagnostics.GetComponent<CameraViewSwitcher>();
            if (options.TryGetValue("--render", out string render))
            {
                ui.RenderResolution.value = render == "fullhd" ? 0 : 1;
            }

            if (options.TryGetValue("--fullscreen", out string fullscreen))
            {
                ui.Fullscreen.isOn = fullscreen == "on";
            }

            if (options.TryGetValue("--vsync", out string vsync))
            {
                ui.VSync.isOn = vsync == "on";
            }

            if (options.TryGetValue("--aa", out string aa))
            {
                ui.AntiAliasing.value = Array.IndexOf(valid["--aa"], aa);
            }

            if (options.TryGetValue("--resolution", out string resolution))
            {
                ui.OutputResolution.value = resolution == "fullhd" ? 0 : 1;
            }

            if (options.TryGetValue("--fps", out string fps))
            {
                ui.OutputFrameRate.value = fps == "30" ? 0 : 1;
            }

            if (options.TryGetValue("--quality", out string quality))
            {
                int selectedQuality = Array.IndexOf(valid["--quality"], quality);
                ui.RecordingQuality.SetValueWithoutNotify(selectedQuality);
                ui.SetRecordingQuality(selectedQuality);
            }
            if (options.TryGetValue("--codec", out string codec))
            {
                ui.VideoCodec.value = codec == "h264" ? 0 : 1;
            }

            if (options.TryGetValue("--camera", out string camera))
            {
                if (camera == "1")
                {
                    ui.SelectCamera1();
                }
                else
                {
                    ui.SelectCamera2();
                }
            }

            if (options.TryGetValue("--duration", out string duration))
            {
                scene.Captures.SetDuration(float.Parse(duration, CultureInfo.InvariantCulture));
            }

            if (!options.TryGetValue("--record", out string record))
            {
                yield break;
            }

            var sources = new HashSet<string>(record.Split(','));
            ui.RecordCamera1.isOn = sources.Contains("camera1") || record == "both" || record == "all";
            ui.RecordCamera2.isOn = sources.Contains("camera2") || record == "both" || record == "all";
            ui.RecordScreen.isOn = sources.Contains("screen") || record == "all";
            if (options.ContainsKey("--single-output"))
            {
                ui.SingleVideoOutput.isOn = true;
            }
            // Allow asynchronous display changes to settle before taking the first frame.
            yield return new WaitForSecondsRealtime(1);
            try
            {
                FfmpegEnvironment.GetExecutablePath();
            }
            catch (Exception exception)
            {
                Debug.LogError("Recording failed — " + exception.Message);
                if (!Application.isEditor)
                {
                    Application.Quit(1);
                }
                yield break;
            }
            ui.ToggleRecording();
            while (scene.Captures.IsCapturing)
            {
                yield return null;
            }

            if (options.ContainsKey("--quit-after-recording"))
            {
                ui.Quit();
            }
        }

        // Accepts unique comma-separated source names and the existing convenience aliases.
        private static bool IsValidRecordingSources(string value)
        {
            if (value == "both" || value == "all")
            {
                return true;
            }

            var seen = new HashSet<string>();
            foreach (string source in value.Split(','))
            {
                if ((source != "camera1" && source != "camera2" && source != "screen") || !seen.Add(source))
                {
                    return false;
                }
            }

            return seen.Count > 0;
        }

        // Deletes only matching top-level files from this application's output directory.
        private static void PurgeOutput()
        {
            string directory = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "output"));
            if (!Directory.Exists(directory))
            {
                return;
            }

            if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("Output purge refuses a linked output directory.");
            }

            int removed = 0;
            foreach (string file in Directory.GetFiles(directory, "*", SearchOption.TopDirectoryOnly))
            {
                string target = Path.GetFullPath(file);
                if (!string.Equals(Path.GetDirectoryName(target), directory, StringComparison.OrdinalIgnoreCase))
                {
                    throw new IOException("Output purge target is outside the output directory.");
                }

                string name = Path.GetFileName(target);
                if (!name.StartsWith("UnitySample_", StringComparison.OrdinalIgnoreCase) && !name.StartsWith("CubeOrbit_", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Do not follow symbolic links or junction-like file entries during cleanup.
                if ((File.GetAttributes(target) & FileAttributes.ReparsePoint) != 0)
                {
                    continue;
                }

                File.Delete(target);
                removed++;
            }

            Debug.Log($"Output purge: permanently removed {removed} UnitySample file(s).");
        }
    }
}
