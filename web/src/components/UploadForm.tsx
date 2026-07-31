import { useState } from 'react'
import type { FormEvent } from 'react'
import { FileDropzone } from './FileDropzone'
import './UploadForm.css'

interface UploadSelection {
  file: File | null
  error: string | null
}

const EMPTY_SELECTION: UploadSelection = { file: null, error: null }

export function UploadForm() {
  const [selection, setSelection] = useState<UploadSelection>(EMPTY_SELECTION)

  const handleSelect = (file: File | null, error: string | null) => {
    setSelection({ file, error })
  }

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (selection.file === null) {
      return
    }
  }

  return (
    <form className="upload-form" onSubmit={handleSubmit}>
      <FileDropzone
        file={selection.file}
        error={selection.error}
        onSelect={handleSelect}
      />
      <button
        type="submit"
        className="upload-form-submit"
        aria-label="Upload audio file"
        disabled={selection.file === null}
      >
        Upload
      </button>
    </form>
  )
}
