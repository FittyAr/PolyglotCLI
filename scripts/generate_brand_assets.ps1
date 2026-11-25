# scripts/generate_brand_assets.ps1
# Genera los recursos visuales empaquetados (MSIX / MAUI / Inno Setup) a
# partir del arte de marca maestro que vive en assets/brand/.
#
#   assets/brand/LogoSimple.png          (1024x1024, fondo transparente) -> marca "P"
#   assets/brand/SplashScreen-master.png (2480x1200)                     -> splash MSIX
#
# Salidas:
#   assets/msix/Assets/*                      logos del paquete MSIX (con calificadores MRT
#                                             scale-XXX / targetsize-XX / altform-unplated)
#   assets/brand/installer/*                  imagenes del asistente de Inno Setup
#   PolyglotCLI.Maui/Resources/AppIcon/appicon.png
#   PolyglotCLI.Maui/Resources/Splash/splash.png
#
# Uso (idempotente, regenera todo):
#   pwsh scripts/generate_brand_assets.ps1
#
# Escalado: System.Drawing con interpolacion HighQualityBicubic y canal
# alfa preservado. No se generan variantes que requeririan ampliar la
# fuente mas de 1.25x (MRT usa la escala mas cercana disponible).

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$brandDir = Join-Path $repoRoot "assets/brand"
$msixDir = Join-Path $repoRoot "assets/msix/Assets"
$installerDir = Join-Path $brandDir "installer"
$mauiIconDir = Join-Path $repoRoot "PolyglotCLI.Maui/Resources/AppIcon"
$mauiSplashDir = Join-Path $repoRoot "PolyglotCLI.Maui/Resources/Splash"

foreach ($d in @($msixDir, $installerDir, $mauiIconDir, $mauiSplashDir)) {
    New-Item -ItemType Directory -Path $d -Force | Out-Null
}

$markSource = Join-Path $brandDir "LogoSimple.png"
$splashSource = Join-Path $brandDir "SplashScreen-master.png"
foreach ($f in @($markSource, $splashSource)) {
    if (-not (Test-Path $f)) { throw "No se encontro el arte maestro: $f" }
}

# --- Utilidades ------------------------------------------------------------

function Get-AlphaBounds {
    param([System.Drawing.Bitmap]$Bitmap, [int]$Threshold = 16)
    $minX = $Bitmap.Width; $minY = $Bitmap.Height; $maxX = -1; $maxY = -1
    $rect = New-Object System.Drawing.Rectangle 0, 0, $Bitmap.Width, $Bitmap.Height
    $data = $Bitmap.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $stride = $data.Stride
        $bytes = New-Object byte[] ($stride * $Bitmap.Height)
        [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
    } finally {
        $Bitmap.UnlockBits($data)
    }
    for ($y = 0; $y -lt $Bitmap.Height; $y++) {
        $row = $y * $stride
        for ($x = 0; $x -lt $Bitmap.Width; $x++) {
            if ($bytes[$row + $x * 4 + 3] -gt $Threshold) {
                if ($x -lt $minX) { $minX = $x }
                if ($x -gt $maxX) { $maxX = $x }
                if ($y -lt $minY) { $minY = $y }
                if ($y -gt $maxY) { $maxY = $y }
            }
        }
    }
    if ($maxX -lt 0) { throw "La imagen no tiene pixeles visibles." }
    return New-Object System.Drawing.Rectangle $minX, $minY, ($maxX - $minX + 1), ($maxY - $minY + 1)
}

