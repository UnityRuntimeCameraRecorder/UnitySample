# UnitySample

An editable Unity scene demonstrating camera recording: a lit cube, wind-blown grass, particles and mist, with an orbit camera and a fixed camera.

## Run and record

Video recording requires Windows x64, an NVIDIA NVENC GPU with a recent driver, and [FFmpeg](https://ffmpeg.org/download.html). Set `FFMPEG_PATH` to its full executable path before launching Unity or the app.

Open `Assets/Scenes/RecordingSample.unity` in Unity **6000.0.61f1**, press Play, select Camera 1, Camera 2 and/or Screen, then click **Record**. Each source produces a separate MP4; Screen includes the application UI. **Stop recording** finalizes the files.

## Build

Install Unity **6000.0.61f1** with your platform's build support module. Put `Unity` / `Unity.exe` in `PATH` or set `UNITY_EXECUTABLE`. Save and close Unity, then run in Bash (Git Bash on Windows):

```bash
bash ./build.sh
```

The script builds for the current OS into `Builds/Windows`, `Builds/Linux` or `Builds/macOS`; logs go to `build.log`. It uses the included plugin DLLs. Linux/macOS players support preview, not the current NVIDIA video backend; they have not been tested locally.

## Command-line example

With `FFMPEG_PATH` set in the launching process:

```bash
./Builds/Windows/UnitySample.exe --render 4k --resolution 4k --fps 60 --codec hevc --preset p5 --vsync on --aa 4 --record camera1,camera2 --duration 30 --quit-after-recording
```

Switches use the same settings as the UI:

| Switch | Values |
| --- | --- |
| `--render`, `--resolution` | `fullhd`, `4k`: render/window size and video size respectively |
| `--fps` | `30`, `60` |
| `--codec` | `h264`, `hevc` |
| `--preset` | `p2` to `p6` |
| `--vsync`, `--fullscreen` | `on`, `off` |
| `--aa` | `off`, `2`, `4`, `8` |
| `--camera` | `1`, `2`: preview only |
| `--record` | Comma-separated `camera1,camera2,screen`; `both` or `all` also accepted |
| `--duration` | Seconds, at least `0.1` |
| `--quit-after-recording` | Finalize and exit; requires `--record` |
| `--purge` | Permanently delete only sample-prefixed output files before recording |

Without `--record`, the app only previews. Keep it visible: batch/headless player runs are not valid recording tests. With `FFMPEG_PATH` set, run `bash ./run.sh` in Git Bash on Windows: it records camera 1 and screen for 10 seconds in 4K/60 max, HEVC/P5, VSync and MSAA 4x, exits after finalization and prints the session's JSON statistics. It uses `--purge`, deleting only sample-prefixed output files. Linux/macOS video recording still needs a compatible backend.

## Output and editing

Videos and `*.stats.json` go into `output` beside the executable (project root in Play mode). Statistics include Unity render FPS; output FPS is a ceiling. Edit UI positions under `Diagnostics > ApplicationCanvas` outside Play mode.

The bundled libraries are [UnityMediaRecorder](https://github.com/end3rbyte/UnityMediaRecorder), [Direct3DVideoEncoder](https://github.com/end3rbyte/Direct3DVideoEncoder) and [FFmpegMediaWriter](https://github.com/end3rbyte/FFmpegMediaWriter).
