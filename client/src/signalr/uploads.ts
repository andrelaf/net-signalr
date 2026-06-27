import { HubConnection, Subject } from '@microsoft/signalr'

export interface UploadMetadata {
  uploadId: string
  ticketId: string
  fileName: string
  contentType: string
  totalSize: number
}

const CHUNK_SIZE = 32 * 1024 // 32 KB (< MaximumReceiveMessageSize do servidor)

/**
 * Faz upload de um arquivo via streaming CLIENTE -> SERVIDOR.
 * Cria um Subject e empurra os chunks (Uint8Array); o servidor consome como IAsyncEnumerable<byte[]>.
 * O progresso é reportado de volta pelo servidor no evento 'UploadProgress'.
 */
export async function uploadFile(
  conn: HubConnection,
  ticketId: string,
  file: File,
): Promise<{ uploadId: string; attachmentId: string }> {
  const uploadId = crypto.randomUUID()
  const meta: UploadMetadata = {
    uploadId,
    ticketId,
    fileName: file.name,
    contentType: file.type || 'application/octet-stream',
    totalSize: file.size,
  }

  const subject = new Subject<Uint8Array>()

  // Dispara a invocação com o stream como argumento; aguarda o id do anexo.
  const resultPromise = conn.invoke<string>('UploadAttachment', meta, subject)

  const buffer = await file.arrayBuffer()
  const bytes = new Uint8Array(buffer)
  for (let offset = 0; offset < bytes.length; offset += CHUNK_SIZE) {
    subject.next(bytes.subarray(offset, offset + CHUNK_SIZE))
    // Pequena pausa para o progresso ser visível na demo.
    await new Promise((r) => setTimeout(r, 30))
  }
  subject.complete()

  const attachmentId = await resultPromise
  return { uploadId, attachmentId }
}
