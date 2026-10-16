import api from './request'
import type { FileItemDto, CreateFileItemDto } from '@/types/models'

export const listFiles = (archiveId: number) =>
  api.get<{ code: number; data: FileItemDto[] }>('/files', { params: { archiveId } })
export const getFile = (id: number) => api.get(`/files/${id}`)
export const createFile = (dto: CreateFileItemDto) => api.post('/files', dto)
export const updateFile = (id: number, dto: CreateFileItemDto) => api.put(`/files/${id}`, dto)
export const deleteFile = (id: number) => api.delete(`/files/${id}`)
