# Capstone — Plataforma Distribuida de Procesamiento, Transcripción y Resumen de Audio con IA

Plataforma empresarial orientada a eventos para la **ingesta, validación binaria, compresión de alta fidelidad, transcripción automática con IA (Whisper) y generación de resúmenes semánticos con LLMs (Qwen2.5)**.

El sistema está diseñado bajo los principios de **Clean Architecture (Arquitectura Limpia)** y **Event-Driven Microservices (Arquitectura Orientada a Eventos)**, orquestado mediante **RabbitMQ**, con almacenamiento de objetos S3 compatible (**MinIO**), persistencia relacional (**PostgreSQL 16**) y una interfaz reactiva moderna (**React + TypeScript + Vite**) apoyada en **Web Workers** para concurrencia en cliente.

Los diagramas de arquitectura y flujo se encuentran definidos en código **PlantUML estándar**, disponibles directamente en este documento y como archivos fuente ejecutables en el directorio [`doc/diagrams/`](file:///c:/Users/PC/Documents/jala/progra7/capstone/doc/diagrams).

---

## Tabla de Contenidos

1. [Visión General y Propósito del Sistema](#visión-general-y-propósito-del-sistema)
2. [Stack Tecnológico](#stack-tecnológico)
3. [Arquitectura Global del Sistema](#arquitectura-global-del-sistema)
4. [Diagramas de Flujo del Pipeline de Datos](#diagramas-de-flujo-del-pipeline-de-datos)
   - [Flujo Secuencial Integral (End-to-End)](#flujo-secuencial-integral-end-to-end)
   - [Topología del Message Broker (RabbitMQ)](#topología-del-message-broker-rabbitmq)
   - [Máquina de Estados del Archivo de Audio](#máquina-de-estados-del-archivo-de-audio)
   - [Inspección de Seguridad y Magic Bytes](#inspección-de-seguridad-y-magic-bytes)
   - [Pipeline de Inteligencia Artificial (Whisper + Qwen LLM)](#pipeline-de-inteligencia-artificial-whisper--qwen-llm)
5. [Arquitectura de Software (Clean Architecture)](#arquitectura-de-software-clean-architecture)
6. [Modelo de Datos Relacional (ERD)](#modelo-de-datos-relacional-erd)
7. [Arquitectura del Frontend y Concurrencia](#arquitectura-del-frontend-y-concurrencia)
8. [Streaming y Comparativa de Audio Dual](#streaming-y-comparativa-de-audio-dual)
9. [Especificación de la API REST](#especificación-de-la-api-rest)
10. [Infraestructura y Despliegue con Docker](#infraestructura-y-despliegue-con-docker)
11. [Guía de Ejecución Local y con Contenedores](#guía-de-ejecución-local-y-con-contenedores)
12. [Estrategia de Pruebas y Benchmarking de Rendimiento](#estrategia-de-pruebas-y-benchmarking-de-rendimiento)

---

## Visión General y Propósito del Sistema

El objetivo de este proyecto es solucionar el procesamiento asíncrono y distribuido de medios de audio pesados, evitando cuellos de botella en el servidor web y garantizando:
- **Resiliencia y desacoplamiento**: Cada etapa de procesamiento (validación, compresión, transcripción e inferencia LLM) se ejecuta de forma independiente mediante workers autónomos.
- **Validación rigurosa**: Inspección de cabeceras binarias (*magic numbers*) para impedir la inyección de archivos maliciosos disfrazados de audio.
- **Optimización de ancho de banda y almacenamiento**: Transcodificación automática a **AAC @ 128 kbps**, reduciendo el tamaño original entre un 60% y un 85% sin pérdida perceptible de claridad vocal.
- **Enriquecimiento cognitivo con IA Local**: Transcripción fonética precisa vía **Whisper.net** y generación de resúmenes concisos ejecutando localmente el modelo de lenguaje **Qwen2.5-0.5B-Instruct**, sin enviar datos a APIs de terceros ni incurrir en costes recurrentes de nube.
- **Experiencia de usuario fluida**: El cliente Web delega la subida paralela y el sondeo continuo a un **Web Worker** en segundo plano, y ofrece un reproductor dual comparativo en tiempo real.

---

## Stack Tecnológico

| Capa / Dominio | Componente / Herramienta | Versión / Detalle | Propósito en el Sistema |
| :--- | :--- | :--- | :--- |
| **Backend Core** | .NET Runtime & C# | .NET 8 / C# 12 | Motor principal de API y servicios en background |
| **Patrón Arquitectónico** | Clean Architecture | 4 Capas (Domain, App, Infra, Api) | Desacoplamiento e inversión de dependencias |
| **Broker de Mensajería** | RabbitMQ | 4.0 Management Alpine | Bus de eventos asíncronos y colas topic distribuidas |
| **Base de Datos** | PostgreSQL | 16 Alpine + EF Core 8 + Npgsql | Almacén relacional de metadatos, estados y textos IA |
| **Object Storage** | MinIO | S3-Compatible API | Almacén persistente de audio original y comprimido |
| **Transcodificación** | FFmpeg | 6.x / 7.x CLI | Conversión y compresión a AAC y remuestreo a 16kHz WAV |
| **Motor Speech-to-Text** | Whisper.net | Modelo `ggml-tiny.bin` | Transcripción de voz a texto autónoma en CPU/GPU |
| **Motor LLM (Inferencia)** | LLamaSharp | `qwen2.5-0.5b-instruct-q8_0.gguf` | Resúmenes y síntesis del contenido del audio |
| **Frontend SPA** | React + TypeScript + Vite | React 18, TS 5, Vite 6 | Interfaz interactiva de alta fidelidad con tema claro/oscuro |
| **Concurrencia Web** | Web Worker API | `audioUpload.worker.ts` | Upload multi-hilo en navegador y polling desacoplado |
| **Reverse Proxy** | Nginx | Alpine | Gateway de producción frontend y proxy reverso hacia API |
| **Contenedores** | Docker & Docker Compose | Compose Spec v3.8 | Orquestación completa de los 8 servicios del sistema |
| **Pruebas de Carga** | k6 | Grafana k6 | Pruebas de estrés y límites de latencia bajo carga |

---

## Arquitectura Global del Sistema

Fuente PlantUML: [`doc/diagrams/01_arquitectura_global.puml`](file:///c:/Users/PC/Documents/jala/progra7/capstone/doc/diagrams/01_arquitectura_global.puml)

```plantuml
@startuml
skinparam componentStyle rectangle

package "Capa de Presentacion / Cliente" {
  [Web Client (React 18 + TS)] as UI
  [Web Worker (audioUpload.worker)] as WW
}

package "Gateway & Reverse Proxy" {
  [Nginx (Puerto 8080:80)] as NGINX
}

package "Capa API Principal (Clean Architecture)" {
  [Capstone.Api (.NET 8 - Puerto 5069:8080)] as API
  [AudioProcessedEventConsumer (HostedService)] as API_CONS
}

package "Message Broker (RabbitMQ 4)" {
  queue "Topic Exchange: capstone.audio" as EX
  queue "Cola: audio.validate" as Q_VAL
  queue "Cola: audio.compress" as Q_CMP
  queue "Cola: audio.transcribe" as Q_TX
  queue "Cola: audio.processed.api" as Q_PROC
}

package "Pipeline de Procesamiento Distribuido (Workers)" {
  [Capstone.Validator\n- Inspeccion Magic Bytes\n- Filtro anti-tampering] as W_VAL
  [Capstone.Compressor\n- FFmpeg AAC 128kbps\n- Optimizacion de tamano] as W_CMP
  [Capstone.Transcriber\n- Whisper ASR (tiny)\n- Qwen2.5 LLM Summarizer] as W_TX
}

database "PostgreSQL 16\n(AudioFiles, Transcriptions, Summaries)" as DB
storage "MinIO Object Storage\nBucket: audio\n(Originales + Filtrados AAC)" as MINIO

UI <--> WW : PostMessage / Eventos
UI --> NGINX : HTTP / SPA Bundle
NGINX --> API : Proxy /api/
WW --> NGINX : Upload / Polling

API --> MINIO : 1. Sube audio crudo
API --> DB : 2. Inserta registro inicial
API --> EX : 3. Publica 'audio.uploaded'

EX --> Q_VAL : audio.uploaded
EX --> Q_CMP : audio.validated
EX --> Q_TX : audio.compressed
EX --> Q_PROC : audio.processed

Q_VAL --> W_VAL : Consume
W_VAL --> MINIO : Descarga para validar
W_VAL --> DB : Actualiza status: validated/rejected
W_VAL --> EX : Publica 'audio.validated'

Q_CMP --> W_CMP : Consume
W_CMP --> MINIO : Descarga audio
W_CMP --> MINIO : Sube audio comprimido .aac
W_CMP --> DB : Actualiza URLs y metricas
W_CMP --> EX : Publica 'audio.compressed'

Q_TX --> W_TX : Consume
W_TX --> MINIO : Descarga AAC
W_TX --> DB : Guarda Transcripcion & Resumen
W_TX --> EX : Publica 'audio.processed'

Q_PROC --> API_CONS : Consume
API_CONS --> DB : Marca ProcessedAt
@enduml
```

---

## Diagramas de Flujo del Pipeline de Datos

### Flujo Secuencial Integral (End-to-End)

Fuente PlantUML: [`doc/diagrams/02_flujo_secuencial_pipeline.puml`](file:///c:/Users/PC/Documents/jala/progra7/capstone/doc/diagrams/02_flujo_secuencial_pipeline.puml)

```plantuml
@startuml
autonumber

actor Usuario
participant "React UI / Dropzone" as UI
participant "Web Worker" as WW
participant "Capstone.Api" as API
participant "MinIO (S3)" as MinIO
database "PostgreSQL" as DB
queue "RabbitMQ (capstone.audio)" as RMQ
participant "Validator Worker" as VAL
participant "Compressor Worker" as CMP
participant "Transcriber / LLM Worker" as TX

Usuario -> UI : Arrastra y suelta archivo de audio
UI -> WW : Encola archivo (START_UPLOAD)
WW -> API : POST /api/audio/upload (multipart/form-data)

group Fase 1: Ingesta Inmediata
  API -> MinIO : Stream upload a bucket 'audio'
  MinIO --> API : URL de almacenamiento (OriginalStorageUrl)
  API -> DB : INSERT INTO AudioFiles (Status='uploaded')
  API -> RMQ : Publica evento 'audio.uploaded'
  API --> WW : 201 Created (AudioId, FileName, Status='uploaded')
end

WW --> UI : Notifica estado 'processing'

par Polling desacoplado en cliente
  loop Cada 2.5 segundos
    WW -> API : GET /api/audio/{id}/result
    API -> DB : Consulta estado y relaciones
    API --> WW : JSON Estado actual (pending / ready / rejected)
  end
else Pipeline Asincrono en Backend
  group Fase 2: Validacion Estricta
    RMQ -> VAL : Mensaje de cola 'audio.validate'
    VAL -> MinIO : Descarga primeros bytes a temp
    VAL -> VAL : Comprobacion de Magic Bytes (MP3, WAV, OGG, FLAC, M4A)
    alt Formato no valido
      VAL -> DB : UPDATE AudioFiles SET Status='rejected', Reason='...'
      VAL -> RMQ : BasicAck (Termina procesamiento)
    else Formato verificado
      VAL -> DB : UPDATE AudioFiles SET Status='validated', ValidatedAt=NOW()
      VAL -> RMQ : Publica evento 'audio.validated'
      VAL -> RMQ : BasicAck
    end
  end

  group Fase 3: Transcodificacion y Compresion
    RMQ -> CMP : Mensaje de cola 'audio.compress'
    CMP -> MinIO : Descarga archivo original
    CMP -> CMP : Ejecuta FFmpeg (Transcodifica a AAC 128kbps)
    CMP -> MinIO : Sube archivo comprimido (.aac)
    CMP -> DB : UPDATE AudioFiles SET FilteredStorageUrl=..., CompressionDurationMs=...
    CMP -> RMQ : Publica evento 'audio.compressed'
    CMP -> RMQ : BasicAck
  end

  group Fase 4: Inteligencia Artificial
    RMQ -> TX : Mensaje de cola 'audio.transcribe'
    TX -> MinIO : Descarga archivo comprimido
    TX -> TX : Whisper.net: Transcripcion fonetica (speech-to-text)
    TX -> DB : INSERT INTO AudioTranscriptions
    TX -> TX : LLamaSharp + Qwen2.5 LLM: Generacion de resumen
    TX -> DB : INSERT INTO AudioSummaries
    TX -> RMQ : Publica evento 'audio.processed'
    TX -> RMQ : BasicAck
  end

  API -> DB : AudioProcessedEventConsumer marca ProcessedAt
end

WW -> API : GET /api/audio/{id}/result
API --> WW : 200 OK (Status='ready', TranscriptionDto, SummaryDto)
WW --> UI : Notifica estado 'ready' con datos de IA
UI --> Usuario : Muestra texto transcrito, resumen IA y selector Original / Comprimido
@enduml
```

---

### Topología del Message Broker (RabbitMQ)

Fuente PlantUML: [`doc/diagrams/03_topologia_rabbitmq.puml`](file:///c:/Users/PC/Documents/jala/progra7/capstone/doc/diagrams/03_topologia_rabbitmq.puml)

```plantuml
@startuml
skinparam componentStyle rectangle

package "Productores" {
  [UploadAudioUseCase\n(Capstone.Api)] as P1
  [AudioValidatedPublisher\n(Capstone.Validator)] as P2
  [AudioCompressedPublisher\n(Capstone.Compressor)] as P3
  [AudioProcessedPublisher\n(Capstone.Transcriber)] as P4
}

package "Broker: Topic Exchange" {
  queue "Exchange: capstone.audio\n(Type: Topic, Durable)" as EX
}

package "Colas Durables" {
  queue "audio.validate\n(Binding: audio.uploaded)" as Q1
  queue "audio.compress\n(Binding: audio.validated)" as Q2
  queue "audio.transcribe\n(Binding: audio.compressed)" as Q3
  queue "audio.processed.api\n(Binding: audio.processed)" as Q4
}

package "Consumidores (Workers)" {
  [AudioValidationWorker\n(MaxConcurrency: 4)] as C1
  [AudioCompressionWorker\n(MaxConcurrency: 4)] as C2
  [AudioProcessingWorker\n(MaxConcurrency: 2)] as C3
  [AudioProcessedEventConsumer\n(API HostedService)] as C4
}

P1 --> EX : audio.uploaded
P2 --> EX : audio.validated
P3 --> EX : audio.compressed
P4 --> EX : audio.processed

EX --> Q1 : audio.uploaded
EX --> Q2 : audio.validated
EX --> Q3 : audio.compressed
EX --> Q4 : audio.processed

Q1 --> C1 : BasicGet / Prefetch
Q2 --> C2 : BasicGet / Prefetch
Q3 --> C3 : BasicGet / Prefetch
Q4 --> C4 : BasicGet / Prefetch
@enduml
```

#### Contratos de Mensajería (JSON Payloads)

1. **`audio.uploaded`** y **`audio.validated`**:
```json
{
  "AudioFileId": "c56a4180-65aa-42ec-a945-5fd21dec0538",
  "StorageUrl": "http://minio:9000/audio/c56a4180_conferencia.mp3",
  "FileName": "conferencia.mp3"
}
```

2. **`audio.compressed`**:
```json
{
  "AudioFileId": "c56a4180-65aa-42ec-a945-5fd21dec0538",
  "StorageUrl": "http://minio:9000/audio/c56a4180_conferencia.aac",
  "FileName": "conferencia.aac"
}
```

3. **`audio.processed`**:
```json
{
  "AudioFileId": "c56a4180-65aa-42ec-a945-5fd21dec0538",
  "ProcessedAt": "2026-09-06T14:32:10.123Z"
}
```

---

### Máquina de Estados del Archivo de Audio

Fuente PlantUML: [`doc/diagrams/04_maquina_estados.puml`](file:///c:/Users/PC/Documents/jala/progra7/capstone/doc/diagrams/04_maquina_estados.puml)

```plantuml
@startuml
[*] --> uploaded : API recibe archivo y guarda en MinIO

state uploaded {
  uploaded : Audio original guardado en S3
  uploaded : En espera de validacion
}

uploaded --> validated : Validator comprueba Magic Bytes validos
uploaded --> rejected : Magic Bytes no coinciden o archivo corrupto

state rejected {
  rejected : Validacion fallida
  rejected : Se almacena mensaje de error
  rejected : Estado terminal
}

state validated {
  validated : Formato autentico verificado
  validated : En espera de transcodificacion
}

validated --> compressed : Compressor transcodifica a AAC 128k y sube variante filtrada

state compressed {
  compressed : Variante AAC lista
  compressed : En espera de inferencia Whisper y LLM
}

compressed --> ready : Transcripcion y Resumen guardados satisfactoriamente

state ready {
  ready : Procesamiento completado con exito
  ready : Disponible para consulta y streaming dual
  ready : Estado terminal
}

rejected --> [*]
ready --> [*]
@enduml
```

---

### Inspección de Seguridad y Magic Bytes

Fuente PlantUML: [`doc/diagrams/05_inspeccion_magic_bytes.puml`](file:///c:/Users/PC/Documents/jala/progra7/capstone/doc/diagrams/05_inspeccion_magic_bytes.puml)

```plantuml
@startuml
start
:Archivo recibido en Validator;
:Leer primeros 12 bytes de cabecera;

if (Es MP3? (Header 'ID3' o Sync Frame 0xFFE0)) then (si)
  :Formato Valido;
elseif (Es WAV? (Bytes 0-3 = 'RIFF' & Bytes 8-11 = 'WAVE')) then (si)
  :Formato Valido;
elseif (Es OGG? (Bytes 0-3 = 'OggS')) then (si)
  :Formato Valido;
elseif (Es FLAC? (Bytes 0-3 = 'fLaC')) then (si)
  :Formato Valido;
elseif (Es MP4 / M4A? (Bytes 4-7 = 'ftyp')) then (si)
  :Formato Valido;
elseif (Es AAC puro? (Sync Word 0xFFF0 o caja 'ftyp')) then (si)
  :Formato Valido;
else (no)
  :Formato Invalido o Falsificado;
  :Actualizar Status = 'rejected';
  :Almacenar ValidationErrorMessage;
  stop
endif

:Actualizar Status = 'validated';
:Publicar 'audio.validated';
stop
@enduml
```

---

### Pipeline de Inteligencia Artificial (Whisper + Qwen LLM)

Fuente PlantUML: [`doc/diagrams/06_pipeline_ia_whisper_qwen.puml`](file:///c:/Users/PC/Documents/jala/progra7/capstone/doc/diagrams/06_pipeline_ia_whisper_qwen.puml)

```plantuml
@startuml
start

partition "1. Ingesta de Audio para Inferencia" {
  :Audio AAC Comprimido (MinIO);
  :Descarga a disco temporal (/tmp);
}

partition "2. Transcripcion Fonetica Automatica (Speech-to-Text)" {
  :FFmpeg: Conversion a PCM 16-bit 16kHz Mono WAV;
  :Whisper.net (Modelo ggml-tiny.bin);
  :Texto Completo Transcrito (FullText);
  :Guardar en tabla AudioTranscriptions;
}

partition "3. Sintesis y Resumen Semantico (LLM)" {
  :Construccion de Prompt Estructurado:\n'Resume en pocas oraciones clave el siguiente audio...';
  :LLamaSharp Engine\nModelo: Qwen2.5-0.5B-Instruct-Q8_0.gguf\n(Contexto: 2048 tokens, Temp: 0.3);
  :Texto de Resumen Conciso;
  :Guardar en tabla AudioSummaries;
}

partition "4. Notificacion y Cierre de Evento" {
  :Publicar 'audio.processed' en RabbitMQ;
}

stop
@enduml
```

---

## Arquitectura de Software (Clean Architecture)

Fuente PlantUML: [`doc/diagrams/07_clean_architecture.puml`](file:///c:/Users/PC/Documents/jala/progra7/capstone/doc/diagrams/07_clean_architecture.puml)

```plantuml
@startuml
package "Capa 1: Capstone.Domain (Nucleo Puro)" {
  class AudioFile {
    +Guid Id
    +string FileName
    +string ContentType
    +long Size
    +string StorageUrl
    +DateTime UploadedAt
    +string Status
    +string ValidationErrorMessage
    +string OriginalStorageUrl
    +string FilteredStorageUrl
  }
  class AudioTranscription {
    +Guid Id
    +Guid AudioFileId
    +string FullText
    +string ModelName
    +long ProcessingTimeMs
  }
  class AudioSummary {
    +Guid Id
    +Guid AudioFileId
    +Guid TranscriptionId
    +string SummaryText
    +string ModelName
  }
}

package "Capa 2: Capstone.Application (Casos de Uso & Puertos)" {
  class UploadAudioUseCase
  class GetAudioResultUseCase
  class GetAudioFilesListUseCase

  interface IFileStorageService
  interface IAudioFileRepository
  interface IAudioEventPublisher
  interface IAudioTranscriptionRepository
}

package "Capa 3: Capstone.Infrastructure (Adaptadores Concretos)" {
  class MinIOStorageService
  class AudioFileRepository
  class RabbitMQEventPublisher
  class FFmpegCompressionService
  class AppDbContext
}

package "Capa 4: Capstone.Api & Services (Presentacion y Workers)" {
  class AudioController
  class AudioValidationWorker
  class AudioCompressionWorker
  class AudioProcessingWorker
}

"Capa 2: Capstone.Application (Casos de Uso & Puertos)" ..> "Capa 1: Capstone.Domain (Nucleo Puro)"
"Capa 3: Capstone.Infrastructure (Adaptadores Concretos)" ..> "Capa 2: Capstone.Application (Casos de Uso & Puertos)"
"Capa 3: Capstone.Infrastructure (Adaptadores Concretos)" ..> "Capa 1: Capstone.Domain (Nucleo Puro)"
"Capa 4: Capstone.Api & Services (Presentacion y Workers)" ..> "Capa 3: Capstone.Infrastructure (Adaptadores Concretos)"
"Capa 4: Capstone.Api & Services (Presentacion y Workers)" ..> "Capa 2: Capstone.Application (Casos de Uso & Puertos)"
@enduml
```

### Estructura de Directorios del Repositorio

```
capstone/
├── src/
│   ├── Capstone.Domain/              # Entidades sin dependencias externas (AudioFile, AudioTranscription, etc.)
│   ├── Capstone.Application/         # Casos de uso, interfaces/puertos y DTOs
│   ├── Capstone.Infrastructure/      # Implementación de MinIO, PostgreSQL/EF Core, RabbitMQ, FFmpeg
│   └── Capstone.Api/                 # ASP.NET Core Web API, Middleware, Swagger, Endpoints
├── services/
│   ├── Capstone.Validator/           # Worker de validación de Magic Bytes
│   ├── Capstone.Compressor/          # Worker de compresión a AAC 128kbps
│   └── Capstone.Transcriber/         # Worker de transcripción Whisper y resumen Qwen LLM
├── web/                              # SPA React + TypeScript + Vite + Nginx
│   ├── src/
│   │   ├── components/               # AudioList, FileDropzone, AudioPreviewBar (dual player), etc.
│   │   ├── workers/                  # audioUpload.worker.ts (Web Worker multihilo)
│   │   └── services/                 # Llamadas HTTP tipadas
├── doc/
│   └── diagrams/                     # Fuentes .puml estándar de todos los diagramas del sistema
├── k6/                               # Scripts de prueba de carga y benchmarking con k6
├── tests/
│   └── Capstone.Tests/               # Pruebas unitarias de casos de uso y servicios
├── docker-compose.yml                # Orquestación de los 8 contenedores
└── README.md                         # Documentación integral de arquitectura
```

---

## Modelo de Datos Relacional (ERD)

Fuente PlantUML: [`doc/diagrams/08_modelo_datos_erd.puml`](file:///c:/Users/PC/Documents/jala/progra7/capstone/doc/diagrams/08_modelo_datos_erd.puml)

```plantuml
@startuml
entity "AudioFiles" as AF {
  * Id : uuid <<PK>>
  --
  * FileName : varchar(255)
  * ContentType : varchar(100)
  * Size : bigint
  * StorageUrl : varchar(500)
  * UploadedAt : timestamp
  * Status : varchar(50)
  ValidationErrorMessage : varchar(500)
  ValidatedAt : timestamp
  * OriginalFileName : varchar(255)
  * OriginalContentType : varchar(100)
  * OriginalSize : bigint
  CompressedAt : timestamp
  CompressionDurationMs : bigint
  ProcessedAt : timestamp
  OriginalStorageUrl : varchar(500)
  FilteredStorageUrl : varchar(500)
}

entity "AudioTranscriptions" as AT {
  * Id : uuid <<PK>>
  --
  * AudioFileId : uuid <<FK>>
  * FullText : text
  * ModelName : varchar(100)
  * ProcessingTimeMs : bigint
  * CreatedAt : timestamp
}

entity "AudioSummaries" as ASUM {
  * Id : uuid <<PK>>
  --
  * AudioFileId : uuid <<FK>>
  * TranscriptionId : uuid <<FK>>
  * SummaryText : text
  * ModelName : varchar(100)
  * CreatedAt : timestamp
}

AF ||--o| AT : "tiene (1 a 0..1)"
AF ||--o| ASUM : "tiene (1 a 0..1)"
AT ||--o| ASUM : "origina (1 a 0..1)"
@enduml
```

---

## Arquitectura del Frontend y Concurrencia

Fuente PlantUML: [`doc/diagrams/09_frontend_concurrencia.puml`](file:///c:/Users/PC/Documents/jala/progra7/capstone/doc/diagrams/09_frontend_concurrencia.puml)

```plantuml
@startuml
package "Navegador - Hilo Principal (UI)" {
  [FileDropzone\n(Drag & Drop / Selector)] as DROP
  [Estado Global / React Hooks\n(audioItems Map)] as STATE
  [AudioCard & AudioList\n(Tarjetas y Estados)] as CARD
  [AudioPreviewBar\n(Reproductor Dual A/B)] as PLAYER

  DROP -> STATE
  STATE -> CARD
  CARD -> PLAYER
}

package "Navegador - Hilo Secundario (Web Worker)" {
  [audioUpload.worker.ts] as WORKER
  [Cola de Subida Concurrente] as QUEUE
  [Interval Polling Engine\n(2500ms por archivo)] as POLL

  WORKER -> QUEUE
  QUEUE -> POLL
}

package "Servidor Backend (Nginx / API)" {
  [POST /api/audio/upload] as API_UP
  [GET /api/audio/{id}/result] as API_RES
}

DROP --> WORKER : START_UPLOAD
QUEUE --> API_UP : fetch multipart
API_UP --> WORKER : 201 Created con ID
POLL --> API_RES : fetch status
WORKER --> STATE : postMessage (STATUS_UPDATE:\nuploading / processing / ready / rejected)
@enduml
```

---

## Streaming y Comparativa de Audio Dual

Fuente PlantUML: [`doc/diagrams/10_streaming_dual.puml`](file:///c:/Users/PC/Documents/jala/progra7/capstone/doc/diagrams/10_streaming_dual.puml)

```plantuml
@startuml
autonumber
actor Usuario
participant "AudioPreviewBar (UI)" as Player
participant "AudioController.Stream" as API
participant "MinIO Object Storage" as MinIO

Usuario -> Player : Selecciona variante "Original"
Player -> API : GET /api/audio/{id}/stream/original\n(Range: bytes=0-1048575)
API -> MinIO : Lee stream del objeto 'OriginalStorageUrl'
MinIO --> API : Stream binario
API --> Player : HTTP 206 Partial Content (audio/mpeg o codec nativo)

Usuario -> Player : Conmuta a variante "Filtrado (AAC)"
Player -> API : GET /api/audio/{id}/stream/filtered\n(Range: bytes=0-1048575)
API -> MinIO : Lee stream del objeto 'FilteredStorageUrl'
MinIO --> API : Stream binario
API --> Player : HTTP 206 Partial Content (audio/aac)

note over Player
  El usuario percibe la equivalencia de calidad acustica
  con una reduccion de mas del 70% en el peso del archivo.
end note
@enduml
```

---

## Especificación de la API REST

Base URL en desarrollo local: `http://localhost:5069`  
Base URL a través de Nginx (Docker): `http://localhost:8080`  
Documentación interactiva Swagger: `http://localhost:5069/swagger`

| Método | Endpoint | Parámetros / Body | Códigos HTTP | Descripción |
| :--- | :--- | :--- | :--- | :--- |
| `POST` | `/api/audio/upload` | Form-data `file` (Hasta 50 MB) | `201`, `400`, `413` | Sube archivo, almacena en MinIO y encola validación |
| `GET` | `/api/audio/{id}/result` | Path: `id` (UUID) | `200`, `400`, `404` | Obtiene el estado (`pending`, `ready`, `rejected`), transcripción y resumen |
| `GET` | `/api/audio` | Ninguno | `200` | Lista todos los audios procesados o en cola |
| `GET` | `/api/audio/{id}/stream/original` | Path: `id` (UUID), Soporta Range | `200`, `206`, `404` | Streaming de la variante de audio original |
| `GET` | `/api/audio/{id}/stream/filtered` | Path: `id` (UUID), Soporta Range | `200`, `206`, `404` | Streaming de la variante comprimida a AAC |

---

## Infraestructura y Despliegue con Docker

Fuente PlantUML: [`doc/diagrams/11_infraestructura_docker.puml`](file:///c:/Users/PC/Documents/jala/progra7/capstone/doc/diagrams/11_infraestructura_docker.puml)

```plantuml
@startuml
node "Docker Host" {
  portin "Puerto 8080" as P8080
  portin "Puerto 5069" as P5069
  portin "Puerto 15672" as P15672
  portin "Puerto 9001" as P9001
  portin "Puerto 5432" as P5432

  node "Docker Network: capstone (Bridge)" {
    [capstone-web (Nginx)] as WEB
    [capstone-api (.NET 8 Web API)] as API
    [capstone-validator (Worker Validacion)] as VAL
    [capstone-compressor (Worker FFmpeg)] as CMP
    [capstone-worker (Transcriber Whisper+Qwen)] as WRK
    queue "capstone-rabbitmq (RabbitMQ 4)" as RMQ
    database "capstone-db (PostgreSQL 16)" as DB
    storage "capstone-minio (MinIO S3)" as MINIO
  }
}

P8080 --> WEB
P5069 --> API
P15672 --> RMQ
P9001 --> MINIO
P5432 --> DB

WEB --> API : Proxy /api/
API --> RMQ
API --> DB
API --> MINIO

VAL --> RMQ
VAL --> DB
VAL --> MINIO

CMP --> RMQ
CMP --> DB
CMP --> MINIO

WRK --> RMQ
WRK --> DB
WRK --> MINIO
@enduml
```

### Puertos y Credenciales por Defecto

| Servicio | Puerto Host | Puerto Contenedor | Credenciales / Acceso |
| :--- | :--- | :--- | :--- |
| **Frontend Web (Nginx)** | `8080` | `80` | `http://localhost:8080` |
| **API REST (.NET)** | `5069` | `8080` | `http://localhost:5069/swagger` |
| **RabbitMQ Management** | `15672` | `15672` | `http://localhost:15672` (guest / guest) |
| **RabbitMQ AMQP** | `5672` | `5672` | Protocolo AMQP para los workers |
| **MinIO Console** | `9001` | `9001` | `http://localhost:9001` (minioadmin / minioadmin) |
| **MinIO S3 API** | `9000` | `9000` | Endpoint S3 para API y Workers |
| **PostgreSQL** | `5432` | `5432` | `postgres` / `postgres` (DB: `capstone_audio`) |

---

## Guía de Ejecución Local y con Contenedores

### Requisitos Previos
- [Docker](https://www.docker.com/) & Docker Compose v2+
- [.NET 8 SDK](https://dotnet.microsoft.com/) (si se ejecuta fuera de Docker)
- [Node.js 20+](https://nodejs.org/) y [pnpm](https://pnpm.io/) (para desarrollo web local)
- [FFmpeg](https://ffmpeg.org/) instalado y disponible en PATH (solo para desarrollo sin Docker)
- Archivos de modelos IA en `services/Capstone.Transcriber/models/`:
  - `ggml-tiny.bin` (Whisper ASR)
  - `qwen2.5-0.5b-instruct-q8_0.gguf` (LLM de Resúmenes)

---

### Opción A: Despliegue Completo con Docker (Recomendado)

1. **Clonar el repositorio y situarse en la raíz:**
   ```bash
   cd capstone
   ```

2. **Verificar los modelos de IA:**
   Asegúrese de que la carpeta `services/Capstone.Transcriber/models/` contiene:
   - `ggml-tiny.bin`
   - `qwen2.5-0.5b-instruct-q8_0.gguf`

3. **Construir y levantar todos los servicios:**
   ```bash
   docker compose up -d --build
   ```

4. **Verificar el estado de los contenedores:**
   ```bash
   docker compose ps
   ```

5. **Acceder a las aplicaciones:**
   - **Aplicación Web**: [http://localhost:8080](http://localhost:8080)
   - **Documentación Swagger API**: [http://localhost:5069/swagger](http://localhost:5069/swagger)
   - **Consola RabbitMQ**: [http://localhost:15672](http://localhost:15672) (User: `guest`, Pass: `guest`)
   - **Consola MinIO**: [http://localhost:9001](http://localhost:9001) (User: `minioadmin`, Pass: `minioadmin`)

---

### Opción B: Ejecución en Entorno de Desarrollo Híbrido

Si desea depurar el código de la API o de un worker en Visual Studio / VS Code mientras los servicios auxiliares corren en Docker:

1. **Levantar solo la infraestructura de soporte:**
   ```bash
   docker compose up -d postgres minio rabbitmq
   ```

2. **Configurar el archivo `.env`:**
   Copiar `.env.example` a `.env` en la raíz y configurar `HOST_IP=localhost`.

3. **Ejecutar la API:**
   ```bash
   dotnet run --project src/Capstone.Api
   ```

4. **Ejecutar los Workers (en terminales separadas):**
   ```bash
   dotnet run --project services/Capstone.Validator
   dotnet run --project services/Capstone.Compressor
   dotnet run --project services/Capstone.Transcriber
   ```

5. **Ejecutar el Frontend Web:**
   ```bash
   cd web
   pnpm install
   pnpm dev
   ```
   Acceder a `http://localhost:5173`.

---

## Estrategia de Pruebas y Benchmarking de Rendimiento

### 1. Pruebas Unitarias e Integración (.NET)

La suite de pruebas con **xUnit** y **Moq** cubre:
- Casos de uso de subida y consulta (`UploadAudioUseCase`, `GetAudioResultUseCase`, `GetAudioFilesListUseCase`).
- Detección y compresión con FFmpeg (`FFmpegCompressionServiceTests`).
- Concurrencia de workers y límites de hilos (`AudioProcessingWorkerConcurrencyTests`).
- Serialización y publicación de eventos RabbitMQ (`AudioProcessedPublisherTests`).

Para ejecutar todas las pruebas:
```bash
dotnet test tests/Capstone.Tests
```

---

### 2. Pruebas de Rendimiento y Carga con k6

El proyecto incluye un script de benchmarking en `k6/upload-audio.js` configurado con etapas de estrés incremental:
- **Etapa 1**: 5 usuarios virtuales (15s).
- **Etapa 2**: 10 usuarios virtuales simultáneos (30s).
- **Etapa 3**: Pico de 25 usuarios virtuales (15s).
- **Etapa 4**: Enfriamiento a 0 usuarios (15s).

**Métricas y Umbrales Evaluados:**
- Latencia percentil 95: `p(95) < 10000 ms`.
- Tasa de éxito de subida: `rate > 95%`.
- Tasa de fallos HTTP: `rate < 5%`.

Para ejecutar el benchmark:
```bash
k6 run k6/upload-audio.js
```
