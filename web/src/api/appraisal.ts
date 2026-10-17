import api from './request'
import type {
  CreateAppraisalDto, AppraisalDto, AppraisalAuditDto, DueArchiveDto,
  PagedAppraisalResult, PagedDueResult
} from '@/types/models'

export const listDue = (excludeProposed = false) =>
  api.get<{ code: number; data: DueArchiveDto[] }>('/appraisal/due', { params: { excludeProposed } })

export const listDuePaged = (page: number, pageSize: number, excludeProposed = false) =>
  api.get<{ code: number; data: PagedDueResult }>('/appraisal/due', {
    params: { page, pageSize, excludeProposed }
  })

export const createAppraisal = (dto: CreateAppraisalDto) =>
  api.post<{ code: number; data: AppraisalDto }>('/appraisal', dto)

export const listAppraisal = () => api.get<{ code: number; data: AppraisalDto[] }>('/appraisal')

export const listAppraisalPaged = (page: number, pageSize: number, status?: string) =>
  api.get<{ code: number; data: PagedAppraisalResult }>('/appraisal', {
    params: { page, pageSize, status: status || undefined }
  })

export const approveAppraisal = (id: number, dto: AppraisalAuditDto) =>
  api.put(`/appraisal/${id}/approve`, dto)
export const executeAppraisal = (id: number) => api.put(`/appraisal/${id}/execute`)
