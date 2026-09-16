#!/usr/bin/env bash
# Record camera 1 and the application screen, then display this session's statistics.
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
            'Usage: bash record.sh' \
            'Requires a Windows/NVIDIA build and FFMPEG_PATH pointing to FFmpeg.' \
            'Records camera1 + screen for 10 seconds: 4K input/output, 60 FPS max, HEVC, P5, VSync, MSAA 4x.' \
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

printf '%s\n' 'Recording camera 1 and screen for 10 seconds: 4K input/output, 60 FPS max, HEVC, P5, VSync, MSAA 4x.'
printf '%s\n' 'Sample-prefixed output files will be purged before recording.'
result=0
"$player_path" --purge --render 4k --resolution 4k --fps 60 --codec hevc \
    --vsync on --aa 4 --preset p5 --record camera1,screen --duration 10 \
    --quit-after-recording || result=$?

report="$(latest_stats)"
if [[ -n "$report" && "$report" != "$previous_report" ]]; then
    printf '\nRecording session statistics\n'
    cat -- "$report"
    printf '\nReport: %s\n' "$report"
    printf '%s\n' 'Frame counts above measure Unity rendering, not encoded video frames.'
else
    printf '%s\n' 'WARNING: no new session statistics were produced.' >&2
fi

if [[ "$result" -ne 0 ]]; then
    printf 'ERROR: UnitySample exited with code %s.\n' "$result" >&2
    exit "$result"
fi
printf 'Session finished. Videos and statistics: %s\n' "$output_directory"
