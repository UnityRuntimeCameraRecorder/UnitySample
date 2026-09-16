# UnitySample

Standalone Unity example for `UnityMediaRecorder`. This repository contains example scripts and shaders, not the recorder library or its dependencies. It has no dependency on Valheim or BepInEx.

Under `Assets/UnitySample`, `Scene` contains the recorded world: cube, scenery and their shaders. `Recorders` contains the moving and fixed recording cameras and the capture controller; `Recorders/Effects` contains temporal post-processing and its shader. `Diagnostics` contains measurements, diagnostic overlays and their overlay camera. `SampleController.cs` remains at the root to connect these components. Keep all nested `Resources` folders when copying the sample into Unity.

Camera behavior is separated into `OrbitCamera.cs` (moving view), `FixedCamera.cs` (stationary view) and `OverlayCamera.cs` (diagnostics only).

`SampleCube.cs` creates the cube and owns its position, material and shadow configuration.

Independent Unity components separate responsibilities: `SampleController` builds and connects the sample, `SampleEnvironment` creates the scenery, `SampleCaptureController` manages capture sessions and benchmarks, and `SampleDiagnostics` owns the overlays and render measurements. `SampleGrass` and `SampleAtmosphere` are ordinary construction helpers. `TemporalMotionSmoothing` is a separate post-processing component. There are no partial classes.

The sample creates a shadow-casting cube, shadow-receiving floor, directional light, wind-animated tall-grass patches, softly drifting airborne particles, low humidity mist, depth fog and an orbiting camera at runtime. It enables vertical synchronization and overlays live render time, capture resolution, target rate, elapsed time, rendered-frame count and GPU name. By default, it captures both cameras as PNG image sequences for ten seconds in `Desktop/UnitySample`.

Each camera produces one PNG per second. The numbered sequences are stored in separate `<capture name>_Frames` directories; the example does not create MP4 or MKV files.

## Run it

1. Create an empty 3D Unity project compatible with the Unity assemblies used to build `UnityMediaRecorder`.
2. Ensure Unity's built-in Particle System module is enabled in Package Manager.
3. Copy `UnityMediaRecorder.dll` and `FFmpegMediaWriter.dll` into `Assets/Plugins`.
4. Copy `Assets/UnitySample` from this repository into your project's `Assets` folder.
5. Open an empty scene and enter Play mode.

No scene objects, inspector configuration, FFmpeg installation or native NVIDIA DLL are required for the PNG example.

The standalone example also accepts `CAPTURE_WIDTH`, `CAPTURE_HEIGHT`, `CAPTURE_FRAME_RATE`, `CAPTURE_MSAA` and `CAPTURE_PROFILE` environment variables. MSAA defaults to 4 samples. These overrides make it possible to generate profiles such as Full HD/30 FPS and 4K/60 FPS from the same build.

Set `CAPTURE_BENCHMARK_MODE` to `single-render`, `dual-render`, `dual-nvenc` or `dual-nvenc-no-temporal` to run a ten-second controlled comparison instead of the PNG example. Each run writes one `CAPTURE_BENCHMARK` log entry containing the exact rendered-frame counts and average FPS. The NVENC modes require FFmpeg and the native encoder DLL.

For video modes, copy `Direct3DVideoEncoder.dll` into `Assets/Plugins/x86_64`, use Direct3D 11 with an NVIDIA GPU, and set `FFMPEG_PATH` to the separately installed FFmpeg executable. Run a visible standalone player; do not use `-batchmode` for recording.

Released under the [MIT License](LICENSE).
