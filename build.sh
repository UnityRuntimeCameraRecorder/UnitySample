#!/usr/bin/env bash
# Build the Unity sample for the current operating system.
set -euo pipefail

# Print an actionable error and stop the build.
fail() {
    printf 'ERROR: %s\n' "$*" >&2
    exit 1
}

# Describe the command and its required Unity installation.
usage() {
    printf '%s\n' \
        'Usage: bash build.sh [clean]' \
        'Use clean to remove player artifacts and the Unity Library cache before building.' \
        'Install Unity matching ProjectSettings/ProjectVersion.txt and its target build module.' \
        'Put Unity (Unity.exe on Windows) in PATH or set UNITY_EXECUTABLE to its full path.' \
        'Close this project in the Unity Editor before building.'
}

clean_build=false
for argument in "$@"; do
    case "$argument" in
        clean)
            clean_build=true
            ;;
        --help|-h)
            usage
            exit 0
            ;;
        *)
            fail "Unknown argument: $argument. Use --help."
            ;;
    esac
done

project_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
[[ -f "$project_directory/ProjectSettings/ProjectVersion.txt" ]] || fail 'Unity project settings were not found next to this script.'

# Synchronize recorder-owned Unity assets without maintaining sample copies.
recorder_directory="$(cd -- "$project_directory/UnityRuntimeCameraRecorder" && pwd -P)"
recorder_shader_source="$recorder_directory/Resources/UnityRuntimeCameraRecorderCrossFade.shader"
recorder_shader_meta_source="$recorder_shader_source.meta"
recorder_resources_directory="$project_directory/Assets/UnityRuntimeCameraRecorder/Resources"
[[ -f "$recorder_shader_source" && -f "$recorder_shader_meta_source" ]] || fail 'The recorder crossfade shader package asset is missing.'
mkdir -p -- "$recorder_resources_directory"
cp -- "$recorder_shader_source" "$recorder_shader_meta_source" "$recorder_resources_directory/"

case "$(uname -s)" in
    MINGW*|MSYS*|CYGWIN*)
        platform=Windows
        build_option=-buildWindows64Player
        player_relative_path=Builds/Windows/UnitySample.exe
        ;;
    Linux)
        platform=Linux
        build_option=-buildLinux64Player
        player_relative_path=Builds/Linux/UnitySample
        ;;
    Darwin)
        platform=macOS
        build_option=-buildOSXUniversalPlayer
        player_relative_path=Builds/macOS/UnitySample.app
        ;;
    *)
        fail 'Supported systems: Windows with Git Bash, Linux and macOS.'
        ;;
esac

unity_executable="${UNITY_EXECUTABLE:-}"
if [[ -z "$unity_executable" ]]; then
    if command -v Unity >/dev/null 2>&1; then
        unity_executable="$(command -v Unity)"
    elif command -v Unity.exe >/dev/null 2>&1; then
        unity_executable="$(command -v Unity.exe)"
    else
        fail 'Unity was not found. Set UNITY_EXECUTABLE to the Unity editor executable or add it to PATH.'
    fi
fi

if [[ "$platform" == Windows ]]; then
    command -v cygpath >/dev/null 2>&1 || fail 'Run this script using Git Bash on Windows.'
    unity_executable="$(cygpath -u "$unity_executable")"
fi
[[ -x "$unity_executable" ]] || fail "Unity is not executable: $unity_executable"

# Build the recorder and synchronize its outputs into the Unity plugin directory.
plugins_directory="$project_directory/Assets/UnitySample/Plugins"
unity_editor_directory="$(cd -- "$(dirname -- "$unity_executable")" && pwd -P)"
unity_managed_path="$unity_editor_directory/Data/Managed/UnityEngine"
[[ -s "$unity_managed_path/UnityEngine.CoreModule.dll" ]] || fail "Unity managed references are missing: $unity_managed_path"
build_properties=(-p:UnityManagedPath="$unity_managed_path")
if [[ "$platform" == Windows ]]; then
    vswhere='/c/Program Files (x86)/Microsoft Visual Studio/Installer/vswhere.exe'
    [[ -x "$vswhere" ]] || fail 'Visual Studio Installer vswhere.exe was not found.'
    native_msbuild="$("$vswhere" -latest -products '*' -requires Microsoft.Component.MSBuild Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -find 'MSBuild\**\Bin\MSBuild.exe' | tr -d '\r' | head -n 1)"
    [[ -n "$native_msbuild" ]] || fail 'Visual Studio C++ build tools were not found.'
    build_properties+=(-p:NativeMSBuildPath="$native_msbuild")
else
    printf '%s\n' 'Note: the current video encoder is Windows/NVIDIA only; this build can preview the scene and use the image-sequence capture API.'
fi
dotnet build "$recorder_directory/UnityRuntimeCameraRecorder.csproj" -c Release "${build_properties[@]}" || fail 'Recorder dependency build failed.'
recorder_output="$recorder_directory/bin/Release/netstandard2.1"
mkdir -p -- "$plugins_directory/x86_64"
cp -- "$recorder_output/UnityRuntimeCameraRecorder.dll" "$recorder_output/FFmpegMediaWriter.dll" "$plugins_directory/"
if [[ "$platform" == Windows ]]; then
    cp -- "$recorder_output/Direct3DVideoEncoder.dll" "$plugins_directory/x86_64/"
fi

player_path="$project_directory/$player_relative_path"
logs_directory="$project_directory/Builds/Logs"
log_path="$logs_directory/build.log"
build_directory="$project_directory/Builds/$platform"
[[ "$(dirname -- "$player_path")" == "$build_directory" ]] || fail 'The player path is outside its expected build directory.'
mkdir -p -- "$build_directory" "$logs_directory"
if [[ "$clean_build" == true ]]; then
    printf 'Cleaning previous %s player artifacts.\n' "$platform"
    find "$build_directory" -mindepth 1 -maxdepth 1 ! -name output -exec rm -rf -- {} +
    printf '%s\n' 'Cleaning the Unity import cache for a full rebuild.'
    rm -rf -- "$project_directory/Library"
fi
unity_project_path="$project_directory"
unity_player_path="$player_path"
unity_log_path="$log_path"

# Pass explicit Windows paths and prevent MSYS from rewriting Unity arguments.
if [[ "$platform" == Windows ]]; then
    unity_project_path="$(cygpath -m "$project_directory")"
    unity_player_path="$(cygpath -m "$player_path")"
    unity_log_path="$(cygpath -m "$log_path")"
    export MSYS2_ARG_CONV_EXCL='*'
fi

printf 'Building UnitySample for %s. Save your scene and close the editor first.\n' "$platform"
unity_arguments=(-batchmode -quit -projectPath "$unity_project_path")
if [[ "$clean_build" == true ]]; then
    unity_arguments+=(-cleanBuildCache)
fi
unity_arguments+=("$build_option" "$unity_player_path" -logFile "$unity_log_path")
if "$unity_executable" "${unity_arguments[@]}"; then
    if [[ "$platform" == macOS ]]; then
        [[ -d "$player_path" && -x "$player_path/Contents/MacOS/UnitySample" ]] || fail "Unity returned success but the player is missing. See $log_path."
    else
        [[ -s "$player_path" && -x "$player_path" ]] || fail "Unity returned success but the player is missing. See $log_path."
    fi
else
    fail "Unity build failed. See $log_path."
fi

printf 'Build completed: %s\n' "$player_path"
