# UnitySample

An editable Unity sample demonstrating runtime recording from two cameras and the player screen, either as separate videos or as one automatically edited multi-source video.

## Run and record

Video recording requires Windows x64, an NVIDIA NVENC GPU with a recent driver, and [FFmpeg](https://ffmpeg.org/download.html). Set `FFMPEG_PATH` to the absolute path of the FFmpeg `bin` directory containing `ffmpeg.exe` and `ffprobe.exe` before launching Unity or the app.

PowerShell:

```powershell
$env:FFMPEG_PATH = "C:\tools\ffmpeg\bin"
```

Git Bash:

```bash
export FFMPEG_PATH='/c/tools/ffmpeg/bin'
```

`UnitySample` passes this directory to `RecordingSettings.FfmpegPath`. The recorder resolves `ffmpeg.exe` and, when statistics are enabled, `ffprobe.exe` inside it.

Open `Assets/Scenes/RecordingSample.unity` in Unity **6000.0.61f1**, press Play, select Camera 1, Camera 2 and/or Screen, then click **Record**. Each selected source produces a separate MP4, and Screen includes the application UI. Recording continues until **Stop recording** is clicked. An explicit command-line `--duration` requests a timed recording. **Stop recording** finalizes the files.

## Build

Install Unity **6000.0.61f1** with your platform's build support module. Put `Unity` / `Unity.exe` in `PATH` or set `UNITY_EXECUTABLE`. Save and close Unity, then run in Bash (Git Bash on Windows):

```bash
bash ./build.sh
```

The script builds for the current OS into `Builds/Windows`, `Builds/Linux` or `Builds/macOS`; logs go to `Builds/Logs/build.log`. It uses the included plugin DLLs. Linux/macOS players support preview, not the current NVIDIA video backend; they have not been tested locally.

On Windows, clean, build and package the Release player with MSBuild:

```bash
dotnet msbuild Assembly-CSharp.csproj -t:package
```

The archive is written to `Builds/Packages/UnitySample-Windows-x64.zip`. It contains the Unity player, `UnityRuntimeCameraRecorder.dll`, `Direct3DVideoEncoder.dll` and `FFmpegMediaWriter.dll`. FFmpeg and FFprobe executables are deliberately excluded and must be installed separately.

## Command-line example

With `FFMPEG_PATH` set in the launching process:

```bash
./Builds/Windows/UnitySample.exe --render 4k --resolution 4k --fps 60 --codec h264 --quality high --vsync on --aa 4 --record camera1,camera2 --duration 30 --statistics --quit-after-recording
```

Switches use the same settings as the UI:

| Switch | Values |
| --- | --- |
| `--render`, `--resolution` | `fullhd`, `4k`: render/window size and video size respectively |
| `--fps` | `30`, `60` |
| `--codec` | `h264`, `hevc` |
| `--quality` | `low`, `medium`, `high` |
| `--vsync`, `--fullscreen` | `on`, `off` |
| `--aa` | `off`, `2`, `4`, `8` |
| `--camera` | `1`, `2`: preview only |
| `--record` | Comma-separated `camera1,camera2,screen`; `both` or `all` also accepted |
| `--single-output` | Combine all selected recording sources into one automatically edited MP4 |
| `--duration` | Seconds, at least `0.1` |
| `--statistics` | Generate a statistics text file for each output video |
| `--quit-after-recording` | Finalize and exit; requires `--record` |
| `--purge` | Permanently delete only sample-prefixed output files before recording |

Without `--record`, the app only previews. Keep it visible: batch/headless player runs are not valid recording tests. With `FFMPEG_PATH` set, run `bash ./run.sh` in Git Bash on Windows: it records camera 1 and screen for 10 seconds in 4K/60 max, H.264/High, VSync and MSAA 4x, exits after finalization and prints the session's JSON statistics. It uses `--purge`, deleting only sample-prefixed output files. Linux/macOS video recording still needs a compatible backend.

## Output and editing

Videos go into `output` beside the executable (project root in Play mode). With `--statistics`, each video also produces a `*.stats.txt` file. The **Export JPG** button writes one second of JPEG frames into a subdirectory. Statistics include Unity render FPS; output FPS is a ceiling. Edit UI positions under `Diagnostics > ApplicationCanvas` outside Play mode.

The bundled libraries are [UnityRuntimeCameraRecorder](https://github.com/CineCapture/UnityRuntimeCameraRecorder), [Direct3DVideoEncoder](https://github.com/CineCapture/Direct3DVideoEncoder) and [FFmpegMediaWriter](https://github.com/CineCapture/FFmpegMediaWriter).

The Quality menu selects Low, Medium, High or Highest (default); H.264 is the default codec. Profile details are documented in [UnityRuntimeCameraRecorder](https://github.com/CineCapture/UnityRuntimeCameraRecorder#quality-profiles), and native settings in [Direct3DVideoEncoder](https://github.com/CineCapture/Direct3DVideoEncoder#sdr-constant-qp-quality-entry-point). HDR is not supported today.

## Resources

Soundtrack: ["Iced Out" by LSPLASH](https://soundcloud.com/lightningsplash/icedout), available for use in videos and livestreams.
