# Capstone — Audio Upload API

API para subir archivos de audio con **compresión automática a AAC** utilizando **Clean Architecture**, **MinIO** como file store S3-compatible y **PostgreSQL** como base de datos.

## Stack

| Capa           | Tecnología                                               |
| -------------- | -------------------------------------------------------- |
| Runtime        | .NET 10                                                  |
| Arquitectura   | Clean Architecture (4 capas)                             |
| API            | ASP.NET Core Web API (Controllers)                       |
| ORM            | Entity Framework Core + Npgsql                           |
| DB             | PostgreSQL                                               |
| File Store     | MinIO (Minio SDK)                                        |
| Compresión     | FFmpeg (proceso externo, AAC 128k)                       |
| Lenguaje       | C# 13                                                    |

## Estructura del proyecto

```
Capstone.sln
src/
├── Capstone.Domain/              => Entidades del negocio puras
├── Capstone.Application/         => Puertos (interfaces) + Casos de uso + DTOs
├── Capstone.Infrastructure/      => Implementaciones concretas (EF Core, MinIO, FFmpeg)
└── Capstone.Api/                 => Controladores, Middleware, DI, Program.cs
tests/
└── Capstone.Tests/               => Pruebas unitarias (xUnit + Moq)
```

### Capstone.Domain
Capa más interna. Contiene solo entidades del dominio sin dependencias externas.
- `Entities/AudioFile.cs` — Entidad con Id, FileName, ContentType, Size, StorageUrl, UploadedAt, más campos de compresión (OriginalFileName, OriginalContentType, OriginalSize, CompressedAt, CompressionDurationMs)

### Capstone.Application
Capa de casos de uso. Define **puertos** (interfaces) que la infraestructura implementa.
- `Interfaces/` — Contratos (`IFileStorageService`, `IAudioFileRepository`, `IAudioCompressionService`)
- `UseCases/` — Orquestación de la lógica de negocio (`UploadAudioUseCase`)
- `DTOs/` — Objetos de transferencia de datos

### Capstone.Infrastructure
Capa que implementa los puertos definidos en Application.
- `Persistence/` — EF Core DbContext + Repositories + Migrations
- `FileStorage/` — Servicio concreto para MinIO
- `Compression/` — Servicio de compresión con FFmpeg

### Capstone.Api
Capa de presentación. Depende de Application e Infrastructure.
- `Controllers/` — Endpoints REST
- `Program.cs` — Wiring de dependencias y configuración

## Flujo de subida de audio

```
Cliente → POST /api/audio/upload
              → AudioController (valida formato y tamaño)
                  → UploadAudioUseCase
                      → IAudioCompressionService.CompressAsync()
                          → ffmpeg -i pipe:0 -c:a aac -b:a 128k pipe:1
                          ← CompressionResult (stream AAC comprimido)
                      → IFileStorageService.UploadAsync(stream comprimido) → MinIO
                      → IAudioFileRepository.AddAsync(metadatos + stats compresión) → PostgreSQL
              ← 201 Created (UUID + URL + CompressedSize + WasCompressed)
```

El audio original se comprime a **AAC @ 128k** antes de almacenarse. Si el archivo ya es AAC/M4A, se omite la compresión (passthrough). El tiempo de compresión se loggea automáticamente.

## Requisitos

### Local
- .NET 10 SDK
- PostgreSQL (local o Docker)
- MinIO (local o Docker)
- **FFmpeg** instalado y disponible en PATH

### Docker (recomendado)
- Docker Compose (incluye API + PostgreSQL + MinIO, FFmpeg ya instalado en el container)

## Ejecutar

### Con Docker (recomendado)

```bash
docker compose up -d
```

La API queda en `http://localhost:5069/swagger`. FFmpeg ya viene instalado dentro del container.

### Local

```bash
dotnet run --project src/Capstone.Api
```

Requiere FFmpeg instalado en el sistema.

## Configuración

### Variables de entorno / .env

Todas las credenciales se configuran sin tocar código. Copiar `.env.example` a `.env`:

```env
HOST_IP=192.168.1.100

# MinIO — file storage
MinIO__Endpoint=${HOST_IP}:9000
MinIO__AccessKey=minioadmin
MinIO__SecretKey=minioadmin
MinIO__BucketName=audio
MinIO__UseSsl=false

# PostgreSQL — database
ConnectionStrings__DefaultConnection=Host=${HOST_IP};Database=capstone_audio;Username=postgres;Password=postgres

# FFmpeg — compresión de audio (opcional, usa defaults si se omite)
FFmpeg__FfmpegPath=ffmpeg
FFmpeg__BitRate=128000
FFmpeg__CompressionTimeoutMs=120000
```

### O con User Secrets (alternativa local)

```bash
dotnet user-secrets init --project src/Capstone.Api
dotnet user-secrets set "MinIO:Endpoint" "192.168.1.100:9000"
dotnet user-secrets set "MinIO:AccessKey" "minioadmin"
dotnet user-secrets set "MinIO:SecretKey" "minioadmin"
dotnet user-secrets set "MinIO:BucketName" "audio"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=192.168.1.100;Database=capstone_audio;Username=postgres;Password=tu-pass"
```

## Endpoints

| Método | Ruta              | Descripción                    |
| ------ | ----------------- | ------------------------------ |
| POST   | `/api/audio/upload` | Subir archivo de audio (se comprime a AAC automáticamente) |

### POST /api/audio/upload

- **Content-Type:** `multipart/form-data`
- **Body:** campo `file` con el archivo de audio
- **Formatos aceptados:** MP3, WAV, OGG, MP4, AAC, FLAC
- **Límite:** 50 MB

**Respuesta 201:**
```json
{
  "id": "uuid",
  "fileName": "cancion.aac",
  "storageUrl": "http://minio:9000/audio/uuid_cancion.aac",
  "uploadedAt": "2026-07-16T14:30:00Z",
  "compressedSize": 1153433,
  "wasCompressed": true
}
```

- `fileName` — Nombre del archivo almacenado (con extensión .aac si se comprimió)
- `compressedSize` — Tamaño final del archivo comprimido en bytes
- `wasCompressed` — Indica si el audio fue comprimido (false si ya era AAC/M4A)
- **Respuesta 400:** Formato no soportado o archivo vacío
- **Respuesta 500:** Error de compresión (FFmpeg no disponible, timeout, etc.)

## Tests

```bash
dotnet test tests/Capstone.Tests
```

Pruebas unitarias con xUnit + Moq que cubren:
- Passthrough para archivos AAC y MP4
- Valores por defecto de CompressionResult
- Manejo de error cuando FFmpeg no está instalado
