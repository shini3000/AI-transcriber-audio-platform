import { useEffect, useState } from 'react'
import { ApiError, getAudioResult } from '../services/api'
import type { AudioResult } from '../types/api'

interface UseAudioResultProps {
  audioFileId: string | null
  pollIntervalMs?: number
  timeoutMs?: number
}

const DEFAULT_POLL_INTERVAL_MS = 3000
const DEFAULT_TIMEOUT_MS = 120000

export function useAudioResult({
  audioFileId,
  pollIntervalMs = DEFAULT_POLL_INTERVAL_MS,
  timeoutMs = DEFAULT_TIMEOUT_MS,
}: UseAudioResultProps) {
  const [result, setResult] = useState<AudioResult | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [isPolling, setIsPolling] = useState(false)

  useEffect(() => {
    if (audioFileId === null) {
      setResult(null)
      setError(null)
      setIsPolling(false)
      return
    }

    let cancelled = false
    let intervalId: ReturnType<typeof setInterval> | undefined
    let timeoutId: ReturnType<typeof setTimeout> | undefined

    setResult(null)
    setError(null)
    setIsPolling(true)

    const stopPolling = () => {
      if (intervalId !== undefined) {
        clearInterval(intervalId)
      }
      if (timeoutId !== undefined) {
        clearTimeout(timeoutId)
      }
    }

    const poll = async () => {
      if (cancelled) {
        return
      }
      try {
        const response = await getAudioResult(audioFileId)
        if (cancelled) {
          return
        }
        if (response.status === 'ready') {
          setResult(response)
          setIsPolling(false)
          stopPolling()
        }
      } catch (error) {
        if (cancelled) {
          return
        }
        if (error instanceof ApiError && error.status !== undefined) {
          if (error.status === 404) {
            setError('Audio file not found.')
          } else {
            setError(error.message)
          }
          setIsPolling(false)
          stopPolling()
        }
      }
    }

    void poll()
    intervalId = setInterval(poll, pollIntervalMs)
    timeoutId = setTimeout(() => {
      if (cancelled) {
        return
      }
      setError('Processing is taking too long.')
      setIsPolling(false)
      stopPolling()
    }, timeoutMs)

    return () => {
      cancelled = true
      if (intervalId !== undefined) {
        clearInterval(intervalId)
      }
      if (timeoutId !== undefined) {
        clearTimeout(timeoutId)
      }
    }
  }, [audioFileId, pollIntervalMs, timeoutMs])

  return { result, error, isPolling }
}
