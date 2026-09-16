# UnitySample

To move controls, leave Play mode and expand `Diagnostics > ApplicationCanvas` in the Hierarchy. Select a button or checkbox, then edit its Rect Transform position or use the Rect tool (`T`) in the 2D Scene view. Save the scene. The camera diagnostic texts live in the two other Canvas objects under `Diagnostics`; they remain separate so each recorded camera keeps its own overlay. Screen controls use normal serialized Button callbacks and an Input System EventSystem.

**Record screen** creates a separate MP4 of the application's displayed Game view, including its interface (not the Windows desktop or editor panels). It follows preview-camera changes. The **Fullscreen** checkbox and **Quit** button are always visible. **Quit** finalizes any active videos before exiting. In the editor, these controls maximize the Game view and leave Play mode instead of closing Unity. Output dimensions stay fixed if the window changes size.

The left controls select render resolution (Full HD or 4K), fullscreen, MSAA and VSync. VSync also controls the editor Game view. The right controls independently select video output resolution and a 30/60 FPS capture ceiling. Resolution and MSAA changes are locked during recording. Live application FPS, render dimensions and GPU appear only in Play mode; recording progress appears at the bottom.

## Build

Enable `Assets/Scenes/RecordingSample.unity` in **Build Profiles > Scene List**, save and close the editor, then run `build.bat`. It uses `Unity.exe` from PATH and produces `Builds/Windows/UnitySample.exe`, with diagnostics in `build.log`. The bundled plugin DLLs are included, not rebuilt; FFmpeg remains a separate installation.

## Command line

Startup switches use the same settings as the Canvas. They configure a new process, not an already-running application. Example (PowerShell):

```powershell
& .\Builds\Windows\UnitySample.exe --render fullhd --fullscreen off --vsync on --aa 4 --resolution 4k --fps 60 --record both --duration 10 --quit-after-recording
```

| Switch | Values |
| --- | --- |
| `--render` | `fullhd`, `4k` (render/window resolution) |
| `--fullscreen` | `on`, `off` |
| `--vsync` | `on`, `off` |
| `--aa` | `off`, `2`, `4`, `8` (MSAA) |
| `--camera` | `1`, `2` (displayed view) |
| `--resolution` | `fullhd`, `4k` (video output) |
| `--fps` | `30`, `60` (capture ceiling) |
| `--preset` | `p1` to `p7` (NVENC preset; default P4; dropdown on the right) |
| `--record` | Comma-separated `camera1`, `camera2`, `screen`; also `both` or `all` |
| `--duration` | Positive seconds, at least `0.1`; decimal separator `.` |
| `--purge` | No value; permanently delete sample files from `output` before starting |
| `--quit-after-recording` | No value; requires `--record`; exit after files are finalized |

Omitted switches retain startup defaults. Without `--record`, the application only previews. Recordings go to `output` beside the executable. Invalid or duplicate switches are logged and cause the standalone application to exit with code 1. Keep the application visible while recording; do not use Unity's `-nographics` mode.

For example, `--purge --record camera1,camera2` removes old sample files (including videos and statistics) starting with `UnitySample_` or the legacy `CubeOrbit_` before recording. Other files are preserved. Cleanup is opt-in, case-insensitive and does not recurse into directories or follow symbolic links. Deleted files are not moved to the recycle bin. Avoid running cleanup while another instance is recording in the same output directory.

Open this project with Unity 6000.0.61f1, then open `Assets/Scenes/RecordingSample.unity`.

The saved scene contains a lit cube with shadows, wind-bent grass, floating particles, humidity mist, an orbit camera and a fixed camera. Both cameras have temporal smoothing and motion blur. Diagnostic text uses a 2D canvas.

Press Play to preview the scene. **Camera 1** and **Camera 2** select the displayed view. Check **Record camera 1**, **Record camera 2**, or both, then press **Record**. Each selected camera produces its own video. **Stop recording** ends the session early; otherwise it ends after the configured duration. These screen controls are not included in the video.

Select `SampleController` and enable **Record On Play** to record both cameras automatically. Select `Recorders` to change capture resolution, frame rate, duration and FFmpeg path. Defaults are 3840 × 2160 at up to 60 FPS; this is a capture limit, not a guaranteed rendering speed.

Recording requires an NVIDIA GPU supported by NVENC and a separately installed FFmpeg executable. The project includes UnityMediaRecorder, FFmpegMediaWriter and Direct3DVideoEncoder binaries.

Recordings are saved in `output` next to the launched executable. In editor Play mode, `output` is created at the project root. The folder is created automatically and must be writable.

Each recording session also produces a readable `*.stats.json` report in that folder. It records status, UTC timestamps, GPU, Unity version, selected sources, render/output dimensions, FPS ceiling, MSAA, initial VSync setting, requested/actual capture duration, finalization duration, per-camera rendered frames and average render FPS, output file paths/sizes and any error. Render counters stop when finalization begins: these are Unity render measurements, not encoded MP4 frame counts. The report is written at preparation, capture, finalization and completion; an interrupted session can therefore retain an unfinished status.

Scene objects and materials are editable in the Inspector. Grass and atmosphere creation helpers are editor-only; the scenery is not recreated at runtime. `UnitySample > Complete saved scene` restores the sample structure in the active scene, so use it only in the sample scene.
