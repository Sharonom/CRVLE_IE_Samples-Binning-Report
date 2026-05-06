<#
.SYNOPSIS
    Queries the Vortex database for VPO bin split data.

.PARAMETER VpoNumbers
    One or more VPO numbers, separated by commas, spaces, or newlines.

.PARAMETER ShowVisualIds
    Also fetch and display per-unit Visual ID detail.

.PARAMETER ExportCsv
    Optional file path to export the bin split pivot table as CSV.
#>
param(
    [Parameter(Mandatory)]
    [string]$VpoNumbers,
    [switch]$ShowVisualIds,
    [string]$ExportCsv = ""
)

$ErrorActionPreference = "Stop"

# ── 1. Parse VPO numbers ──────────────────────────────────────────────────────
$vpos = ($VpoNumbers -split '[,\r\n\s]+') |
    Where-Object { $_.Trim().Length -gt 0 -and $_.Trim().Length -le 100 } |
    ForEach-Object { $_.Trim() } |
    Select-Object -Unique |
    Select-Object -First 1000

if ($vpos.Count -eq 0) {
    Write-Error "No valid VPO numbers provided."
    exit 1
}

# ── 2. Read connection string from dotnet user-secrets ────────────────────────
$projectPath = Join-Path $PSScriptRoot "..\src\SamplesBucketing.Web"
$secretLines = & dotnet user-secrets list --project $projectPath 2>&1

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to read user secrets. Ensure the .NET SDK is installed and secrets are configured."
    exit 1
}

$csLine = $secretLines |
    Where-Object { $_ -match 'ConnectionStrings:VortexConnection\s*=' } |
    Select-Object -First 1

if (-not $csLine) {
    Write-Error "VortexConnection not found in user secrets."
    exit 1
}

$connectionString = ($csLine -replace '^ConnectionStrings:VortexConnection\s*=\s*', '').Trim()

# ── 3. ISO work-week string ───────────────────────────────────────────────────
function Get-WwString($dt) {
    if ($null -eq $dt -or $dt -is [System.DBNull]) { return "" }
    $d = [datetime]$dt
    $dow = [int][System.DayOfWeek]$d.DayOfWeek; if ($dow -eq 0) { $dow = 7 }
    $thu = $d.AddDays(4 - $dow)
    $ww  = [System.Globalization.CultureInfo]::InvariantCulture.Calendar.GetWeekOfYear(
               $thu,
               [System.Globalization.CalendarWeekRule]::FirstFourDayWeek,
               [System.DayOfWeek]::Monday)
    return "$($thu.Year)$($ww.ToString('D2'))"
}

# ── 4. Helper: DataRow column value, returns $null for DBNull ─────────────────
function Get-Val($row, $col) {
    $v = $row.($col)
    if ($v -is [System.DBNull]) { return $null }
    return $v
}

# ── 5. Helper: run a parameterised IN-list query ──────────────────────────────
function Invoke-InQuery($Conn, $Sql, $VpoList) {
    $cmd = $Conn.CreateCommand()
    $placeholders = for ($i = 0; $i -lt $VpoList.Count; $i++) {
        $cmd.Parameters.AddWithValue("@p$i", $VpoList[$i]) | Out-Null
        "@p$i"
    }
    $cmd.CommandText = $Sql -replace '\{PARAMS\}', ($placeholders -join ', ')
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter $cmd
    $dt = New-Object System.Data.DataTable
    $adapter.Fill($dt) | Out-Null
    return $dt
}

# ── 6. SQL ────────────────────────────────────────────────────────────────────
$binSql = @"
SELECT
    v.vpo_number                              AS VpoNumber,
    ISNULL(t.processing_site_id, '')          AS Site,
    ISNULL(t.operation_code,     '')          AS Operation,
    ISNULL(t.task_name,          '')          AS Flow,
    t.complete_date                           AS TestEndDate,
    ISNULL(t.step_in_quantity,   0)           AS Tested,
    ISNULL(t.step_out_quantity,  0)           AS Good,
    b.bin_name                                AS BinName,
    COALESCE(COUNT(mr.material_result_id), 0) AS Quantity
FROM  vortex_dbo.vw_vpo v
INNER JOIN vortex_dbo.vw_public_task t
       ON t.task_id = v.current_task_id
INNER JOIN vortex_dbo.vw_public_result_bin b
       ON b.task_id = v.current_task_id
LEFT  JOIN vortex_dbo.vw_public_material_result mr
       ON mr.result_id = b.result_id
WHERE v.vpo_number IN ({PARAMS})
GROUP BY v.vpo_number,
         t.processing_site_id, t.operation_code, t.task_name,
         t.complete_date, t.step_in_quantity, t.step_out_quantity,
         b.bin_name
ORDER BY v.vpo_number, b.bin_name;
"@

$vidSql = @"
SELECT v.vpo_number     AS VpoNumber,
       b.bin_name       AS BinName,
       mr.material_name AS VisualId
FROM  vortex_dbo.vw_vpo v
INNER JOIN vortex_dbo.vw_public_task t
       ON t.task_id = v.current_task_id
INNER JOIN vortex_dbo.vw_public_result_bin b
       ON b.task_id = v.current_task_id
