import { useState } from 'react'
import type { FormEvent } from 'react'
import { FileDropzone } from './FileDropzone'
import { LoadingSpinner } from './LoadingSpinner'
import { TranscriptionResult } from './TranscriptionResult'
import { UploadResult } from './UploadResult'
import { UploadError } from './UploadError'
import { useAudioResult } from '../hooks/useAudioResult'
import { ApiError, uploadAudio } from '../services/api'
import type { UploadResponse } from '../types/api'
import './UploadForm.css'

interface UploadSelection {
  file: File | null
  error: string | null
}

const EMPTY_SELECTION: UploadSelection = { file: null, error: null }

export function UploadForm() {
  const [selection, setSelection] = useState<UploadSelection>(EMPTY_SELECTION)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [result, setResult] = useState<UploadResponse | null>(null)
  const [submitError, setSubmitError] = useState<string | null>(null)

  const polling = useAudioResult({ audioFileId: result?.id ?? null })

  const handleSelect = (file: File | null, error: string | null) => {
    setSelection({ file, error })
    setResult(null)
    setSubmitError(null)
  }

  const handleReset = () => {
    setSelection(EMPTY_SELECTION)
    setResult(null)
    setSubmitError(null)
  }

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (selection.file === null) {
      return
    }
    setIsSubmitting(true)
    setSubmitError(null)
    try {
      const uploadResult = await uploadAudio(selection.file)
      setResult(uploadResult)
    } catch (error) {
      setResult(null)
      setSubmitError(
        error instanceof ApiError
          ? error.message
          : 'Upload failed. Please try again.',
      )
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <form className="upload-form" onSubmit={handleSubmit}>
      <FileDropzone
        file={selection.file}
        error={selection.error}
        onSelect={handleSelect}
        disabled={isSubmitting}
      />
      <button
        type="submit"
        className="upload-form-submit"
        aria-label="Upload audio file"
        disabled={isSubmitting || selection.file === null}
      >
        {isSubmitting ? 'Uploading…' : 'Upload'}
      </button>
      {isSubmitting && (
        <div className="upload-form-status" aria-live="polite">
          <LoadingSpinner size="md" />
          <span className="upload-form-status-text">Uploading…</span>
        </div>
      )}
      {result !== null && selection.file !== null && (
        <UploadResult
          result={result}
          originalSize={selection.file.size}
          onReset={handleReset}
        />
      )}
      {result !== null && polling.isPolling && (
        <div className="upload-form-status" aria-live="polite">
          <LoadingSpinner size="sm" />
          <span className="upload-form-status-text">
            Transcribing and summarizing…
          </span>
        </div>
      )}
      {polling.result !== null && polling.result.status === 'ready' && (
        <TranscriptionResult result={polling.result} />
      )}
      {polling.error !== null && <UploadError message={polling.error} />}
      {submitError !== null && <UploadError message={submitError} />}
    </form>
  )
}
