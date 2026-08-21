import { useRef, useState } from 'react'
import type { ChangeEvent, DragEvent, KeyboardEvent } from 'react'
import { UploadCloud, FileAudio, Plus, Trash2 } from 'lucide-react'
import { MAX_FILE_SIZE_BYTES } from '../types/upload'
import { formatBytes } from '../lib/formatBytes'
import './FileDropzone.css'

interface FileDropzoneProps {
  files: File[]
  error?: string | null
  onFilesChange: (files: File[]) => void
  disabled?: boolean
  maxFiles?: number
}

export function FileDropzone({
  files,
  error,
  onFilesChange,
  disabled = false,
  maxFiles = 3,
}: FileDropzoneProps) {
  const inputRef = useRef<HTMLInputElement>(null)
  const [isDragging, setIsDragging] = useState(false)
  const [localWarning, setLocalWarning] = useState<string | null>(null)

  const handleCandidates = (candidates: FileList | File[] | null) => {
    if (!candidates || candidates.length === 0) return

    setLocalWarning(null)
    const newFileList = Array.from(candidates)
    const combined = [...files, ...newFileList]

    if (combined.length > maxFiles) {
      setLocalWarning(`Maximum ${maxFiles} files allowed. Only the first ${maxFiles} were kept.`)
      onFilesChange(combined.slice(0, maxFiles))
    } else {
      onFilesChange(combined)
    }
  }

  const handleRemove = (index: number) => {
    setLocalWarning(null)
    const updated = files.filter((_, i) => i !== index)
    onFilesChange(updated)
  }

  const openFileDialog = () => {
    if (disabled || files.length >= maxFiles) return
    inputRef.current?.click()
  }

  const handleInputChange = (event: ChangeEvent<HTMLInputElement>) => {
    handleCandidates(event.target.files)
    event.target.value = ''
  }

  const handleKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    if (disabled || files.length >= maxFiles) return
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault()
      openFileDialog()
    }
  }

  const handleDragOver = (event: DragEvent<HTMLDivElement>) => {
    if (disabled || files.length >= maxFiles) return
    event.preventDefault()
    setIsDragging(true)
  }

  const handleDragLeave = (event: DragEvent<HTMLDivElement>) => {
    if (event.currentTarget.contains(event.relatedTarget as Node | null)) return
    setIsDragging(false)
  }

  const handleDrop = (event: DragEvent<HTMLDivElement>) => {
    if (disabled || files.length >= maxFiles) return
    event.preventDefault()
    setIsDragging(false)
    handleCandidates(event.dataTransfer.files)
  }

  const canAddMore = files.length < maxFiles

  return (
    <div className="file-dropzone-wrap">
      <div
        className={`file-dropzone${isDragging ? ' is-dragging' : ''}${!canAddMore ? ' is-full' : ''}`}
        role={canAddMore ? 'button' : undefined}
        tabIndex={canAddMore ? 0 : undefined}
        aria-label={canAddMore ? `Select up to ${maxFiles} audio files to upload` : undefined}
        aria-disabled={disabled || !canAddMore || undefined}
        onClick={openFileDialog}
        onKeyDown={handleKeyDown}
        onDragOver={handleDragOver}
        onDragLeave={handleDragLeave}
        onDrop={handleDrop}
      >
        <input
          ref={inputRef}
          type="file"
          multiple
          onChange={handleInputChange}
          hidden
        />

        {files.length === 0 ? (
          <div className="file-dropzone-empty">
            <div className="file-dropzone-icon" aria-hidden="true">
              <UploadCloud size={40} className="file-dropzone-lucide-icon" />
            </div>
            <p className="file-dropzone-instruction">
              Drag &amp; drop audio files here, or click to browse
            </p>
            <p className="file-dropzone-hint">
              Up to {maxFiles} files simultaneously · Max {formatBytes(MAX_FILE_SIZE_BYTES)} each
            </p>
          </div>
        ) : (
          <div className="file-dropzone-staged-list">
            <div className="file-dropzone-staged-header">
              <span className="file-dropzone-count-badge">
                {files.length} / {maxFiles} files selected
              </span>
              {canAddMore && !disabled && (
                <button
                  type="button"
                  className="file-dropzone-add-more-btn"
                  onClick={(e) => {
                    e.stopPropagation()
                    openFileDialog()
                  }}
                >
                  <Plus size={14} /> Add another audio
                </button>
              )}
            </div>

            <div className="file-dropzone-items">
              {files.map((f, idx) => (
                <div
                  key={`${f.name}_${idx}_${f.size}`}
                  className="file-dropzone-item"
                  onClick={(e) => e.stopPropagation()}
                >
                  <div className="file-dropzone-item-info">
                    <span className="file-dropzone-item-icon">
                      <FileAudio size={20} />
                    </span>
                    <div className="file-dropzone-item-texts">
                      <span className="file-dropzone-name" title={f.name}>
                        {f.name}
                      </span>
                      <span className="file-dropzone-size">{formatBytes(f.size)}</span>
                    </div>
                  </div>
                  <button
                    type="button"
                    className="file-dropzone-remove"
                    aria-label={`Remove ${f.name}`}
                    disabled={disabled}
                    onClick={() => handleRemove(idx)}
                  >
                    <Trash2 size={14} /> Remove
                  </button>
                </div>
              ))}
            </div>
          </div>
        )}
      </div>

      {(error || localWarning) && (
        <p className="file-dropzone-error" aria-live="polite">
          {error || localWarning}
        </p>
      )}
    </div>
  )
}
