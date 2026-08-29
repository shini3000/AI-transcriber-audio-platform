export interface UploadResponse {
  id: string
  fileName: string
  storageUrl: string
  uploadedAt: string
  compressedSize: number
  wasCompressed: boolean
}

export type AudioResultStatus = 'pending' | 'ready' | 'rejected'

export interface Transcription {
  fullText: string
  modelName: string
  processingTimeMs: number
  createdAt: string
}

export interface Summary {
  summaryText: string
  modelName: string
  createdAt: string
}

export interface AudioResult {
  status: AudioResultStatus
  audioFileId: string
  fileName: string | null
  transcription: Transcription | null
  summary: Summary | null
  validationErrorMessage?: string | null
}
