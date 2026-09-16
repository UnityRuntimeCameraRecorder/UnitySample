# UnitySample

To move controls, leave Play mode and expand `Diagnostics > ApplicationCanvas` in the Hierarchy. Select a button or checkbox, then edit its Rect Transform position or use the Rect tool (`T`) in the 2D Scene view. Save the scene. The camera diagnostic texts live in the two other Canvas objects under `Diagnostics`; they remain separate so each recorded camera keeps its own overlay. Screen controls use normal serialized Button callbacks and an Input System EventSystem.

**Record screen** creates a separate MP4 of the application's displayed Game view, including its interface (not the Windows desktop or editor panels). It follows preview-camera changes. The **Fullscreen** checkbox and **Quit** button are always visible. **Quit** finalizes any active videos before exiting. In the editor, these controls maximize the Game view and leave Play mode instead of closing Unity. Output dimensions stay fixed if the window changes size.

The left controls select render resolution (Full HD or 4K), fullscreen, MSAA and VSync. VSync also controls the editor Game view. The right controls independently select video output resolution and a 30/60 FPS capture ceiling. Resolution and MSAA changes are locked during recording. Live application FPS, render dimensions and GPU appear only in Play mode; recording progress appears at the bottom.

## Build

Enable `Assets/Scenes/RecordingSample.unity` in **Build Profiles > Scene List**, save and close the editor, then run `build.bat`. It uses `Unity.exe` from PATH and produces `Builds/Windows/UnitySample.exe`, with diagnostics in `build.log`. The bundled plugin DLLs are included, not rebuilt; FFmpeg remains a separate installation.

Open this project with Unity 6000.0.61f1, then open `Assets/Scenes/RecordingSample.unity`.

The saved scene contains a lit cube with shadows, wind-bent grass, floating particles, humidity mist, an orbit camera and a fixed camera. Both cameras have temporal smoothing and motion blur. Diagnostic text uses a 2D canvas.

Press Play to preview the scene. **Camera 1** and **Camera 2** select the displayed view. Check **Record camera 1**, **Record camera 2**, or both, then press **Record**. Each selected camera produces its own video. **Stop recording** ends the session early; otherwise it ends after the configured duration. These screen controls are not included in the video.

Select `SampleController` and enable **Record On Play** to record both cameras automatically. Select `Recorders` to change capture resolution, frame rate, duration and FFmpeg path. Defaults are 3840 × 2160 at up to 60 FPS; this is a capture limit, not a guaranteed rendering speed.

Recording requires an NVIDIA GPU supported by NVENC and a separately installed FFmpeg executable. The project includes UnityMediaRecorder, FFmpegMediaWriter and Direct3DVideoEncoder binaries.

Scene objects and materials are editable in the Inspector. Grass and atmosphere creation helpers are editor-only; the scenery is not recreated at runtime. `UnitySample > Complete saved scene` restores the sample structure in the active scene, so use it only in the sample scene.
