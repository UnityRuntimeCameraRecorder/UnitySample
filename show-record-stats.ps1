param(
    [Parameter(Mandatory = $true)]
    [string] $OutputDirectory
)

# Display the latest sample session report without changing any output files.
$ErrorActionPreference = 'Stop'
try {
    $report = Get-ChildItem -LiteralPath $OutputDirectory -Filter 'UnitySample_*.stats.json' -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
    if ($null -eq $report) {
        Write-Warning 'No session statistics were produced.'
        exit 0
    }
    $stats = Get-Content -LiteralPath $report.FullName -Raw | ConvertFrom-Json
    Write-Host ''
    Write-Host 'Recording session statistics'
    Write-Host ('Status: {0}' -f $stats.status)
    Write-Host ('GPU: {0}' -f $stats.gpu)
    Write-Host ('Render: {0} x {1} | Output: {2} x {3} | Max FPS: {4}' -f
        $stats.renderWidth, $stats.renderHeight, $stats.outputWidth, $stats.outputHeight, $stats.maximumVideoFps)
    Write-Host ('Preset: P{0} | MSAA: {1}x | VSync count: {2}' -f
        $stats.encodingPreset, $stats.msaaSamples, $stats.vSyncCount)
    Write-Host ('Capture: {0:N2} s | Finalization: {1:N2} s' -f
        $stats.captureDurationSeconds, $stats.finalizationDurationSeconds)
    Write-Host ('Camera 1 render: {0:N2} FPS, {1} frames | selected: {2}' -f
        $stats.camera1AverageRenderFps, $stats.camera1RenderedFrames, $stats.camera1)
    Write-Host ('Camera 2 render: {0:N2} FPS, {1} frames | selected: {2}' -f
        $stats.camera2AverageRenderFps, $stats.camera2RenderedFrames, $stats.camera2)
    Write-Host ('Screen selected: {0}' -f $stats.screen)
    for ($index = 0; $index -lt $stats.videoFiles.Count; $index++) {
        Write-Host ('Video: {0} ({1:N1} MB)' -f
            $stats.videoFiles[$index], ($stats.videoFileBytes[$index] / 1000000))
    }
    if ($stats.error) { Write-Host ('Error: {0}' -f $stats.error) }
    Write-Host ('Report: {0}' -f $report.FullName)
    Write-Host 'Frame counts above measure Unity rendering, not encoded video frames.'
} catch {
    Write-Warning ('Cannot display session statistics: {0}' -f $_.Exception.Message)
    exit 1
}
