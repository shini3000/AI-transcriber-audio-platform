import type { AudioResult, UploadResponse } from '../types/api'
import type { WorkerInMessage, WorkerOutMessage } from '../types/parallelUpload'

const activePollers = new Map<string, number>()

function postUpdate(message: WorkerOutMessage) {
  self.postMessage(message)
}

function clearPoller(clientId: string) {
  const timerId = activePollers.get(clientId)
  if (timerId !== undefined) {
    self.clearInterval(timerId)
    activePollers.delete(clientId)
  }
}

async function handleStartUpload(
  clientId: string,
  file: File,
  apiBaseUrl: string,
) {
  postUpdate({
    type: 'STATUS_UPDATE',
    clientId,
    status: 'uploading',
  })

  const baseUrl = (apiBaseUrl || '').replace(/\/api\/?$/, '').replace(/\/$/, '')

  try {
    const formData = new FormData()
    formData.append('file', file)

    const uploadRes = await fetch(`${baseUrl}/api/audio/upload`, {
      method: 'POST',
      body: formData,
    })

    if (!uploadRes.ok) {
      let errMsg = `Upload failed with status ${uploadRes.status}`
      try {
        const errorJson = await uploadRes.json()
        if (errorJson?.error) errMsg = errorJson.error
      } catch { }
      postUpdate({
        type: 'STATUS_UPDATE',
        clientId,
        status: 'failed',
        error: errMsg,
      })
      return
    }

    const uploadData: UploadResponse = await uploadRes.json()

    postUpdate({
      type: 'STATUS_UPDATE',
      clientId,
      status: 'processing',
      uploadResponse: uploadData,
    })

    const audioFileId = uploadData.id
    const pollIntervalMs = 2500
    const timeoutMs = 120000
    const startTime = Date.now()

    const timerId = self.setInterval(async () => {
      if (Date.now() - startTime > timeoutMs) {
        clearPoller(clientId)
        postUpdate({
          type: 'STATUS_UPDATE',
          clientId,
          status: 'failed',
          error: 'Processing timed out after 2 minutes.',
        })
        return
      }

      try {
        const resultRes = await fetch(
          `${baseUrl}/api/audio/${audioFileId}/result`,
        )
        if (!resultRes.ok) {
          if (resultRes.status === 404) {
            clearPoller(clientId)
            postUpdate({
              type: 'STATUS_UPDATE',
              clientId,
              status: 'failed',
              error: 'Audio file not found.',
            })
          }
          return
        }

        const resultData: AudioResult = await resultRes.json()

        if (resultData.status === 'ready') {
          clearPoller(clientId)
          postUpdate({
            type: 'STATUS_UPDATE',
            clientId,
            status: 'ready',
            audioResult: resultData,
          })
        } else if (resultData.status === 'rejected') {
          clearPoller(clientId)
          postUpdate({
            type: 'STATUS_UPDATE',
            clientId,
            status: 'rejected',
            audioResult: resultData,
            error:
              resultData.validationErrorMessage ??
              'File was rejected: invalid audio format.',
          })
        }
      } catch (pollErr) {
      }
    }, pollIntervalMs)

    activePollers.set(clientId, timerId)
  } catch (err) {
    postUpdate({
      type: 'STATUS_UPDATE',
      clientId,
      status: 'failed',
      error: err instanceof Error ? err.message : 'Network error during upload.',
    })
  }
}

self.onmessage = (event: MessageEvent<WorkerInMessage>) => {
  const data = event.data
  if (data.type === 'START_UPLOAD') {
    void handleStartUpload(
      data.item.clientId,
      data.item.file,
      data.item.apiBaseUrl,
    )
  } else if (data.type === 'CANCEL_ITEM') {
    clearPoller(data.clientId)
  }
}
