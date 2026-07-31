import type { UploadResponse } from '../types/api'

export class ApiError extends Error {
  readonly status: number | undefined
  readonly payload: unknown

  constructor(message: string, status?: number, payload?: unknown) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.payload = payload
  }
}

const UPLOAD_ENDPOINT = '/api/audio/upload'

async function readErrorPayload(response: Response): Promise<unknown> {
  try {
    return await response.json()
  } catch {
    return null
  }
}

function getErrorMessage(payload: unknown, fallback: string): string {
  if (typeof payload === 'object' && payload !== null) {
    const errorBody = payload as Record<string, unknown>
    const message = errorBody.error ?? errorBody.detail ?? errorBody.title
    if (typeof message === 'string' && message.trim() !== '') {
      return message
    }
  }
  return fallback
}

export async function uploadAudio(file: File): Promise<UploadResponse> {
  const formData = new FormData()
  formData.append('file', file)

  let response: Response
  try {
    response = await fetch(UPLOAD_ENDPOINT, {
      method: 'POST',
      body: formData,
    })
  } catch {
    throw new ApiError(
      'Network error: could not reach the upload server. Check your connection and try again.',
    )
  }

  if (!response.ok) {
    const payload = await readErrorPayload(response)
    throw new ApiError(
      getErrorMessage(payload, `Upload failed with status ${response.status}.`),
      response.status,
      payload,
    )
  }

  return (await response.json()) as UploadResponse
}
