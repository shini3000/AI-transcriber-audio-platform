import { formatBytes } from '../lib/formatBytes'

export const ACCEPTED_AUDIO_MIME_TYPES = [
  'audio/mpeg',
  'audio/wav',
  'audio/x-wav',
  'audio/mp4',
  'audio/ogg',
  'audio/flac',
  'audio/aac',
  'audio/webm',
] as const

export const ACCEPTED_AUDIO_EXTENSIONS = [
  '.mp3',
  '.wav',
  '.m4a',
  '.ogg',
  '.flac',
  '.aac',
  '.webm',
] as const

export type AudioMimeType = (typeof ACCEPTED_AUDIO_MIME_TYPES)[number]
export type AudioExtension = (typeof ACCEPTED_AUDIO_EXTENSIONS)[number]

export const MAX_FILE_SIZE_BYTES = 50 * 1024 * 1024

export type AudioFileValidation = { ok: true } | { ok: false; error: string }

function getFileExtension(fileName: string): string {
  const dotIndex = fileName.lastIndexOf('.')
  return dotIndex >= 0 ? fileName.slice(dotIndex).toLowerCase() : ''
}

export function validateAudioFile(file: File): AudioFileValidation {
  if (file.size > MAX_FILE_SIZE_BYTES) {
    return {
      ok: false,
      error: `File exceeds the ${formatBytes(MAX_FILE_SIZE_BYTES)} size limit.`,
    }
  }

  const mimeType = file.type.toLowerCase()
  const extension = getFileExtension(file.name)
  const isAccepted =
    (ACCEPTED_AUDIO_MIME_TYPES as readonly string[]).includes(mimeType) ||
    (ACCEPTED_AUDIO_EXTENSIONS as readonly string[]).includes(extension)

  if (!isAccepted) {
    return {
      ok: false,
      error: 'Unsupported audio format. Accepted: MP3, WAV, M4A, OGG, FLAC, AAC, WebM.',
    }
  }

  return { ok: true }
}
