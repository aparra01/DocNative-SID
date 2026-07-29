# DocNative-SID

Servicio nativo .NET 8 de **pre-procesamiento de PDFs** para el flujo PagareOCR de sucursales. Corre **antes** de PyVision: limpia hojas en blanco, corrige orientación a portrait y entrega en `ENTRADA/<codigo>/LISTO/`.

## Proyectos

| Proyecto | Tipo | Descripción |
|----------|------|-------------|
| `DocNative.Core` | Librería | Render PDF, detección de blanco, rotación, reescritura, errores, CSV |
| `DocNative.Sucursales` | Worker Service | Hotfolder `ENTRADA/<codigo>/` |
| `DocNative.Core.Tests` | xUnit | Tests unitarios |

## Flujo de datos

```
MFP / UNC  →  ENTRADA/<codigo>/  →  DocNative  →  ENTRADA/<codigo>/LISTO/  →  PyVision  →  SALIDA/<codigo>/PROCESADOS/
                     │                    │                                              │
                     │                    └── work temp (%LOCALAPPDATA%/DocNative/work)     └── SALIDA/ERROR/ + CSV diario
```

**Carpetas visibles para el operador:**

| Carpeta | Uso |
|---------|-----|
| `ENTRADA/<codigo>/` | Escaneo MFP (intake) |
| `ENTRADA/<codigo>/LISTO/` | Handoff post DocNative (PyVision solo procesa aquí) |
| `SALIDA/<codigo>/PROCESADOS/` | Resultado OCR |
| `SALIDA/ERROR/` | Errores centralizados (compartido con PyVision) |

El trabajo interno de DocNative usa `%LOCALAPPDATA%/DocNative/work/<codigo>/` (no requiere montaje SMB).

## Requisitos

- .NET 8 SDK (desarrollo)
- Windows Server / Windows 10+ con **contenedores Windows** (LTSC 2022) para Docker
- Imagen base runtime: `mcr.microsoft.com/dotnet/runtime:8.0-servercore-ltsc2022` (**no** nanoserver — OpenCvSharp requiere DLLs del sistema)

## Configuración

`appsettings.json` o variables de entorno (`DocNative__*`):

| Clave | Default contenedor | Descripción |
|-------|-------------------|-------------|
| `OutputRoot` | `C:/mnt/PagareOcrEntrada` | ENTRADA (subcarpetas = código sucursal) |
| `WorkRoot` | *(vacío → `%LOCALAPPDATA%/DocNative/work`)* | Claim temporal interno |
| `SalidaRoot` | `C:/mnt/PagareOcrSalida` | Raíz SALIDA de PyVision |
| `ErrorRoot` | *(vacío → `{SalidaRoot}/ERROR`)* | Errores centralizados + CSV |
| `BlankPageThreshold` | `0.02` | Umbral stddev normalizado (0–1) para hoja en blanco |
| `RenderDpi` | `150` | DPI render Docnet |
| `FileStabilityMaxWaitSeconds` | `20` | Espera archivo estable antes de procesar |

### Variables Docker (host)

```env
PYVISION_HOST_PAGAREOCR_ENTRADA=D:/SID/COOPROGRESO/PAGAREOCR/ENTRADA
PYVISION_HOST_PAGAREOCR_SALIDA=D:/SID/COOPROGRESO/PAGAREOCR/SALIDA
```

### Migración BD (PyVision)

```bash
cd PyVision-SID
python scripts/migrar_pagare_ocr_listo.py --ruta-base C:/SID/COOPROGRESO/PAGAREOCR
```

## Desarrollo local

```bash
cd DocNative-SID
dotnet restore
dotnet build
dotnet test

# Worker en consola (ajustar appsettings.Development.json)
dotnet run --project src/DocNative.Sucursales
```

## Docker (Windows containers)

Desde la raíz del monorepo SID:

```powershell
docker compose --env-file .env.windows -f docker-compose.windows.yml build docnative
docker compose --env-file .env.windows -f docker-compose.windows.yml up -d docnative
```

Compose standalone:

```powershell
docker compose -f DocNative-SID/docker/docker-compose.docnative.yml --env-file docker.env.windows up -d --build
```

## Windows Service (host sin Docker)

```powershell
dotnet publish src/DocNative.Sucursales -c Release -o C:\Services\DocNative
sc.exe create DocNative.Sucursales binPath= "C:\Services\DocNative\DocNative.Sucursales.exe"
sc.exe start DocNative.Sucursales
```

El worker registra `AddWindowsService` y puede instalarse como servicio nativo de Windows.

## Errores y CSV diario

Estructura (compartida con PyVision):

```
SALIDA/ERROR/
  29_07_2026/
    documento_fallido.pdf
    errores_29_07_2026.csv
```

CSV (append inmediato al registrar cada error):

| # | Fecha | Hora | Agencia | Nombre PDF | Tipo Error |
|---|-------|------|---------|------------|------------|

- **Un solo CSV por día:** `errores_DD_MM_YYYY.csv` dentro de `SALIDA/ERROR/DD_MM_YYYY/`. DocNative y PyVision escriben en el mismo archivo; no se crean CSV por agencia, hora ni tipo de error.
- **Tipo Error:** descripción legible del motivo (ej. `PDF corrupto`, `Documento sin contenido util`).
- Hora en formato **24h** (`HH:mm:ss`).

## Montaje SMB (producción)

Monte el UNC del servidor impresión directamente en **`ENTRADA/<codigo>/`**. PyVision vigila la misma raíz ENTRADA pero solo procesa PDFs en `LISTO/`.

## Stack técnico

- **Docnet.Core** — render PDF → imagen (PDFium)
- **OpenCvSharp4** — detección blanco y orientación
- **PdfSharpCore** — reescritura PDF (eliminar páginas, rotación)
- **Serilog** — logging estructurado

## Opción B (precisión)

Si OpenCV no alcanza precisión en documentos reales del banco, `IBlankPageDetector` e `IRotationCorrector` están abstraídos para adapters de **LEADTOOLS** o **Dynamsoft Document Normalizer** (`DocNative:DetectionEngine=OpenCv|LeadTools|Dynamsoft` — no implementado en v1).

## Troubleshooting

| Síntoma | Causa probable | Acción |
|---------|----------------|--------|
| Contenedor no arranca | Imagen nanoserver | Usar `servercore-ltsc2022` |
| `DllNotFoundException` OpenCV | Falta VC++ redist | Dockerfile instala VC++ 2015–2022 x64 |
| PyVision lee PDF sin procesar | PDF aún en ENTRADA (no en LISTO) o DocNative detenido | Verificar DocNative; PDF debe pasar a `LISTO/` |
| CSV vacío | Sin errores ese día | Normal; no hay PDFs fallidos en la carpeta del día |

## Integración SID

- Servicio `docnative` en `docker-compose.windows.yml` y compose offline.
- PyVision depende de `docnative` y monta `PAGAREOCR/ENTRADA` y `PAGAREOCR/SALIDA`.
- Empaquetado offline: `npm run sid:pack-images:windows` incluye imagen `sid/docnative`.
