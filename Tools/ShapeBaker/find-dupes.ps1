param(
    [string]$Path = "shapes.bake"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $Path)) {
    Write-Error "bake file not found: $Path"
    exit 1
}

$fs = [System.IO.File]::OpenRead((Resolve-Path -LiteralPath $Path))
try {
    $br = New-Object System.IO.BinaryReader($fs, [System.Text.Encoding]::ASCII)

    $magic   = $br.ReadUInt32()
    if ($magic -ne 0x53424d45) { throw "bad magic: 0x{0:X8}" -f $magic }
    $version = $br.ReadUInt32()
    if ($version -ne 1)        { throw "unsupported version $version" }
    $res     = [int]$br.ReadUInt32()
    $count   = [int]$br.ReadUInt32()
    $minX = $br.ReadSingle(); $minY = $br.ReadSingle(); $minZ = $br.ReadSingle()
    $maxX = $br.ReadSingle(); $maxY = $br.ReadSingle(); $maxZ = $br.ReadSingle()

    $totalBits  = $res * $res * $res
    $byteCount  = [int][math]::Ceiling($totalBits / 8.0)

    Write-Output ("=== bake header ===")
    Write-Output ("path        : " + (Resolve-Path -LiteralPath $Path))
    Write-Output ("resolution  : $res  ($totalBits voxels, $byteCount bytes/stamp)")
    Write-Output ("stamp count : $count")
    Write-Output ("frame min   : ({0:F3}, {1:F3}, {2:F3})" -f $minX,$minY,$minZ)
    Write-Output ("frame max   : ({0:F3}, {1:F3}, {2:F3})" -f $maxX,$maxY,$maxZ)
    Write-Output ""

    # Read stamps into name + hex-key for grouping.
    $groups = @{}
    $names  = New-Object System.Collections.Generic.List[string]
    $fills  = @{}
    for ($i = 0; $i -lt $count; $i++) {
        $nameLen = $br.ReadUInt16()
        $nameBytes = $br.ReadBytes($nameLen)
        $name = [System.Text.Encoding]::ASCII.GetString($nameBytes)
        $packed = $br.ReadBytes($byteCount)

        $key = [System.BitConverter]::ToString($packed)
        if (-not $groups.ContainsKey($key)) {
            $groups[$key] = New-Object System.Collections.Generic.List[string]
        }
        $groups[$key].Add($name) | Out-Null
        $names.Add($name) | Out-Null

        # Pop-count for fill diagnostic.
        $fill = 0
        foreach ($b in $packed) {
            $v = [int]$b
            $v = $v - (($v -shr 1) -band 0x55)
            $v = ($v -band 0x33) + (($v -shr 2) -band 0x33)
            $v = ($v + ($v -shr 4)) -band 0x0F
            $fill += $v
        }
        $fills[$name] = $fill
    }

    $dupeGroups = @($groups.Values | Where-Object { $_.Count -gt 1 })
    $totalDupes = ($dupeGroups | ForEach-Object { $_.Count } | Measure-Object -Sum).Sum
    $redundant  = ($dupeGroups | ForEach-Object { $_.Count - 1 } | Measure-Object -Sum).Sum

    Write-Output ("=== duplicate summary ===")
    Write-Output ("unique occupancy patterns : {0}" -f $groups.Count)
    Write-Output ("stamps in duplicate groups: {0}" -f $totalDupes)
    Write-Output ("redundant stamps          : {0}  (i.e. could collapse to {1} unique)" -f $redundant, $groups.Count)
    Write-Output ("duplicate group count     : {0}" -f $dupeGroups.Count)

    # Special call-out for all-zero (empty) stamps.
    $emptyKey = ("00-" * $byteCount).TrimEnd("-")
    if ($groups.ContainsKey($emptyKey)) {
        $emptyMembers = $groups[$emptyKey]
        Write-Output ("empty stamps (fill=0)     : {0}" -f $emptyMembers.Count)
    }
    Write-Output ""

    Write-Output ("=== duplicate groups (size desc) ===")
    $sorted = $dupeGroups | Sort-Object { -$_.Count }
    $idx = 0
    foreach ($g in $sorted) {
        $idx++
        $sampleName = $g[0]
        $fill = $fills[$sampleName]
        $pct  = 100.0 * $fill / $totalBits
        Write-Output ("[{0,3}] size={1,3}  fill={2,3}/{3} ({4,5:F1}%)" -f $idx, $g.Count, $fill, $totalBits, $pct)
        foreach ($n in ($g | Sort-Object)) {
            Write-Output ("       $n")
        }
    }
}
finally {
    $fs.Dispose()
}
