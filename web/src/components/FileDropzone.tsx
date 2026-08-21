import { useRef, useState } from 'react'
import type { ChangeEvent, DragEvent, KeyboardEvent } from 'react'
import { MAX_FILE_SIZE_BYTES } from '../types/upload'
import { formatBytes } from '../lib/formatBytes'
import './FileDropzone.css'

interface FileDropzoneProps {
  file: File | null
  error: string | null
  onSelect: (file: File | null, error: string | null) => void
  disabled?: boolean
}

export function FileDropzone({
  file,
  error,
  onSelect,
  disabled = false,
}: FileDropzoneProps) {
  const inputRef = useRef<HTMLInputElement>(null)
  const [isDragging, setIsDragging] = useState(false)

  const handleFile = (candidate: File | null) => {
    onSelect(candidate, null)
  }

  const openFileDialog = () => {
    if (disabled) {
      return
    }
    inputRef.current?.click()
  }

  const handleInputChange = (event: ChangeEvent<HTMLInputElement>) => {
    handleFile(event.target.files?.[0] ?? null)
    event.target.value = ''
  }

  const handleKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    if (disabled) {
      return
    }
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault()
      openFileDialog()
    }
  }

  const handleDragOver = (event: DragEvent<HTMLDivElement>) => {
    if (disabled) {
      return
    }
    event.preventDefault()
    setIsDragging(true)
  }

  const handleDragLeave = (event: DragEvent<HTMLDivElement>) => {
    if (event.currentTarget.contains(event.relatedTarget as Node | null)) {
      return
    }
    setIsDragging(false)
  }

  const handleDrop = (event: DragEvent<HTMLDivElement>) => {
    if (disabled) {
      return
    }
    event.preventDefault()
    setIsDragging(false)
    handleFile(event.dataTransfer.files[0] ?? null)
  }

  const isClickable = file === null

  return (
    <div className="file-dropzone-wrap">
      <div
        className={`file-dropzone${isDragging ? ' is-dragging' : ''}`}
        role={isClickable ? 'button' : undefined}
        tabIndex={isClickable ? 0 : undefined}
        aria-label={isClickable ? 'Select an audio file to upload' : undefined}
        aria-disabled={disabled || undefined}
        onClick={openFileDialog}
        onKeyDown={handleKeyDown}
        onDragOver={handleDragOver}
        onDragLeave={handleDragLeave}
        onDrop={handleDrop}
      >
        <input
          ref={inputRef}
          type="file"
          onChange={handleInputChange}
          hidden
        />
        {file ? (
          <div className="file-dropzone-preview">
            <div className="file-dropzone-preview-info">
              <span className="file-dropzone-name">{file.name}</span>
              <span className="file-dropzone-size">{formatBytes(file.size)}</span>
            </div>
            <button
              type="button"
              className="file-dropzone-remove"
              aria-label="Remove selected audio file"
              disabled={disabled}
              onClick={(event) => {
                event.stopPropagation()
                handleFile(null)
              }}
            >
              Remove
            </button>
          </div>
        ) : (
          <div className="file-dropzone-empty">
            <p className="file-dropzone-instruction">
              Drag &amp; drop an audio file here, or click to browse
            </p>
            <p className="file-dropzone-hint">
              Up to {formatBytes(MAX_FILE_SIZE_BYTES)}
            </p>
          </div>
        )}
      </div>
      {error !== null && (
        <p className="file-dropzone-error" aria-live="polite">
          {error}
        </p>
      )}
    </div>
  )
}
