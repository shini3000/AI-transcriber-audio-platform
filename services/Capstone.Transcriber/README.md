# Capstone.Transcriber - Worker Service

Worker service que escucha eventos de RabbitMQ, transcribe audios con Whisper.net y genera resúmenes con LLamaSharp + Qwen2.5.

## Flujo

```
RabbitMQ (exchange: capstone.audio, routing: audio.uploaded)
  ↓
AudioProcessingWorker
  → descarga audio de MinIO
  → convierte AAC => WAV con FFmpeg
  → transcribe con Whisper.net (modelo tiny)
  → guarda AudioTranscription en PostgreSQL
  → resume con LLamaSharp + Qwen2.5-0.5B GGUF
  → guarda AudioSummary en PostgreSQL
```

## Configuración

Copiar `.env` con los valores correctos:

| Variable | Descripción |
|---|---|
| `RabbitMQ__Host` | IP del servidor RabbitMQ |
| `RabbitMQ__Port` | Puerto AMQP (5672) |
| `MinIO__Endpoint` | IP:puerto de MinIO |
| `ConnectionStrings__DefaultConnection` | Connection string PostgreSQL |
| `Model__QwenPath` | Ruta absoluta al GGUF de Qwen2.5 |

## Ejecutar

```bash
cd services/Capstone.Transcriber
dotnet run
```

## Requisitos

- .NET 8 SDK
- PostgreSQL accesible (tablas `AudioTranscriptions` y `AudioSummaries`)
- MinIO accesible
- RabbitMQ accesible (exchange `capstone.audio` con routing key `audio.uploaded`)
- Modelo Qwen2.5-0.5B-Instruct-Q8_0.gguf en la ruta configurada
- ggml-tiny.bin en la ruta configurada
