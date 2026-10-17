import api from './request'
import type { FondsDto, SaveFondsDto } from '@/types/models'

export const listFonds = () => api.get<{ code: number; data: FondsDto[] }>('/fonds')
export const getFonds = (id: number) => api.get<{ code: number; data: FondsDto }>(`/fonds/${id}`)
export const saveFonds = (dto: SaveFondsDto) =>
  api.put<{ code: number; data: FondsDto }>('/fonds', dto)