# Dibuja $SrcRect de $Source centrado en un lienzo WxH. La marca ocupa como
# maximo $Fill de la dimension menor. $Background = $null -> transparente.
function New-Asset {
    param(
        [System.Drawing.Image]$Source,
        [System.Drawing.Rectangle]$SrcRect,
        [int]$Width,
        [int]$Height,
        [double]$Fill,
        [string]$OutPath,
        [Nullable[System.Drawing.Color]]$Background = $null
    )
    $bmp = New-Object System.Drawing.Bitmap $Width, $Height, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    try {
        $g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
        $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        if ($null -ne $Background) { $g.Clear([System.Drawing.Color]$Background) } else { $g.Clear([System.Drawing.Color]::Transparent) }

        $box = [Math]::Min($Width, $Height) * $Fill
        $scale = [Math]::Min($box / $SrcRect.Width, $box / $SrcRect.Height)
        $dw = $SrcRect.Width * $scale
        $dh = $SrcRect.Height * $scale
        $dx = ($Width - $dw) / 2.0
        $dy = ($Height - $dh) / 2.0
        $dest = New-Object System.Drawing.RectangleF ([single]$dx), ([single]$dy), ([single]$dw), ([single]$dh)

        $attrs = New-Object System.Drawing.Imaging.ImageAttributes
        $attrs.SetWrapMode([System.Drawing.Drawing2D.WrapMode]::TileFlipXY)
        $points = [System.Drawing.PointF[]]@(
            (New-Object System.Drawing.PointF $dest.Left, $dest.Top),
            (New-Object System.Drawing.PointF $dest.Right, $dest.Top),
            (New-Object System.Drawing.PointF $dest.Left, $dest.Bottom))
        $g.DrawImage($Source, $points, [System.Drawing.RectangleF]$SrcRect, [System.Drawing.GraphicsUnit]::Pixel, $attrs)
        $attrs.Dispose()
    } finally {
        $g.Dispose()
    }
    $bmp.Save($OutPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host ("  {0,-55} {1}x{2}" -f (Split-Path $OutPath -Leaf), $Width, $Height)
}

# --- Marca (P) -------------------------------------------------------------

$mark = [System.Drawing.Bitmap]::FromFile($markSource)
$splash = [System.Drawing.Bitmap]::FromFile($splashSource)
try {
    $b = Get-AlphaBounds -Bitmap $mark
    # Recorte cuadrado centrado en la marca con un margen de 2%.
    $side = [int]([Math]::Max($b.Width, $b.Height) * 1.02)
    $cx = $b.X + $b.Width / 2.0; $cy = $b.Y + $b.Height / 2.0
    $markRect = New-Object System.Drawing.Rectangle ([int]($cx - $side / 2)), ([int]($cy - $side / 2)), $side, $side
    Write-Host "Marca detectada en LogoSimple.png: $b -> recorte $markRect" -ForegroundColor Cyan
    $maxUpscale = 1.25

    $scales = @(100, 125, 150, 200, 400)

    # Logos cuadrados/anchos del manifiesto. Fill = fraccion de la dimension
    # menor que ocupa la marca (margen recomendado por las guias de Windows).
    $logos = @(
        @{ Name = "StoreLogo";         W = 50;  H = 50;  Fill = 0.92 },
        @{ Name = "Square44x44Logo";   W = 44;  H = 44;  Fill = 0.84 },
        @{ Name = "Square71x71Logo";   W = 71;  H = 71;  Fill = 0.70 },
        @{ Name = "Square150x150Logo"; W = 150; H = 150; Fill = 0.66 },
        @{ Name = "Wide310x150Logo";   W = 310; H = 150; Fill = 0.66 },
        @{ Name = "Square310x310Logo"; W = 310; H = 310; Fill = 0.60 }
    )

    Write-Host "Generando logos MSIX en $msixDir" -ForegroundColor Yellow
    foreach ($l in $logos) {
        # Archivo sin calificador (fallback / referencia del manifiesto) = escala 100.
        New-Asset -Source $mark -SrcRect $markRect -Width $l.W -Height $l.H -Fill $l.Fill `
            -OutPath (Join-Path $msixDir "$($l.Name).png")
        foreach ($s in $scales) {
            $w = [int][Math]::Round($l.W * $s / 100.0)
            $h = [int][Math]::Round($l.H * $s / 100.0)
            $glyph = [Math]::Min($w, $h) * $l.Fill
            if ($glyph -gt $markRect.Width * $maxUpscale) { continue }
            New-Asset -Source $mark -SrcRect $markRect -Width $w -Height $h -Fill $l.Fill `
                -OutPath (Join-Path $msixDir "$($l.Name).scale-$s.png")
        }
    }

    # Square44x44Logo con targetsize (barra de tareas, Explorador, Alt+Tab...).
    # Se generan las variantes "plated" y "_altform-unplated" (sin placa,
    # usada por la barra de tareas); con fondo transparente son identicas.
    foreach ($t in @(16, 24, 32, 48, 256)) {
        $fill = if ($t -le 24) { 1.0 } else { 0.94 }
        New-Asset -Source $mark -SrcRect $markRect -Width $t -Height $t -Fill $fill `
            -OutPath (Join-Path $msixDir "Square44x44Logo.targetsize-$t.png")
        New-Asset -Source $mark -SrcRect $markRect -Width $t -Height $t -Fill $fill `
            -OutPath (Join-Path $msixDir "Square44x44Logo.targetsize-${t}_altform-unplated.png")
    }

    # Splash 620x300 (base) + escalas, desde el arte maestro 2480x1200.
    # Fill se aplica sobre la dimension menor: para cubrir todo el lienzo
    # (misma proporcion 620:300 que la fuente) usamos Fill = ancho/alto.
    $splashRect = New-Object System.Drawing.Rectangle 0, 0, $splash.Width, $splash.Height
    foreach ($s in $scales) {
        $w = [int][Math]::Round(620 * $s / 100.0); $h = [int][Math]::Round(300 * $s / 100.0)
        New-Asset -Source $splash -SrcRect $splashRect -Width $w -Height $h -Fill (620.0 / 300.0) `
            -OutPath (Join-Path $msixDir "SplashScreen.scale-$s.png")
    }
    New-Asset -Source $splash -SrcRect $splashRect -Width 620 -Height 300 -Fill (620.0 / 300.0) `
        -OutPath (Join-Path $msixDir "SplashScreen.png")

    # Recursos MAUI (icono de ventana/barra de tareas y splash).
    Write-Host "Generando recursos MAUI" -ForegroundColor Yellow
    New-Asset -Source $mark -SrcRect $markRect -Width 456 -Height 456 -Fill 0.94 `
        -OutPath (Join-Path $mauiIconDir "appicon.png")
    New-Asset -Source $mark -SrcRect $markRect -Width 456 -Height 456 -Fill 0.80 `
        -OutPath (Join-Path $mauiSplashDir "splash.png")

    # Imagenes del asistente Inno Setup (lista de tamanos: Inno elige la que
    # mejor encaja segun DPI/WizardSizePercent sin deformarla).
    Write-Host "Generando imagenes del instalador en $installerDir" -ForegroundColor Yellow
    $wizardBg = [System.Drawing.Color]::FromArgb(255, 0x0F, 0x17, 0x2A)
    foreach ($s in @(100, 125, 150, 200)) {
        $w = [int][Math]::Round(164 * $s / 100.0); $h = [int][Math]::Round(314 * $s / 100.0)
        New-Asset -Source $mark -SrcRect $markRect -Width $w -Height $h -Fill 0.80 -Background $wizardBg `
            -OutPath (Join-Path $installerDir "WizardImage-$s.png")
        $sw = [int][Math]::Round(55 * $s / 100.0)
        New-Asset -Source $mark -SrcRect $markRect -Width $sw -Height $sw -Fill 0.96 `
            -OutPath (Join-Path $installerDir "WizardSmallImage-$s.png")
    }
} finally {
    $mark.Dispose()
    $splash.Dispose()
}

Write-Host "Recursos de marca generados." -ForegroundColor Green
