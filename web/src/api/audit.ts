import api from './request'
import type { AuditLogDto, PagedAuditResult } from '@/types/models'

export const listAuditLogs = (from?: string, to?: string, action?: string) =>
  api.get<{ code: number; data: AuditLogDto[] }>('/audit-logs', { params: { from, to, action } })

export const listAuditLogsPaged = (q: {
  from?: string; to?: string; action?: string; page: number; pageSize: number
}) => api.get<{ code: number; data: PagedAuditResult }>('/audit-logs', { params: q })
