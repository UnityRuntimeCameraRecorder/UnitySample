#!/usr/bin/env bash
# Record both cameras for six seconds and measure FPS after the first second.
set -euo pipefail

# Report a configuration error before recording or purging any output files.
fail() {
    printf 'ERROR: %s\n' "$*" >&2
    exit 1
}

# Find the newest sample statistics file without relying on platform-specific find options.
latest_stats() {
    local latest='' report
    for report in "$output_directory"/UnitySample_*.stats.json; do
        if [[ -f "$report" ]] && { [[ -z "$latest" ]] || [[ "$report" -nt "$latest" ]]; }; then
            latest="$report"
        fi
    done
    printf '%s' "$latest"
}

if [[ $# -gt 0 ]]; then
    if [[ $# -eq 1 && ( "$1" == --help || "$1" == -h ) ]]; then
        printf '%s\n' \
            'Usage: bash run.sh' \
            'Requires a Windows/NVIDIA build and FFMPEG_PATH pointing to FFmpeg.' \
            'Records camera1 + camera2 for 6 seconds and measures FPS from second 1 to 6.' \
            'Purges only sample-prefixed output files before recording.'
        exit 0
    fi
    fail 'Unexpected arguments. Use --help.'
fi

project_directory="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
case "$(uname -s)" in
    MINGW*|MSYS*|CYGWIN*)
        player_path="$project_directory/Builds/Windows/UnitySample.exe"
        command -v cygpath >/dev/null 2>&1 || fail 'Run this script using Git Bash on Windows.'
        [[ -n "${FFMPEG_PATH:-}" ]] || fail 'Set FFMPEG_PATH to the full path of the FFmpeg executable.'
        ffmpeg_file="$(cygpath -u "$FFMPEG_PATH")"
        [[ -f "$ffmpeg_file" && -x "$ffmpeg_file" ]] || fail 'FFMPEG_PATH does not point to an executable file.'
        export FFMPEG_PATH="$(cygpath -m "$ffmpeg_file")"
        export MSYS2_ARG_CONV_EXCL='*'
        ;;
    Linux|Darwin)
        fail 'Video recording currently requires Windows/NVIDIA. Linux/macOS need a compatible video backend.'
        ;;
    *)
        fail 'Unsupported operating system.'
        ;;
esac

[[ -s "$player_path" && -x "$player_path" ]] || fail 'UnitySample is missing. Run bash build.sh first.'
output_directory="$(dirname -- "$player_path")/output"
previous_report="$(latest_stats)"

printf '%s\n' 'Recording camera 1 and camera 2 for 6 seconds; FPS measurement uses seconds 1 through 6.'
printf '%s\n' 'Sample-prefixed output files will be purged before recording.'
result=0
"$player_path" --purge --render 4k --resolution 4k --fps 60 --codec h264 \
    --vsync on --aa 4 --quality high --record camera1,camera2 --duration 6 \
    --quit-after-recording || result=$?

report="$(latest_stats)"
if [[ -n "$report" && "$report" != "$previous_report" ]]; then
    printf '\nRecording session statistics\n'
    cat -- "$report"
    printf '\nReport: %s\n' "$report"
    printf '%s\n' 'videoActualFps uses only the five-second window from second 1 to second 6.'
else
    printf '%s\n' 'WARNING: no new session statistics were produced.' >&2
fi

if [[ "$result" -ne 0 ]]; then
    printf 'ERROR: UnitySample exited with code %s.\n' "$result" >&2
    exit "$result"
fi
printf 'Session finished. Videos and statistics: %s\n' "$output_directory"
