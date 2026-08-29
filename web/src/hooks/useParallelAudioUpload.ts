import { useCallback, useEffect, useRef, useState } from 'react'
import type {
  ProcessingAudioItem,
} from '../types/parallelUpload'
import { uploadAudio, getAudioResult } from '../services/api'

interface UseParallelAudioUploadProps {
  onItemCompleted?: () => void
}

export function useParallelAudioUpload({
  onItemCompleted,
}: UseParallelAudioUploadProps = {}) {
  const [items, setItems] = useState<ProcessingAudioItem[]>([])
  const isCancelledRef = useRef(false)

  const onItemCompletedRef = useRef(onItemCompleted)
  useEffect(() => {
    onItemCompletedRef.current = onItemCompleted
  }, [onItemCompleted])

  const startProcessing = useCallback(async (files: File[]) => {
    if (!files.length) return
    isCancelledRef.current = false

    const targetFiles = files.slice(0, 3)

    const initialItems: ProcessingAudioItem[] = targetFiles.map((file) => ({
      clientId: crypto.randomUUID
        ? crypto.randomUUID()
        : `${Date.now()}_${Math.random().toString(36).slice(2)}`,
      file,
      fileName: file.name,
      fileSize: file.size,
      status: 'idle',
    }))

    setItems(initialItems)

    for (let i = 0; i < initialItems.length; i++) {
      if (isCancelledRef.current) break

      const currentItem = initialItems[i]

      setItems((prev) =>
        prev.map((item) =>
          item.clientId === currentItem.clientId
            ? { ...item, status: 'uploading' }
            : item,
        ),
      )

      try {
        const uploadResponse = await uploadAudio(currentItem.file)

        if (isCancelledRef.current) break

        setItems((prev) =>
          prev.map((item) =>
            item.clientId === currentItem.clientId
              ? { ...item, status: 'processing', uploadResponse }
              : item,
          ),
        )

        const audioFileId = uploadResponse.id
        const pollIntervalMs = 2000
        const timeoutMs = 120000
        const startTime = Date.now()

        let finished = false
        while (!finished && !isCancelledRef.current) {
          if (Date.now() - startTime > timeoutMs) {
            setItems((prev) =>
              prev.map((item) =>
                item.clientId === currentItem.clientId
                  ? {
                    ...item,
                    status: 'failed',
                    error: 'Processing timed out after 2 minutes.',
                  }
                  : item,
              ),
            )
            break
          }

          await new Promise((resolve) => setTimeout(resolve, pollIntervalMs))
          if (isCancelledRef.current) break

          try {
            const resultData = await getAudioResult(audioFileId)

            if (resultData.status === 'ready') {
              finished = true
              setItems((prev) =>
                prev.map((item) =>
                  item.clientId === currentItem.clientId
                    ? { ...item, status: 'ready', audioResult: resultData }
                    : item,
                ),
              )
              onItemCompletedRef.current?.()
            } else if (resultData.status === 'rejected') {
              finished = true
              setItems((prev) =>
                prev.map((item) =>
                  item.clientId === currentItem.clientId
                    ? {
                      ...item,
                      status: 'rejected',
                      audioResult: resultData,
                      error:
                        resultData.validationErrorMessage ??
                        'File was rejected: invalid audio format.',
                    }
                    : item,
                ),
              )
              onItemCompletedRef.current?.()
            }
          } catch {
          }
        }
      } catch (err) {
        setItems((prev) =>
          prev.map((item) =>
            item.clientId === currentItem.clientId
              ? {
                ...item,
                status: 'failed',
                error:
                  err instanceof Error
                    ? err.message
                    : 'Network error during upload.',
              }
              : item,
          ),
        )
      }
    }
  }, [])

  const removeItem = useCallback((clientId: string) => {
    setItems((prev) => prev.filter((item) => item.clientId !== clientId))
  }, [])

  const resetAll = useCallback(() => {
    isCancelledRef.current = true
    setItems([])
  }, [])

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
