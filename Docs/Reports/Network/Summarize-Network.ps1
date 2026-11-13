param(
    [string]$InputDirectory = 'Docs/Reports/Network/raw',
    [string]$OutputFile = 'Docs/Reports/Network/summary.md'
)
$ErrorActionPreference = 'Stop'
$culture = [Globalization.CultureInfo]::InvariantCulture
function Number($value) {
    $parsed = 0.0
    if ([double]::TryParse([string]$value, [Globalization.NumberStyles]::Float, $culture, [ref]$parsed) -and
        -not [double]::IsNaN($parsed) -and -not [double]::IsInfinity($parsed)) { return $parsed }
    return $null
}
function Stats($values) {
    $sorted = @($values | Where-Object { $null -ne $_ } | Sort-Object)
    if ($sorted.Count -eq 0) { return @{ Mean = 'N/A'; P95 = 'N/A'; Count = 0 } }
    $mean = ($sorted | Measure-Object -Average).Average
    $p95 = $sorted[[Math]::Max(0, [int][Math]::Ceiling(0.95 * $sorted.Count) - 1)]
    return @{ Mean = $mean.ToString('0.###', $culture); P95 = $p95.ToString('0.###', $culture); Count = $sorted.Count }
}
function Cell($value) { return ([string]$value).Replace('|', '\|').Replace("`r", ' ').Replace("`n", ' ') }
$keys = @('scenario','role','platform','prediction','configured_rtt_ms','configured_loss_percent',
    'mirror_send_rate','input_send_rate','snapshot_send_rate','players','protocol','commit','build_guid')
function Key($row) { return ConvertTo-Json -Compress -InputObject @($keys | ForEach-Object { [string]$row.$_ }) }
$rows = [Collections.Generic.List[object]]::new()
$seen = [Collections.Generic.HashSet[string]]::new()
$events = @{}
$files = @()
if (Test-Path -LiteralPath $InputDirectory) {
    $files = @(Get-ChildItem -LiteralPath $InputDirectory -Filter '*.csv' -File | Where-Object { $_.Name -notlike '*_input.csv' })
}
foreach ($file in $files) {
    $data = @(Import-Csv -LiteralPath $file.FullName -Encoding UTF8)
    if ($data.Count -eq 0) { continue }
    foreach ($required in @('run_id','repeat','scenario','role','interval_s','elapsed_s','warmup_s','capture_s','mirror_bytes_in_s','mirror_bytes_out_s')) {
        if ($data[0].PSObject.Properties.Name -notcontains $required) { throw "Missing column $required in $($file.Name)" }
    }
    $identity = "$($data[0].run_id)/$($data[0].repeat)/$($data[0].role)"
    if (-not $seen.Add($identity)) { throw "Duplicate recording $identity" }
    $key = Key $data[0]
    foreach ($row in $data) {
        if ((Key $row) -cne $key -or "$($row.run_id)/$($row.repeat)/$($row.role)" -ne $identity) {
            throw "Configuration or recording identity changed within $($file.Name). Repeat with constant settings."
        }
        if ((Number $row.interval_s) -le 0) { throw "Invalid interval in $($file.Name)" }
        $row | Add-Member -NotePropertyName Recording -NotePropertyValue $identity
        $rows.Add($row)
    }
    $inputFile = Join-Path $file.DirectoryName ($file.BaseName + '_input.csv')
    $events[$identity] = @()
    if (Test-Path -LiteralPath $inputFile) {
        foreach ($event in @(Import-Csv -LiteralPath $inputFile -Encoding UTF8)) {
            if ($event.run_id -ne $data[0].run_id -or $event.repeat -ne $data[0].repeat) { throw "Input-event identity mismatch in $inputFile" }
            $value = Number $event.input_to_view_ms
            if ($null -ne $value -and $value -ge 0) { $events[$identity] += $value }
        }
    }
}
$report = [Collections.Generic.List[string]]::new()
$report.Add('# Network measurements')
$report.Add('')
$report.Add('Generated from recorded CSV only. N/A is missing data, never zero. p95 uses nearest rank. Rate means are weighted by actual interval duration; p95 uses interval samples. Input latency uses all events from companion *_input.csv (CPU LateUpdate proxy, not display hardware latency).')
$report.Add('')
$report.Add('Mirror bytes exclude transport headers. Transport callbacks count payload batches, not UDP datagrams/retransmissions. UDP packets and observed loss remain N/A without another measurement source. Host Mirror totals include local loopback; do not divide them by player count to claim per-client traffic.')
$report.Add('')
if ($rows.Count -eq 0) {
    $report.Add('**PENDING: no measurement files. Stage 2 and NFR-03 are not accepted.**')
}
$metrics = @('rtt_ms','mirror_bytes_in_s','mirror_bytes_out_s','messages_in_s','messages_out_s',
    'transport_payload_in_s','transport_payload_out_s','transport_batches_in_s','transport_batches_out_s',
    'udp_packets_in_s','udp_packets_out_s','observed_loss_percent','observed_tick_hz',
    'prediction_tick_hz','input_packets_hz','snapshots_hz','error_units','corrections_s')
