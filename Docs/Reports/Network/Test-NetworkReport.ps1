param([string]$Root = 'Temp/NetworkReportSelfTest')
$ErrorActionPreference = 'Stop'
$path = Join-Path ([IO.Path]::GetFullPath($Root)) ([Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $path | Out-Null
$script = Join-Path $PSScriptRoot 'Summarize-Network.ps1'
foreach ($repeat in 1..3) {
    $rows = foreach ($second in 1..60) {
        [pscustomobject]@{
            run_id='synthetic'; repeat=$repeat; scenario='synthetic fixture, not measurements'; role='client'; platform='WindowsEditor'
            protocol=9; commit='test'; build_guid='test'; players=4; prediction='on'; configured_rtt_ms=0
            configured_loss_percent=0; mirror_send_rate=30; input_send_rate=30; snapshot_send_rate=30
            interval_s=1; elapsed_s=$second; warmup_s=10; capture_s=60; mirror_bytes_in_s=1000; mirror_bytes_out_s=500
            rtt_ms=$second; udp_packets_in_s='N/A'; udp_packets_out_s='N/A'
        }
    }
    $file = Join-Path $path "synthetic_repeat$repeat.csv"
    $rows | Export-Csv -LiteralPath $file -NoTypeInformation -Encoding UTF8
    @(10,20,30) | ForEach-Object {
        [pscustomobject]@{run_id='synthetic'; repeat=$repeat; network_time=1; input_to_view_ms=$_}
    } | Export-Csv -LiteralPath (Join-Path $path "synthetic_repeat${repeat}_input.csv") -NoTypeInformation -Encoding UTF8
}
$output = Join-Path $path 'summary.md'
& $script -InputDirectory $path -OutputFile $output
$text = Get-Content -Raw -LiteralPath $output
foreach ($expected in @('complete: 3','| rtt_ms | 30.5 | 57 | 180 |','| input_to_view_ms | 20 | 30 | 9 |','| udp_packets_in_s | N/A | N/A | 0 |')) {
    if (-not $text.Contains($expected)) { throw "Report self-test failed: $expected" }
}
Copy-Item -LiteralPath (Join-Path $path 'synthetic_repeat1.csv') -Destination (Join-Path $path 'duplicate.csv')
$rejected = $false
try { & $script -InputDirectory $path -OutputFile $output } catch { $rejected = $true }
if (-not $rejected) { throw 'Duplicate recording was not rejected.' }
Write-Output 'PASS: real CSV parsing, complete repeats, nearest-rank p95, input events, unavailable metrics and duplicate rejection.'
