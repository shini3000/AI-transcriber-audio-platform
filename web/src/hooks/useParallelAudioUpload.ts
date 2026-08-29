import { useCallback, useEffect, useRef, useState } from 'react'
import type {
  ProcessingAudioItem,
  WorkerInMessage,
  WorkerOutMessage,
} from '../types/parallelUpload'

const rawBaseUrl = import.meta.env.VITE_API_BASE_URL ?? (import.meta.env.DEV ? 'http://localhost:5069' : '')
const API_BASE_URL = rawBaseUrl.replace(/\/api\/?$/, '').replace(/\/$/, '')

interface UseParallelAudioUploadProps {
  onItemCompleted?: () => void
}

export function useParallelAudioUpload({
  onItemCompleted,
}: UseParallelAudioUploadProps = {}) {
  const [items, setItems] = useState<ProcessingAudioItem[]>([])
  const workerRef = useRef<Worker | null>(null)

  const onItemCompletedRef = useRef(onItemCompleted)
  useEffect(() => {
    onItemCompletedRef.current = onItemCompleted
  }, [onItemCompleted])

  useEffect(() => {
    const worker = new Worker(
      new URL('../workers/audioUpload.worker.ts', import.meta.url),
      { type: 'module' },
    )
    workerRef.current = worker

    worker.onmessage = (event: MessageEvent<WorkerOutMessage>) => {
      const msg = event.data
      if (msg.type === 'STATUS_UPDATE') {
        setItems((prev) =>
          prev.map((item) => {
            if (item.clientId !== msg.clientId) return item
            return {
              ...item,
              status: msg.status,
              uploadResponse: msg.uploadResponse ?? item.uploadResponse,
              audioResult: msg.audioResult ?? item.audioResult,
              error: msg.error ?? (msg.status === 'ready' ? null : item.error),
            }
          }),
        )

        if (msg.status === 'ready' || msg.status === 'rejected') {
          onItemCompletedRef.current?.()
        }
      }
    }

    return () => {
      worker.terminate()
      workerRef.current = null
    }
  }, [])

  const startProcessing = useCallback((files: File[]) => {
    if (!files.length || !workerRef.current) return

    const targetFiles = files.slice(0, 3)

    const newItems: ProcessingAudioItem[] = targetFiles.map((file) => ({
      clientId: crypto.randomUUID
        ? crypto.randomUUID()
        : `${Date.now()}_${Math.random().toString(36).slice(2)}`,
      file,
      fileName: file.name,
      fileSize: file.size,
      status: 'idle',
    }))

    setItems(newItems)

    newItems.forEach((item) => {
      const msg: WorkerInMessage = {
        type: 'START_UPLOAD',
        item: {
          clientId: item.clientId,
          file: item.file,
          apiBaseUrl: API_BASE_URL,
        },
      }
      workerRef.current?.postMessage(msg)
    })
  }, [])

  const removeItem = useCallback((clientId: string) => {
    workerRef.current?.postMessage({
      type: 'CANCEL_ITEM',
      clientId,
    } as WorkerInMessage)

    setItems((prev) => prev.filter((item) => item.clientId !== clientId))
  }, [])

  const resetAll = useCallback(() => {
    items.forEach((item) => {
      workerRef.current?.postMessage({
        type: 'CANCEL_ITEM',
        clientId: item.clientId,
      } as WorkerInMessage)
    })
    setItems([])
  }, [items])

  const isProcessing = items.some(
    (item) => item.status === 'uploading' || item.status === 'processing',
  )

  return {
    items,
    isProcessing,
    startProcessing,
    removeItem,
    resetAll,
  }
}
