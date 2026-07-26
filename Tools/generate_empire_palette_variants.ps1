param(
    [string]$Source = "Resources/Textures/_Crescent/Clothing/Empire",
    [string]$Output = "art-review/empire-equipment-variants"
)

Add-Type -AssemblyName System.Drawing

$sourceRoot = (Resolve-Path $Source).Path
$outputRoot = Join-Path (Get-Location) $Output
$variants = @{
    "clarizian-navy" = 222.0
    "srm-green-blue" = 174.0
}

function Convert-HsvToRgb([double]$h, [double]$s, [double]$v) {
    $c = $v * $s
    $x = $c * (1.0 - [Math]::Abs((($h / 60.0) % 2.0) - 1.0))
    $m = $v - $c
    $r = 0.0; $g = 0.0; $b = 0.0
    if ($h -lt 60) { $r = $c; $g = $x }
    elseif ($h -lt 120) { $r = $x; $g = $c }
    elseif ($h -lt 180) { $g = $c; $b = $x }
    elseif ($h -lt 240) { $g = $x; $b = $c }
    elseif ($h -lt 300) { $r = $x; $b = $c }
    else { $r = $c; $b = $x }
    return @(
        [Math]::Round(($r + $m) * 255),
        [Math]::Round(($g + $m) * 255),
        [Math]::Round(($b + $m) * 255)
    )
}

function Convert-Color([System.Drawing.Color]$color, [double]$targetHue) {
    if ($color.A -eq 0) { return $color }
    $max = [Math]::Max($color.R, [Math]::Max($color.G, $color.B)) / 255.0
    $min = [Math]::Min($color.R, [Math]::Min($color.G, $color.B)) / 255.0
    $delta = $max - $min
    $saturation = if ($max -eq 0) { 0 } else { $delta / $max }
    $hue = $color.GetHue()

    # Preserve neutral outlines/metal, white highlights, and intentional gold/brass trim.
    $isGold = $hue -ge 35 -and $hue -le 68 -and $saturation -ge 0.24
    if ($saturation -lt 0.12 -or $isGold) { return $color }

    # Keep the pixel-art shading, while giving the faction color enough identity.
    $newSaturation = [Math]::Min(0.78, [Math]::Max(0.28, $saturation * 0.92))
    $newValue = if ($targetHue -eq 222.0) {
        [Math]::Min(0.72, $max * 0.82)
    } else {
        [Math]::Min(0.68, $max * 0.84)
    }
    $rgb = Convert-HsvToRgb $targetHue $newSaturation $newValue
    return [System.Drawing.Color]::FromArgb($color.A, $rgb[0], $rgb[1], $rgb[2])
}

foreach ($variant in $variants.GetEnumerator()) {
    $variantRoot = Join-Path $outputRoot $variant.Key
    Get-ChildItem $sourceRoot -Recurse -File | ForEach-Object {
        $relative = $_.FullName.Substring($sourceRoot.Length).TrimStart('\', '/')
        $destination = Join-Path $variantRoot $relative
        $destinationDir = Split-Path $destination -Parent
        New-Item -ItemType Directory -Path $destinationDir -Force | Out-Null

        if ($_.Extension -ne ".png") {
            Copy-Item -LiteralPath $_.FullName -Destination $destination -Force
            return
        }

        $sourceImage = [System.Drawing.Bitmap]::new($_.FullName)
        $result = [System.Drawing.Bitmap]::new(
            $sourceImage.Width,
            $sourceImage.Height,
            [System.Drawing.Imaging.PixelFormat]::Format32bppArgb
        )
        for ($y = 0; $y -lt $sourceImage.Height; $y++) {
            for ($x = 0; $x -lt $sourceImage.Width; $x++) {
                $result.SetPixel($x, $y, (Convert-Color $sourceImage.GetPixel($x, $y) $variant.Value))
            }
        }
        $result.Save($destination, [System.Drawing.Imaging.ImageFormat]::Png)
        $result.Dispose()
        $sourceImage.Dispose()
    }
}

# One enlarged preview per item, suitable for approving files one by one.
$previewRoot = Join-Path $outputRoot "previews"
foreach ($variant in $variants.Keys) {
    $variantRoot = Join-Path $outputRoot $variant
    Get-ChildItem $variantRoot -Recurse -File -Filter "icon.png" | ForEach-Object {
        $relativeRsi = $_.Directory.FullName.Substring($variantRoot.Length).TrimStart('\', '/')
        $safeName = ($relativeRsi -replace '[\\/]', '__') -replace '\.rsi$', ''
        $previewDir = Join-Path $previewRoot $variant
        New-Item -ItemType Directory -Path $previewDir -Force | Out-Null
        $previewPath = Join-Path $previewDir ($safeName + ".png")
        $icon = [System.Drawing.Bitmap]::new($_.FullName)
        $preview = [System.Drawing.Bitmap]::new(256, 256)
        $graphics = [System.Drawing.Graphics]::FromImage($preview)
        $graphics.Clear([System.Drawing.Color]::FromArgb(32, 35, 42))
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
        $scale = [Math]::Min(7, [Math]::Floor(224 / [Math]::Max($icon.Width, $icon.Height)))
        $w = $icon.Width * $scale
        $h = $icon.Height * $scale
        $graphics.DrawImage($icon, [Math]::Floor((256 - $w) / 2), [Math]::Floor((256 - $h) / 2), $w, $h)
        $preview.Save($previewPath, [System.Drawing.Imaging.ImageFormat]::Png)
        $graphics.Dispose()
        $preview.Dispose()
        $icon.Dispose()
    }
}

$readme = @"
Empire / Imperial Equipment Palette Variants

- clarizian-navy: navy blue palette, hue 222°
- srm-green-blue: greenish-blue palette, hue 174°
- previews: enlarged 256x256 item icons for one-by-one review

The original RSI layout, metadata, animation frames, transparency, silhouettes,
neutral metal/outlines, and gold/brass trim are preserved. Only chromatic faction
colors are remapped. Source assets are not modified.
"@
Set-Content -LiteralPath (Join-Path $outputRoot "README.txt") -Value $readme -Encoding utf8

Write-Output "Generated variants at: $outputRoot"
