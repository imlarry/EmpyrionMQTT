param([string]$Path = "shapes.bake")

$fs = [System.IO.File]::OpenRead((Resolve-Path -LiteralPath $Path))
try {
    $br = New-Object System.IO.BinaryReader($fs, [System.Text.Encoding]::ASCII)
    $magic   = $br.ReadUInt32()
    $version = $br.ReadUInt32()
    $res     = [int]$br.ReadUInt32()
    $count   = [int]$br.ReadUInt32()
    $null = $br.ReadBytes(24)  # frame min+max floats
    $byteCount = [int][math]::Ceiling(($res*$res*$res) / 8.0)
    for ($i = 0; $i -lt $count; $i++) {
        $nameLen   = $br.ReadUInt16()
        $nameBytes = $br.ReadBytes($nameLen)
        $null      = $br.ReadBytes($byteCount)
        Write-Output ([System.Text.Encoding]::ASCII.GetString($nameBytes))
    }
}
finally { $fs.Dispose() }
