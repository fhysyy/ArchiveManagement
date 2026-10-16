import api from './request'
import type { HealthDto, BackupResultDto, UserDto, CreateUserDto, RoleDto, CreateRoleDto, PermissionDto, SetPermissionsRequest } from '@/types/models'

export const getHealth = () => api.get<{ code: number; data: HealthDto }>('/system/health')
export const triggerBackup = () => api.post<{ code: number; data: BackupResultDto }>('/system/backup')

export const listUsers = () => api.get<{ code: number; data: UserDto[] }>('/users')
export const createUser = (dto: CreateUserDto) => api.post('/users', dto)
export const deleteUser = (id: number) => api.delete(`/users/${id}`)

export const listRoles = () => api.get<{ code: number; data: RoleDto[] }>('/roles')
export const createRole = (dto: CreateRoleDto) => api.post('/roles', dto)
export const deleteRole = (id: number) => api.delete(`/roles/${id}`)

export const getPermissions = (roleId: number) =>
  api.get<{ code: number; data: PermissionDto[] }>(`/roles/${roleId}/permissions`)
export const setPermissions = (roleId: number, req: SetPermissionsRequest) =>
  api.put(`/roles/${roleId}/permissions`, req)
