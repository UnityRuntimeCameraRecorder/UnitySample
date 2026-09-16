# UnitySample

An editable Unity scene demonstrating camera recording: a lit cube, wind-blown grass, particles and mist, with an orbit camera and a fixed camera.

## Run and record

The compiled app needs Windows x64, an NVIDIA NVENC GPU, a recent driver and a separate FFmpeg installation. Keep the EXE, `UnitySample_Data` and companion files together.

Open the project in Unity **6000.0.61f1**, then `Assets/Scenes/RecordingSample.unity`. In the Inspector, supply **Ffmpeg Path** on `SampleCaptureController` and save the scene.

Press Play, select the recording sources and click **Record**:

- Camera 1: orbit camera video.
- Camera 2: fixed camera video.
- Screen: the application's displayed view, including UI, not the Windows desktop.

Each source produces a separate MP4. Camera buttons only change the preview. **Stop recording** ends capture and finalizes files; **Quit** finalizes before exiting.

The app starts windowed in Full HD. Left controls set render/window resolution, fullscreen, VSync and MSAA. Right controls set output resolution, FPS ceiling, codec and preset. Defaults: HEVC, P5, 4K output, up to 60 FPS; presets P2–P6 are exposed. NVENC uses asynchronous completion. Render and output resolutions are separate; these settings cannot change during recording.

## Build

### Install FFmpeg on Windows

Download FFmpeg for Windows from the [official download page](https://ffmpeg.org/download.html) and extract the archive. Find `bin\ffmpeg.exe` in the extracted folder. Enter its full path in **Ffmpeg Path** on `SampleCaptureController`, save the scene and rebuild the player.

Configure FFmpeg in the scene before building. Enable the scene in **Build Profiles > Scene List**, save and close Unity, then run:

```bat
build.bat
```

`Unity.exe` must be in your system PATH. The script produces `Builds/Windows/UnitySample.exe` and launches it on success. It includes the DLLs already in `Assets/UnitySample/Plugins`; it does not rebuild or refresh those libraries. Build diagnostics go to `build.log`.

## Command-line example

With the FFmpeg path already saved into your build:

```powershell
& .\Builds\Windows\UnitySample.exe --render 4k --resolution 4k --fps 60 --codec hevc --preset p5 --vsync on --aa 4 --record camera1,camera2 --duration 30 --quit-after-recording
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

Without `--record`, the app only previews. Keep it visible: batch/headless player runs are not valid recording tests. `record.bat` runs a predefined recording and prints statistics; it currently uses `--purge`.

## Output and editing

Videos and session `*.stats.json` files go into `output` beside the EXE (project root in editor Play mode). The directory must be writable. Statistics describe settings, files and Unity render FPS, not decoded video frame counts. The FPS setting is a ceiling, not a guarantee.

To move UI elements, leave Play mode, expand `Diagnostics > ApplicationCanvas`, select a control and use its Rect Transform or the Rect tool (`T`) in Scene view. Save the scene. Scene objects and materials are also editable; scenery is not regenerated during Play.

The bundled libraries are [UnityMediaRecorder](https://github.com/end3rbyte/UnityMediaRecorder), [Direct3DVideoEncoder](https://github.com/end3rbyte/Direct3DVideoEncoder) and [FFmpegMediaWriter](https://github.com/end3rbyte/FFmpegMediaWriter).
