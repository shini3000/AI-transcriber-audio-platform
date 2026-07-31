import { useState } from 'react'
import type { FormEvent } from 'react'
import { FileDropzone } from './FileDropzone'
import { ApiError, uploadAudio } from '../services/api'
import type { UploadResponse } from '../types/api'
import { formatBytes } from '../lib/formatBytes'
import './UploadForm.css'

interface UploadSelection {
  file: File | null
  error: string | null
}

const EMPTY_SELECTION: UploadSelection = { file: null, error: null }

function isHttpUrl(value: string): boolean {
  return value.startsWith('http://') || value.startsWith('https://')
}

export function UploadForm() {
  const [selection, setSelection] = useState<UploadSelection>(EMPTY_SELECTION)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [result, setResult] = useState<UploadResponse | null>(null)
  const [submitError, setSubmitError] = useState<string | null>(null)

  const handleSelect = (file: File | null, error: string | null) => {
    setSelection({ file, error })
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
      {result !== null && (
        <div className="upload-form-result" aria-live="polite">
          <p className="upload-form-result-name">{result.fileName}</p>
          {selection.file !== null && (
            <p className="upload-form-result-size">
              {formatBytes(selection.file.size)}
              {result.wasCompressed
                ? ` → ${formatBytes(result.compressedSize)}`
                : ''}
            </p>
          )}
          {isHttpUrl(result.storageUrl) ? (
            <a
              className="upload-form-result-link"
              href={result.storageUrl}
              target="_blank"
              rel="noreferrer"
            >
              Open file
            </a>
          ) : (
            <p className="upload-form-result-url">{result.storageUrl}</p>
          )}
        </div>
      )}
      {submitError !== null && (
        <p className="upload-form-error" role="alert">
          {submitError}
        </p>
      )}
    </form>
  )
}
