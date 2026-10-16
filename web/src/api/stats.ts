import api from './request'
import type { StatsOverviewDto, BorrowStatsDto, DueWarningDto } from '@/types/models'

export const statsOverview = () => api.get<{ code: number; data: StatsOverviewDto }>('/stats/overview')
export const statsBorrow = () => api.get<{ code: number; data: BorrowStatsDto }>('/stats/borrow')
export const statsDueWarning = () => api.get<{ code: number; data: DueWarningDto }>('/stats/due-warning')
