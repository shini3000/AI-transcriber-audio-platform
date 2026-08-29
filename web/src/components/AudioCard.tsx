import { useState } from 'react'
import {
  FileAudio,
  CheckCircle2,
  XCircle,
  AlertTriangle,
  Sparkles,
  FileText,
  Copy,
  Check,
  Zap,
  Clock,
  X,
  ArrowRight,
  Upload,
} from 'lucide-react'
import type { ProcessingAudioItem } from '../types/parallelUpload'
import { formatBytes } from '../lib/formatBytes'
import { LoadingSpinner } from './LoadingSpinner'
import './AudioCard.css'

interface AudioCardProps {
  item: ProcessingAudioItem
  onRemove?: (clientId: string) => void
}

function formatCreatedAt(dateStr?: string): string {
  if (!dateStr) return ''
  const date = new Date(dateStr)
  return Number.isNaN(date.getTime()) ? dateStr : date.toLocaleTimeString()
}

export function AudioCard({ item, onRemove }: AudioCardProps) {
  const [copied, setCopied] = useState(false)

  const { status, fileName, fileSize, uploadResponse, audioResult, error } = item

  const handleCopy = async () => {
    const text = audioResult?.transcription?.fullText
    if (!text) return
    try {
      await navigator.clipboard.writeText(text)
      setCopied(true)
      setTimeout(() => setCopied(false), 2000)
    } catch { }
  }

  const renderBadge = () => {
    switch (status) {
      case 'idle':
        return <span className="audio-card-badge badge-idle">Queued</span>
      case 'uploading':
        return (
          <span className="audio-card-badge badge-uploading">
            <LoadingSpinner size="sm" /> Uploading
          </span>
        )
      case 'processing':
        return (
          <span className="audio-card-badge badge-processing">
            <LoadingSpinner size="sm" /> Processing
          </span>
        )
      case 'ready':
        return (
          <span className="audio-card-badge badge-ready">
            <CheckCircle2 size={13} /> Ready
          </span>
        )
      case 'rejected':
        return (
          <span className="audio-card-badge badge-rejected">
            <XCircle size={13} /> Rejected
          </span>
        )
      case 'failed':
        return (
          <span className="audio-card-badge badge-failed">
            <AlertTriangle size={13} /> Failed
          </span>
        )
    }
  }

  const renderStatusIcon = () => {
    if (status === 'ready') return <CheckCircle2 size={22} className="icon-ready" />
    if (status === 'rejected') return <XCircle size={22} className="icon-rejected" />
    if (status === 'uploading') return <Upload size={22} className="icon-uploading" />
    return <FileAudio size={22} className="icon-default" />
  }

  return (
    <div className={`audio-card status-${status}`} role="region" aria-label={`Audio processing ${fileName}`}>
      <div className="audio-card-header">
        <div className="audio-card-header-main">
          <span className="audio-card-file-icon" aria-hidden="true">
            {renderStatusIcon()}
          </span>
          <div className="audio-card-title-group">
            <h3 className="audio-card-filename" title={fileName}>
              {fileName}
            </h3>
            <span className="audio-card-sizes">
              {formatBytes(fileSize)}
              {uploadResponse?.wasCompressed && uploadResponse.compressedSize && (
                <span className="audio-card-compressed-size">
                  <ArrowRight size={11} style={{ display: 'inline', margin: '0 2px' }} />
                  {formatBytes(uploadResponse.compressedSize)} (.aac)
                </span>
              )}
            </span>
          </div>
        </div>
        <div className="audio-card-header-actions">
          {renderBadge()}
          {onRemove && (status === 'ready' || status === 'rejected' || status === 'failed') && (
            <button
              type="button"
              className="audio-card-dismiss-btn"
              title="Remove card"
              aria-label="Remove card"
              onClick={() => onRemove(item.clientId)}
            >
              <X size={14} />
            </button>
          )}
        </div>
      </div>

      <div className="audio-card-body">
        {status === 'uploading' && (
          <div className="audio-card-state-loading">
            <div className="audio-card-progress-bar-wrap">
              <div className="audio-card-progress-bar-indeterminate" />
            </div>
            <p className="audio-card-loading-text">Uploading audio stream to MinIO storage...</p>
          </div>
        )}

        {status === 'processing' && (
          <div className="audio-card-state-loading">
            <div className="audio-card-progress-bar-wrap">
              <div className="audio-card-progress-bar-indeterminate progress-pulse" />
            </div>
            <p className="audio-card-loading-text">
              Validating magic bytes, compressing &amp; transcribing with Whisper AI...
            </p>
          </div>
        )}

        {status === 'rejected' && (
          <div className="audio-card-state-error">
            <div className="audio-card-error-banner">
              <span className="audio-card-error-icon">
                <AlertTriangle size={18} />
              </span>
              <div className="audio-card-error-text">
                <strong>Audio Validation Rejected:</strong>
                <p>{error ?? 'The uploaded file does not contain a valid audio stream header.'}</p>
              </div>
            </div>
          </div>
        )}

        {status === 'failed' && (
          <div className="audio-card-state-error">
            <div className="audio-card-error-banner">
              <span className="audio-card-error-icon">
                <XCircle size={18} />
              </span>
              <div className="audio-card-error-text">
                <strong>Processing Error:</strong>
                <p>{error ?? 'An error occurred during audio processing.'}</p>
              </div>
            </div>
          </div>
        )}

        {status === 'ready' && audioResult && (
          <div className="audio-card-results">
            {audioResult.summary && (
              <div className="audio-card-summary-box">
                <div className="audio-card-section-label">
                  <span style={{ display: 'inline-flex', alignItems: 'center', gap: '4px' }}>
                    <Sparkles size={13} /> AI Summary
                  </span>
                  <span className="audio-card-model-tag">{audioResult.summary.modelName}</span>
                </div>
                <p className="audio-card-summary-text">{audioResult.summary.summaryText}</p>
              </div>
            )}

            {audioResult.transcription && (
              <div className="audio-card-transcription-box">
                <div className="audio-card-section-label">
                  <span style={{ display: 'inline-flex', alignItems: 'center', gap: '4px' }}>
                    <FileText size={13} /> Full Transcription
                  </span>
                  <button
                    type="button"
                    className={`audio-card-copy-btn ${copied ? 'is-copied' : ''}`}
                    onClick={handleCopy}
                  >
                    {copied ? (
                      <>
                        <Check size={12} /> Copied
                      </>
                    ) : (
                      <>
                        <Copy size={12} /> Copy Text
                      </>
                    )}
                  </button>
                </div>
                <div className="audio-card-transcription-scroll">
                  <p className="audio-card-transcription-text">{audioResult.transcription.fullText}</p>
                </div>
                <div className="audio-card-footer-meta">
                  <span style={{ display: 'inline-flex', alignItems: 'center', gap: '4px' }}>
                    <Zap size={12} /> Whisper: {audioResult.transcription.processingTimeMs}ms
                  </span>
                  <span style={{ display: 'inline-flex', alignItems: 'center', gap: '4px' }}>
                    <Clock size={12} /> {formatCreatedAt(audioResult.transcription.createdAt)}
                  </span>
                </div>
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  )
}
