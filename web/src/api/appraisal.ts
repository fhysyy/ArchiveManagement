import api from './request'
import type { CreateAppraisalDto, AppraisalDto, AppraisalAuditDto, DueArchiveDto } from '@/types/models'

export const listDue = () => api.get<{ code: number; data: DueArchiveDto[] }>('/appraisal/due')
export const createAppraisal = (dto: CreateAppraisalDto) =>
  api.post<{ code: number; data: AppraisalDto }>('/appraisal', dto)
export const listAppraisal = () => api.get<{ code: number; data: AppraisalDto[] }>('/appraisal')
export const approveAppraisal = (id: number, dto: AppraisalAuditDto) =>
  api.put(`/appraisal/${id}/approve`, dto)
export const executeAppraisal = (id: number) => api.put(`/appraisal/${id}/execute`)