$overview = [Collections.Generic.List[string]]::new()
$overview.Add('## Comparison')
$overview.Add('')
$overview.Add('| Group | Scenario / role | Players | Prediction | Mirror/input/snap Hz | RTT configured / mean ms | Loss configured % | Complete repeats | Input mean / p95 ms | Error p95 | Mirror total mean / p95 B/s |')
$overview.Add('|---|---|---:|---|---|---|---:|---:|---|---:|---|')
$groupIndex = 0
foreach ($group in @($rows | Group-Object -Property { Key $_ })) {
    $groupIndex++
    $first = $group.Group[0]
    $runs = @($group.Group | Group-Object Recording)
    $complete = 0
    foreach ($run in $runs) {
        $duration = ($run.Group | ForEach-Object { Number $_.interval_s } | Measure-Object -Sum).Sum
        $minimumWarmup = ($run.Group | ForEach-Object { Number $_.warmup_s } | Measure-Object -Minimum).Minimum
        $capture = Number $run.Group[0].capture_s
        if ($minimumWarmup -ge 10 -and $capture -ge 60 -and $duration -ge ($capture - 0.5)) { $complete++ }
    }
    $quality = if ($complete -ge 3) { 'COMPLETE RECORDINGS (manual gameplay acceptance still required)' } else { 'PENDING: need 3 complete repeats, warmup >=10 s and capture >=60 s each' }
    $report.Add("## Group $groupIndex")
    $report.Add('')
    $report.Add("Scenario: $(Cell $first.scenario); role: $(Cell $first.role); platform: $(Cell $first.platform); players: $($first.players); prediction: $($first.prediction).")
    $report.Add("Configured RTT/loss: $($first.configured_rtt_ms) ms / $($first.configured_loss_percent)%; Mirror/input/snapshot rates: $($first.mirror_send_rate)/$($first.input_send_rate)/$($first.snapshot_send_rate) Hz.")
    $report.Add("Protocol: $($first.protocol); HEAD commit: $(Cell $first.commit); build: $(Cell $first.build_guid). HEAD does not certify a clean working tree.")
    $report.Add("Repeats: $($runs.Count); complete: $complete. **$quality**")
    $report.Add('')
    $report.Add('| Metric | Mean | p95 | Samples |')
    $report.Add('|---|---:|---:|---:|')
    foreach ($metric in $metrics) {
        $values = @($group.Group | ForEach-Object { Number $_.$metric })
        $stats = Stats $values
        if ($metric -match '_s$|_hz$') {
            $weight = 0.0; $sum = 0.0
            foreach ($row in $group.Group) {
                $value = Number $row.$metric
                if ($null -ne $value) { $dt = Number $row.interval_s; $sum += $value * $dt; $weight += $dt }
            }
            if ($weight -gt 0) { $stats.Mean = ($sum / $weight).ToString('0.###', $culture) }
        }
        $report.Add("| $metric | $($stats.Mean) | $($stats.P95) | $($stats.Count) |")
    }
    $latencies = @($runs | ForEach-Object { $events[$_.Name] })
    $latencyStats = Stats $latencies
    $report.Add("| input_to_view_ms | $($latencyStats.Mean) | $($latencyStats.P95) | $($latencyStats.Count) |")
    $totals = @($group.Group | ForEach-Object { (Number $_.mirror_bytes_in_s) + (Number $_.mirror_bytes_out_s) })
    $trafficStats = Stats $totals
    $totalWeight = 0.0; $weightedTotal = 0.0
    foreach ($row in $group.Group) {
        $incoming = Number $row.mirror_bytes_in_s
        $outgoing = Number $row.mirror_bytes_out_s
        if ($null -ne $incoming -and $null -ne $outgoing) {
            $dt = Number $row.interval_s; $totalWeight += $dt; $weightedTotal += ($incoming + $outgoing) * $dt
        }
    }
    if ($totalWeight -gt 0) { $trafficStats.Mean = ($weightedTotal / $totalWeight).ToString('0.###', $culture) }
    $report.Add("| mirror_total_bytes_s | $($trafficStats.Mean) | $($trafficStats.P95) | $($trafficStats.Count) |")
    $report.Add('')
    $budget = if ($first.role -ne 'client') { 'N/A: use a separate client capture.' }
        elseif ($first.players -lt 4 -or $complete -lt 3) { 'PENDING: insufficient MVP load or complete repeats.' }
        elseif ([double]::Parse($trafficStats.P95, $culture) -ge 20000) { 'OVER budget on Mirror messages (p95 >= 20000 B/s).' }
        else { 'Mirror message budget below 20000 B/s; transport overhead/retransmission budget and manual acceptance remain unverified.' }
    $errorStats = Stats @($group.Group | ForEach-Object { Number $_.error_units })
    $rttStats = Stats @($group.Group | ForEach-Object { Number $_.rtt_ms })
    $overview.Add("| G$groupIndex | $(Cell $first.scenario) / $($first.role) | $($first.players) | $($first.prediction) | $($first.mirror_send_rate)/$($first.input_send_rate)/$($first.snapshot_send_rate) | $($first.configured_rtt_ms) / $($rttStats.Mean) | $($first.configured_loss_percent) | $complete | $($latencyStats.Mean) / $($latencyStats.P95) | $($errorStats.P95) | $($trafficStats.Mean) / $($trafficStats.P95) |")
    $report.Add("NFR-03: $budget")
    $report.Add('')
}
if ($rows.Count -gt 0) { $overview.Add(''); $report.InsertRange(6, $overview) }
$parent = Split-Path -Parent ([IO.Path]::GetFullPath($OutputFile))
[IO.Directory]::CreateDirectory($parent) | Out-Null
[IO.File]::WriteAllLines([IO.Path]::GetFullPath($OutputFile), $report, [Text.UTF8Encoding]::new($false))
Write-Output "Report: $OutputFile; recordings: $($seen.Count); interval rows: $($rows.Count)"
