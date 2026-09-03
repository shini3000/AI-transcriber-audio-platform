import { useEffect, useState } from 'react'
import {
  RefreshCw,
  FileAudio,
  Sparkles,
  FileText,
  ChevronDown,
  ChevronUp,
  CheckCircle2,
  Clock,
  Zap,
} from 'lucide-react'
import { ApiError, getAudioFiles } from '../services/api'
import type { AudioResult } from '../types/api'
import { LoadingSpinner } from './LoadingSpinner'
import './AudioList.css'

interface AudioListProps {
  refreshTrigger?: number
}

function formatCreatedAt(createdAt: string): string {
  const date = new Date(createdAt)
  return Number.isNaN(date.getTime()) ? createdAt : date.toLocaleString()
}

export function AudioList({ refreshTrigger = 0 }: AudioListProps) {
  const [audios, setAudios] = useState<AudioResult[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [expandedIds, setExpandedIds] = useState<Record<string, boolean>>({})

  const fetchList = async (showLoading = false) => {
    if (showLoading) setIsLoading(true)
    try {
      const list = await getAudioFiles()
      setAudios(list)
      setError(null)
    } catch (err) {
      setError(
        err instanceof ApiError
          ? err.message
          : 'Failed to load recordings history.',
      )
    } finally {
      if (showLoading) setIsLoading(false)
    }
  }

  useEffect(() => {
    void fetchList(true)
  }, [refreshTrigger])

  useEffect(() => {
    const hasPending = audios.some((audio) => audio.status === 'pending')
    if (!hasPending) return

    const intervalId = setInterval(() => {
      void fetchList(false)
    }, 5000)

    return () => clearInterval(intervalId)
  }, [audios])

  const toggleExpand = (id: string) => {
    setExpandedIds((prev) => ({
      ...prev,
      [id]: !prev[id],
    }))
  }

  if (isLoading) {
    return (
      <div className="audio-list-loading" role="status" aria-live="polite">
        <LoadingSpinner size="md" />
        <p>Loading recording history...</p>
      </div>
    )
  }

  if (error !== null) {
    return (
      <div className="audio-list-error" role="alert">
        <p className="audio-list-error-message">{error}</p>
        <button type="button" className="audio-list-retry" onClick={() => void fetchList(true)}>
          Retry
        </button>
      </div>
    )
  }

  if (audios.length === 0) {
    return (
      <div className="audio-list-empty">
        <FileAudio size={36} style={{ color: 'var(--color-text-muted)', marginBottom: '8px' }} />
        <p>No recordings uploaded yet.</p>
      </div>
    )
  }

  return (
    <div className="audio-list-container" role="region" aria-label="Recording history list">
      <div className="audio-list-header">
        <h3 className="audio-list-title">History ({audios.length})</h3>
        <button
          type="button"
          className="audio-list-refresh-btn"
          onClick={() => void fetchList(true)}
          aria-label="Refresh history"
          style={{ display: 'inline-flex', alignItems: 'center', gap: '4px' }}
        >
          <RefreshCw size={13} /> Refresh
        </button>
      </div>

      <div className="audio-list">
        {audios.map((audio) => {
          const isExpanded = !!expandedIds[audio.audioFileId]
          const isReady = audio.status === 'ready'

          return (
            <div key={audio.audioFileId} className={`audio-list-card ${audio.status}`}>
              <div className="audio-card-main">
                <div className="audio-card-meta">
                  <span className={`audio-status-badge ${audio.status}`}>
                    {isReady ? (
                      <>
                        <CheckCircle2 size={12} /> Ready
                      </>
                    ) : (
                      'Processing'
                    )}
                  </span>
                  <span className="audio-card-filename">{audio.fileName || 'Unnamed File'}</span>
                </div>

                {isReady && audio.summary && (
                  <div className="audio-card-summary">
                    <h4 className="audio-card-label" style={{ display: 'flex', alignItems: 'center', gap: '4px' }}>
                      <Sparkles size={13} /> Summary
                    </h4>
                    <p className="audio-card-summary-text">{audio.summary.summaryText}</p>
                    <p className="audio-card-meta-detail" style={{ display: 'flex', alignItems: 'center', gap: '4px' }}>
                      <Clock size={11} /> Model: {audio.summary.modelName} · {formatCreatedAt(audio.summary.createdAt)}
                    </p>
                  </div>
                )}

                {!isReady && (
                  <div className="audio-card-processing-status">
                    <LoadingSpinner size="sm" />
                    <span>Transcribing and generating title in the background...</span>
                  </div>
                )}

                {isReady && audio.transcription && (
                  <div className="audio-card-transcription-section">
                    <button
                      type="button"
                      className="audio-card-toggle-transcription"
                      onClick={() => toggleExpand(audio.audioFileId)}
                      style={{ display: 'inline-flex', alignItems: 'center', gap: '4px' }}
                    >
                      <FileText size={13} />
                      {isExpanded ? 'Hide Transcription' : 'Show Transcription'}
                      {isExpanded ? <ChevronUp size={13} /> : <ChevronDown size={13} />}
                    </button>
                    {isExpanded && (
                      <div className="audio-card-transcription-content">
                        <p className="audio-card-transcription-text">
                          {audio.transcription.fullText}
                        </p>
                        <p className="audio-card-meta-detail" style={{ display: 'flex', alignItems: 'center', gap: '4px' }}>
                          <Zap size={11} /> Model: {audio.transcription.modelName} · Took:{' '}
                          {(audio.transcription.processingTimeMs / 1000).toFixed(2)}s
                        </p>
                      </div>
                    )}
                  </div>
                )}
              </div>
            </div>
          )
        })}
      </div>
    </div>
  )
}
