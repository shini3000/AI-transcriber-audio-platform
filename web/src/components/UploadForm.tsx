import { useState } from 'react'
import type { FormEvent } from 'react'
import { Upload, Plus, Layers } from 'lucide-react'
import { FileDropzone } from './FileDropzone'
import { AudioCard } from './AudioCard'
import { LoadingSpinner } from './LoadingSpinner'
import { useParallelAudioUpload } from '../hooks/useParallelAudioUpload'
import './UploadForm.css'

interface UploadFormProps {
  onUploadSuccess?: () => void
}

export function UploadForm({ onUploadSuccess }: UploadFormProps) {
  const [stagedFiles, setStagedFiles] = useState<File[]>([])

  const { items, isProcessing, startProcessing, removeItem, resetAll } =
    useParallelAudioUpload({ onItemCompleted: onUploadSuccess })

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (stagedFiles.length === 0 || isProcessing) return

    startProcessing(stagedFiles)
    setStagedFiles([])
  }

  const handleStartNewBatch = () => {
    resetAll()
    setStagedFiles([])
  }

  const completedCount = items.filter(
    (item) => item.status === 'ready' || item.status === 'rejected' || item.status === 'failed',
  ).length

  return (
    <div className="upload-form-container">
      {items.length === 0 ? (
        <form className="upload-form" onSubmit={handleSubmit}>
          <FileDropzone
            files={stagedFiles}
            onFilesChange={setStagedFiles}
            disabled={isProcessing}
            maxFiles={3}
          />

          <div className="upload-form-actions">
            <button
              type="submit"
              className="upload-form-submit"
              disabled={isProcessing || stagedFiles.length === 0}
            >
              <Upload size={18} style={{ display: 'inline', marginRight: '6px', verticalAlign: 'text-bottom' }} />
              Upload
            </button>
          </div>
        </form>
      ) : (
        <div className="upload-parallel-view">
          <div className="upload-parallel-header">
            <div className="upload-parallel-title-group">
              <h3 className="upload-parallel-title" style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                <Layers size={18} /> Processing Queue
              </h3>
              <span className="upload-parallel-subtitle">
                {isProcessing
                  ? `Processing sequentially (1 by 1) · ${completedCount}/${items.length} completed`
                  : `Batch finished · ${completedCount} items processed`}
              </span>
            </div>

            <div className="upload-parallel-controls">
              {isProcessing && (
                <div className="upload-parallel-spinner-tag">
                  <LoadingSpinner size="sm" />
                  <span>Sequential Processing</span>
                </div>
              )}
              <button
                type="button"
                className="upload-new-batch-btn"
                onClick={handleStartNewBatch}
                disabled={isProcessing}
                style={{ display: 'inline-flex', alignItems: 'center', gap: '4px' }}
              >
                <Plus size={15} /> New Upload
              </button>
            </div>
          </div>

          <div className="audio-cards-grid">
            {items.map((item) => (
              <AudioCard key={item.clientId} item={item} onRemove={removeItem} />
            ))}
          </div>
        </div>
      )}
    </div>
  )
}
