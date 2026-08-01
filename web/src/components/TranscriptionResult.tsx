import type { AudioResult } from '../types/api'
import './TranscriptionResult.css'

interface TranscriptionResultProps {
  result: AudioResult
}

function formatCreatedAt(createdAt: string): string {
  const date = new Date(createdAt)
  return Number.isNaN(date.getTime()) ? createdAt : date.toLocaleString()
}

export function TranscriptionResult({ result }: TranscriptionResultProps) {
  const { fileName, transcription, summary } = result

  return (
    <div
      className="transcription-result"
      role="region"
      aria-label="Transcription and summary"
    >
      {fileName !== null && (
        <h3 className="transcription-result-title">{fileName}</h3>
      )}
      {summary !== null && (
        <div className="transcription-result-summary">
          <h4 className="transcription-result-label">Summary</h4>
          <p className="transcription-result-summary-text">
            {summary.summaryText}
          </p>
          <p className="transcription-result-meta">
            {summary.modelName} · {formatCreatedAt(summary.createdAt)}
          </p>
        </div>
      )}
      {transcription !== null && (
        <div className="transcription-result-transcription">
          <h4 className="transcription-result-label">Transcription</h4>
          <p className="transcription-result-text">{transcription.fullText}</p>
          <p className="transcription-result-meta">
            {transcription.modelName} · {formatCreatedAt(transcription.createdAt)}
          </p>
        </div>
      )}
    </div>
  )
}
