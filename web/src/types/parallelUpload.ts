import type { AudioResult, UploadResponse } from './api'

export type ProcessingStatus =
  | 'idle'
  | 'uploading'
  | 'processing'
  | 'ready'
  | 'rejected'
  | 'failed'

export interface ProcessingAudioItem {
  clientId: string
  file: File
  fileName: string
  fileSize: number
  status: ProcessingStatus
  uploadResponse?: UploadResponse | null
  audioResult?: AudioResult | null
  error?: string | null
}

export type WorkerInMessage =
  | {
      type: 'START_UPLOAD'
      item: {
        clientId: string
        file: File
        apiBaseUrl: string
      }
    }
  | {
      type: 'CANCEL_ITEM'
      clientId: string
    }

export type WorkerOutMessage =
  | {
      type: 'STATUS_UPDATE'
      clientId: string
      status: ProcessingStatus
      uploadResponse?: UploadResponse | null
      audioResult?: AudioResult | null
      error?: string | null
    }
