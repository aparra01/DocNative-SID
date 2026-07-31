# Reprocesa PDFs movidos a ERROR por fallo transitorio de PagareSplit.
# Uso:
#   .\reprocesar-pagaresplit-error.ps1 -SalidaRoot "C:\SID\COOPROGRESO\PAGAREOCR\SALIDA" -EntradaRoot "C:\SID\COOPROGRESO\PAGAREOCR\ENTRADA"
#   .\reprocesar-pagaresplit-error.ps1 -SalidaRoot "..." -EntradaRoot "..." -Fecha "20260731" -WhatIf

param(
    [Parameter(Mandatory = $true)]
    [string]$SalidaRoot,

    [Parameter(Mandatory = $true)]
    [string]$EntradaRoot,

    [string]$Fecha = (Get-Date -Format "yyyyMMdd"),

    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"

$tiposRecuperables = @(
    "No se pudo validar el orden del PDF (PagareSplit no disponible)",
    "PagareSplit no disponible (reintentos agotados)",
    "PagareSplit error interno (reintentos agotados)"
)

$errorDir = Join-Path $SalidaRoot "00 - ERROR"
$csvPath = Join-Path $errorDir "${Fecha}_error.csv"
$pdfDir = Join-Path $errorDir $Fecha

if (-not (Test-Path $csvPath)) {
    Write-Error "No se encontró CSV: $csvPath"
}

if (-not (Test-Path $pdfDir)) {
    Write-Error "No se encontró carpeta de PDFs: $pdfDir"
}

$rows = Import-Csv -Path $csvPath -Encoding UTF8
$matched = @($rows | Where-Object {
    $desc = $_.'Descripción Error'
    if (-not $desc) { $desc = $_.'Tipo Error' }
    $tiposRecuperables -contains $desc
})

if ($matched.Count -eq 0) {
    Write-Host "No hay filas recuperables en $csvPath"
    exit 0
}

Write-Host "Filas a reprocesar: $($matched.Count)"

foreach ($row in $matched) {
    $agencia = $row.Agencia
    $pdfName = $row.'Nombre PDF'
    if (-not $agencia -or -not $pdfName) {
        Write-Warning "Fila incompleta, omitiendo: $($row | ConvertTo-Json -Compress)"
        continue
    }

    $source = Join-Path $pdfDir $pdfName
    if (-not (Test-Path $source)) {
        Write-Warning "PDF no encontrado, omitiendo: $source"
        continue
    }

    $destDir = Join-Path $EntradaRoot $agencia
    $dest = Join-Path $destDir $pdfName

    if ($WhatIf) {
        Write-Host "[WhatIf] Mover: $source -> $dest"
        continue
    }

    New-Item -ItemType Directory -Force -Path $destDir | Out-Null
    if (Test-Path $dest) {
        $stamp = Get-Date -Format "HHmmss"
        $base = [System.IO.Path]::GetFileNameWithoutExtension($pdfName)
        $ext = [System.IO.Path]::GetExtension($pdfName)
        $dest = Join-Path $destDir "${base}_reproc_${stamp}${ext}"
    }

    Move-Item -Path $source -Destination $dest
    Write-Host "Reprocesando: $dest"
}

Write-Host "Listo. DocNative reclamará los PDFs desde ENTRADA."
