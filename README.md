# UnitySample

An editable Unity scene demonstrating camera recording: a lit cube, wind-blown grass, particles and mist, with an orbit camera and a fixed camera.

The sample plays "Iced Out" by [LSPLASH](https://soundcloud.com/lightningsplash/icedout) automatically at startup. The Unity audio asset is a lossless FLAC conversion of the AAC stream; LSPLASH allows their music in videos and livestreams.

Set `CAPTURE_KEEP_INTERMEDIATE=1` before recording to keep the MKV files with lossless audio for comparison with the final encoded audio in the MP4 files.

## Run and record

Video recording requires Windows x64, an NVIDIA NVENC GPU with a recent driver, and [FFmpeg](https://ffmpeg.org/download.html). Set `FFMPEG_PATH` to its full executable path before launching Unity or the app.

Open `Assets/Scenes/RecordingSample.unity` in Unity **6000.0.61f1**, press Play, select Camera 1, Camera 2 and/or Screen, then click **Record**. Each selected source produces a separate MP4, and Screen includes the application UI. Recording continues until **Stop recording** is clicked. An explicit command-line `--duration` requests a timed recording. **Stop recording** finalizes the files.

## Build

Install Unity **6000.0.61f1** with your platform's build support module. Put `Unity` / `Unity.exe` in `PATH` or set `UNITY_EXECUTABLE`. Save and close Unity, then run in Bash (Git Bash on Windows):

```bash
bash ./build.sh
```

The script builds for the current OS into `Builds/Windows`, `Builds/Linux` or `Builds/macOS`; logs go to `build.log`. It uses the included plugin DLLs. Linux/macOS players support preview, not the current NVIDIA video backend; they have not been tested locally.

On Windows, clean, build and package the Release player with MSBuild:

```bash
dotnet msbuild Assembly-CSharp.csproj -t:package
```

The archive is written to `Builds/Packages/UnitySample-Windows-x64.zip`. It contains the Unity player, `UnityRuntimeCameraRecorder.dll`, `Direct3DVideoEncoder.dll` and `FFmpegMediaWriter.dll`. FFmpeg and FFprobe executables are deliberately excluded and must be installed separately.

## Command-line example

With `FFMPEG_PATH` set in the launching process:

```bash
./Builds/Windows/UnitySample.exe --render 4k --resolution 4k --fps 60 --codec h264 --quality high --vsync on --aa 4 --record camera1,camera2 --duration 30 --quit-after-recording
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
| `--duration` | Seconds, at least `0.1` |
| `--quit-after-recording` | Finalize and exit; requires `--record` |
| `--purge` | Permanently delete only sample-prefixed output files before recording |

Without `--record`, the app only previews. Keep it visible: batch/headless player runs are not valid recording tests. With `FFMPEG_PATH` set, run `bash ./run.sh` in Git Bash on Windows: it records camera 1 and screen for 10 seconds in 4K/60 max, H.264/High, VSync and MSAA 4x, exits after finalization and prints the session's JSON statistics. It uses `--purge`, deleting only sample-prefixed output files. Linux/macOS video recording still needs a compatible backend.

## Output and editing

Videos and `*.stats.json` go into `output` beside the executable (project root in Play mode). Statistics include Unity render FPS; output FPS is a ceiling. Edit UI positions under `Diagnostics > ApplicationCanvas` outside Play mode.

The bundled libraries are [UnityRuntimeCameraRecorder](https://github.com/UnityRuntimeCameraRecorder/UnityRuntimeCameraRecorder), [Direct3DVideoEncoder](https://github.com/UnityRuntimeCameraRecorder/Direct3DVideoEncoder) and [FFmpegMediaWriter](https://github.com/UnityRuntimeCameraRecorder/FFmpegMediaWriter).

The Quality menu selects Low, Medium or High (default); H.264 is the default codec. Profile details are documented in [UnityRuntimeCameraRecorder](https://github.com/UnityRuntimeCameraRecorder/UnityRuntimeCameraRecorder#automatic-sdr-quality-profiles), and native settings in [Direct3DVideoEncoder](https://github.com/UnityRuntimeCameraRecorder/Direct3DVideoEncoder#sdr-constant-qp-quality-entry-point). HDR is not supported today. See the [specification](docs/recording-quality-presets-spec.md) and [validation results](docs/recording-quality-presets-validation.md).
