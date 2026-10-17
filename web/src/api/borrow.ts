import api from './request'
import type { CreateBorrowDto, BorrowDto, BorrowAuditDto, PagedBorrowResult } from '@/types/models'

export const createBorrow = (dto: CreateBorrowDto) =>
  api.post<{ code: number; data: BorrowDto }>('/borrow', dto)

export const listBorrow = (role: string) =>
  api.get<{ code: number; data: BorrowDto[] }>('/borrow', { params: { role } })

export const listBorrowPaged = (role: string, page: number, pageSize: number) =>
  api.get<{ code: number; data: PagedBorrowResult }>('/borrow', {
    params: { role, page, pageSize }
  })

export const approveBorrow = (id: number, dto?: BorrowAuditDto) =>
  api.put(`/borrow/${id}/approve`, dto ?? {})

export const rejectBorrow = (id: number, dto?: BorrowAuditDto) =>
  api.put(`/borrow/${id}/reject`, dto ?? {})

export const returnBorrow = (id: number) =>
  api.put(`/borrow/${id}/return`)

export const renewBorrow = (id: number, days: number) =>
  api.post(`/borrow/${id}/renew`, { days })
