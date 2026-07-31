export interface UploadResponse {
  id: string
  fileName: string
  storageUrl: string
  uploadedAt: string
  compressedSize: number
  wasCompressed: boolean
}
