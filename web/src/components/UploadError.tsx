import './UploadError.css'

interface UploadErrorProps {
  message: string | null
}

const FALLBACK_MESSAGE = 'Upload failed. Please try again.'

export function UploadError({ message }: UploadErrorProps) {
  const displayMessage =
    message !== null && message.trim() !== '' ? message : FALLBACK_MESSAGE

  return (
    <div className="upload-error" role="alert">
      <svg className="upload-error-icon" viewBox="0 0 20 20" aria-hidden="true">
        <path
          fill="currentColor"
          d="M10 1a9 9 0 1 0 0 18 9 9 0 0 0 0-18Zm0 16a7 7 0 1 1 0-14 7 7 0 0 1 0 14Z"
        />
        <path
          fill="currentColor"
          d="M10 5a1 1 0 0 1 1 1v4.25a1 1 0 1 1-2 0V6a1 1 0 0 1 1-1Z"
        />
        <circle cx="10" cy="13.75" r="1.25" fill="currentColor" />
      </svg>
      <p className="upload-error-message">{displayMessage}</p>
    </div>
  )
}
