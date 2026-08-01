import { useEffect, useRef } from 'react'
import type { UploadResponse } from '../types/api'
import { formatBytes } from '../lib/formatBytes'
import { isHttpUrl } from '../lib/isHttpUrl'
import './UploadResult.css'

interface UploadResultProps {
  result: UploadResponse
  originalSize: number
  onReset: () => void
}

export function UploadResult({
  result,
  originalSize,
  onReset,
}: UploadResultProps) {
  const cardRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    cardRef.current?.focus()
  }, [])

  return (
    <div
      ref={cardRef}
      className="upload-result"
      role="status"
      tabIndex={-1}
    >
      <div className="upload-result-heading">
        <svg
          className="upload-result-icon"
          viewBox="0 0 20 20"
          aria-hidden="true"
        >
          <path
            fill="currentColor"
            d="M16.7 5.3a1 1 0 0 1 0 1.4l-7.5 7.5a1 1 0 0 1-1.4 0l-3.5-3.5a1 1 0 1 1 1.4-1.4l2.8 2.8 6.8-6.8a1 1 0 0 1 1.4 0Z"
          />
        </svg>
        <h3 className="upload-result-title">Upload complete</h3>
      </div>
      <p className="upload-result-name">{result.fileName}</p>
      <p className="upload-result-size">
        {formatBytes(originalSize)}
        {result.wasCompressed
          ? ` → ${formatBytes(result.compressedSize)}`
          : ' · No compression needed'}
      </p>
      {isHttpUrl(result.storageUrl) ? (
        <a
          className="upload-result-link"
          href={result.storageUrl}
          target="_blank"
          rel="noreferrer"
        >
          Open file
        </a>
      ) : (
        <p className="upload-result-url">{result.storageUrl}</p>
      )}
      <button type="button" className="upload-result-reset" onClick={onReset}>
        Upload another
      </button>
    </div>
  )
}
