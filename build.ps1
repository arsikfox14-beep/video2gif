param(
    [switch]$Force,
    [string]$OutName = ""
)

$ErrorActionPreference = "Stop"
$src = $PSScriptRoot
$parent = Split-Path -Parent $src
if (Test-Path (Join-Path $parent "src")) {
    $root = $parent
    $src = Join-Path $parent "src"
} else {
    $root = $src
}
$build = Join-Path $root "build"
$out = Join-Path $root $(if ($OutName) { $OutName } else { "Video2Gif.exe" })
$ffmpeg = Join-Path $root "bin\ffmpeg.exe"
$gz = Join-Path $build "ffmpeg.gz"
$ico = Join-Path $src "app.ico"
$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if (-not (Test-Path $csc)) {
    $csc = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe"
}
if (-not (Test-Path $csc)) { throw "csc.exe (.NET Framework 4.x) не найден." }
if (-not (Test-Path $build)) { New-Item -ItemType Directory -Path $build | Out-Null }

# --- FFmpeg -> gzip resource -------------------------------------------------
if ($Force -or -not (Test-Path $gz) -or ((Get-Item $ffmpeg).LastWriteTime -gt (Get-Item $gz).LastWriteTime)) {
    if (-not (Test-Path $ffmpeg)) { throw "Не найден $ffmpeg" }
    Write-Host "Сжимаю FFmpeg..."
    $inStream = [IO.File]::OpenRead($ffmpeg)
    $outStream = [IO.File]::Create($gz)
    try {
        $gzip = New-Object IO.Compression.GZipStream($outStream, [IO.Compression.CompressionMode]::Compress)
        try { $inStream.CopyTo($gzip) } finally { $gzip.Close() }
    } finally {
        $outStream.Close()
        $inStream.Close()
    }
    Write-Host ("  ffmpeg.gz: {0:N1} МБ" -f ((Get-Item $gz).Length / 1MB))
}

# --- yt-dlp -> gzip resource -------------------------------------------------
$ytdlp = Join-Path $root "bin\yt-dlp.exe"
$ytgz = Join-Path $build "yt-dlp.gz"
if (-not (Test-Path $ytdlp)) { throw "Не найден $ytdlp. Положите yt-dlp.exe в папку bin." }
if ($Force -or -not (Test-Path $ytgz) -or ((Get-Item $ytdlp).LastWriteTime -gt (Get-Item $ytgz).LastWriteTime)) {
    Write-Host "Сжимаю yt-dlp..."
    $inStream = [IO.File]::OpenRead($ytdlp)
    $outStream = [IO.File]::Create($ytgz)
    try {
        $gzip = New-Object IO.Compression.GZipStream($outStream, [IO.Compression.CompressionMode]::Compress)
        try { $inStream.CopyTo($gzip) } finally { $gzip.Close() }
    } finally {
        $outStream.Close()
        $inStream.Close()
    }
    Write-Host ("  yt-dlp.gz: {0:N1} МБ" -f ((Get-Item $ytgz).Length / 1MB))
}

# --- Icon --------------------------------------------------------------------
function New-RoundedPath([System.Drawing.RectangleF]$rect, [float]$radius) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $radius * 2
    $path.AddArc($rect.X, $rect.Y, $d, $d, 180, 90)
    $path.AddArc($rect.Right - $d, $rect.Y, $d, $d, 270, 90)
    $path.AddArc($rect.Right - $d, $rect.Bottom - $d, $d, $d, 0, 90)
    $path.AddArc($rect.X, $rect.Bottom - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    return $path
}

if ($Force -or -not (Test-Path $ico)) {
    Add-Type -AssemblyName System.Drawing
    $size = 256
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    # Чёрно-белая иконка: белая окантовка, чёрный квадрат, белый треугольник.
    $outer = New-Object System.Drawing.RectangleF(4, 4, 248, 248)
    $outerPath = New-RoundedPath $outer 60
    $g.FillPath([System.Drawing.Brushes]::White, $outerPath)

    $rect = New-Object System.Drawing.RectangleF(16, 16, 224, 224)
    $path = New-RoundedPath $rect 50
    $g.FillPath([System.Drawing.Brushes]::Black, $path)

    $points = @(
        (New-Object System.Drawing.PointF(100, 74)),
        (New-Object System.Drawing.PointF(100, 182)),
        (New-Object System.Drawing.PointF(188, 128))
    )
    $g.FillPolygon([System.Drawing.Brushes]::White, $points)
    $g.Dispose()

    $ms = New-Object IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $png = $ms.ToArray()
    $ms.Dispose()

    $fs = [IO.File]::Create($ico)
    $bw = New-Object IO.BinaryWriter($fs)
    try {
        $bw.Write([UInt16]0)      # reserved
        $bw.Write([UInt16]1)      # icon
        $bw.Write([UInt16]1)      # count
        $bw.Write([Byte]0)        # width 256
        $bw.Write([Byte]0)        # height 256
        $bw.Write([Byte]0)        # palette
        $bw.Write([Byte]0)        # reserved
        $bw.Write([UInt16]1)      # planes
        $bw.Write([UInt16]32)     # bpp
        $bw.Write([UInt32]$png.Length)
        $bw.Write([UInt32]22)     # offset
        $bw.Write($png)
    } finally {
        $bw.Close()
    }
    Write-Host "Иконка: src\app.ico"
}

# --- Compile -----------------------------------------------------------------
Write-Host "Компилирую..."
$sources = Get-ChildItem (Join-Path $src "*.cs") | ForEach-Object { $_.FullName }
$cscArgs = @(
    "/nologo",
    "/target:winexe",
    "/platform:anycpu",
    "/optimize+",
    "/out:$out",
    "/win32icon:$ico",
    "/win32manifest:$(Join-Path $src 'app.manifest')",
    "/resource:$gz,ffmpeg.gz",
    "/resource:$ytgz,yt-dlp.gz",
    "/reference:System.dll",
    "/reference:System.Core.dll",
    "/reference:System.Drawing.dll",
    "/reference:System.Windows.Forms.dll",
    "/reference:System.Web.Extensions.dll"
)
$cscArgs += $sources

& $csc $cscArgs
if ($LASTEXITCODE -ne 0) { throw "Компиляция не удалась (код $LASTEXITCODE)." }

$info = Get-Item $out
Write-Host ("Готово: {0} ({1:N1} МБ)" -f $info.FullName, ($info.Length / 1MB))