INNER JOIN vortex_dbo.vw_public_material_result mr
       ON mr.result_id = b.result_id
WHERE v.vpo_number IN ({PARAMS})
ORDER BY v.vpo_number, b.bin_name, mr.material_name;
"@

# ── 7. Execute ────────────────────────────────────────────────────────────────
$conn = New-Object System.Data.SqlClient.SqlConnection($connectionString)
try {
    $conn.Open()

    Write-Host "Querying bin splits for: $($vpos -join ', ') ..." -ForegroundColor Cyan
    $binTable = Invoke-InQuery $conn $binSql $vpos

    if ($binTable.Rows.Count -eq 0) {
        Write-Host "No bin split results found." -ForegroundColor Yellow
        exit 0
    }

    # Convert DataTable rows to plain PSCustomObjects immediately
    $rawRows = $binTable | ForEach-Object {
        [PSCustomObject]@{
            VpoNumber   = if ($_.VpoNumber   -is [System.DBNull]) { "" } else { [string]$_.VpoNumber }
            Site        = if ($_.Site        -is [System.DBNull]) { "" } else { [string]$_.Site }
            Operation   = if ($_.Operation   -is [System.DBNull]) { "" } else { [string]$_.Operation }
            Flow        = if ($_.Flow        -is [System.DBNull]) { "" } else { [string]$_.Flow }
            TestEndDate = if ($_.TestEndDate -is [System.DBNull]) { $null } else { [datetime]$_.TestEndDate }
            Tested      = if ($_.Tested      -is [System.DBNull]) { 0 }  else { [int]$_.Tested }
            Good        = if ($_.Good        -is [System.DBNull]) { 0 }  else { [int]$_.Good }
            BinName     = if ($_.BinName     -is [System.DBNull]) { "(unknown)" } else { [string]$_.BinName }
            Quantity    = if ($_.Quantity    -is [System.DBNull]) { 0 }  else { [int]$_.Quantity }
        }
    }

    # ── Build pivot ───────────────────────────────────────────────────────────
    $meta    = [ordered]@{}   # VpoNumber -> meta hashtable
    $binData = [ordered]@{}   # VpoNumber -> { BinName -> Quantity }
    $allBinsSet = [System.Collections.Generic.SortedSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

    foreach ($row in $rawRows) {
        if (-not $row.VpoNumber) { continue }
        $allBinsSet.Add($row.BinName) | Out-Null

        if (-not $meta.Contains($row.VpoNumber)) {
            $meta[$row.VpoNumber] = @{
                Site        = $row.Site
                Operation   = $row.Operation
                Flow        = $row.Flow
                TestEndDate = $row.TestEndDate
                Tested      = $row.Tested
                Good        = $row.Good
            }
            $binData[$row.VpoNumber] = @{}
        }
        ($binData[$row.VpoNumber])[$row.BinName] = $row.Quantity
    }

    $binList = @($allBinsSet)

    # ── Build output objects ──────────────────────────────────────────────────
    $outputRows = foreach ($vpo in $meta.Keys) {
        $m   = $meta[$vpo]
        $bcs = $binData[$vpo]
        $obj = [ordered]@{
            SITE          = $m.Site
            LOT           = $vpo
            OPERATION     = $m.Operation
            FLOW          = $m.Flow
            TEST_END_DATE = if ($m.TestEndDate) { $m.TestEndDate.ToString('dd-MMM-yyyy HH:mm').ToUpperInvariant() } else { '' }
            WW_END_TEST   = Get-WwString $m.TestEndDate
            '#TESTED'     = $m.Tested
            T_GOOD        = $m.Good
        }
        foreach ($b in $binList) {
            $obj[$b] = if ($bcs.ContainsKey($b)) { $bcs[$b] } else { 0 }
        }
        [PSCustomObject]$obj
    }

    Write-Host "`n=== BIN SPLIT PIVOT ===" -ForegroundColor Green
    $outputRows | Format-Table -AutoSize

    if ($ExportCsv) {
        $outputRows | Export-Csv -Path $ExportCsv -NoTypeInformation
        Write-Host "Exported to: $ExportCsv" -ForegroundColor Cyan
    }

    # ── Summary ───────────────────────────────────────────────────────────────
    $totalTested = ($outputRows | Measure-Object -Property '#TESTED' -Sum).Sum
    $totalGood   = ($outputRows | Measure-Object -Property 'T_GOOD'  -Sum).Sum
    Write-Host "Total tested: $totalTested  |  Total good: $totalGood" -ForegroundColor Cyan

    # ── Visual ID detail ──────────────────────────────────────────────────────
    if ($ShowVisualIds) {
        Write-Host "`nFetching Visual IDs..." -ForegroundColor Cyan
        $vidTable = Invoke-InQuery $conn $vidSql $vpos

        if ($vidTable.Rows.Count -gt 0) {
            Write-Host "`n=== VISUAL ID DETAIL ($($vidTable.Rows.Count) units) ===" -ForegroundColor Green
            $vidTable | Select-Object VpoNumber, BinName, VisualId | Format-Table -AutoSize
        } else {
            Write-Host "No Visual ID records found." -ForegroundColor Yellow
        }
    }
}
finally {
    $conn.Dispose()
}