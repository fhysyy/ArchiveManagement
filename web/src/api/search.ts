import api from './request'
import type { SearchArchiveDto, PagedArchiveResult } from '@/types/models'

export const searchArchives = (dto: SearchArchiveDto) =>
  api.post<{ code: number; data: PagedArchiveResult }>('/search', dto)

export const suggest = (q: string) => api.get<{ code: number; data: string[] }>('/search/suggest', { params: { q } })
