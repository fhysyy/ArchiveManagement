import api from './request'
import type { AttachmentDto, AttachmentUploadResultDto } from '@/types/models'

export const listAttachments = (fileItemId: number) =>
  api.get<{ code: number; data: AttachmentDto[] }>(`/files/${fileItemId}/attachments`)

export const uploadAttachment = (fileItemId: number, file: File) => {
  const fd = new FormData()
  fd.append('file', file)
  return api.post<{ code: number; data: AttachmentUploadResultDto }>(
    `/files/${fileItemId}/attachments`,
    fd,
    { headers: { 'Content-Type': 'multipart/form-data' } }
  )
}

export const deleteAttachment = (id: number) => api.delete(`/attachments/${id}`)

// 下载：拿到 blob 后按原始文件名触发浏览器下载
export async function downloadAttachment(id: number, originalName?: string) {
  const { data } = await api.get(`/attachments/${id}/download`, { responseType: 'blob' })
  const url = window.URL.createObjectURL(data)
  const a = document.createElement('a')
  a.href = url
  a.download = originalName || `attachment-${id}`
  document.body.appendChild(a)
  a.click()
  a.remove()
  window.URL.revokeObjectURL(url)
}
