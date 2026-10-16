import api from './request'
import type { AuditLogDto } from '@/types/models'

export const listAuditLogs = (from?: string, to?: string) =>
  api.get<{ code: number; data: AuditLogDto[] }>('/audit-logs', { params: { from, to } })
