import api from './request'
import type { CategoryDto, CreateCategoryDto } from '@/types/models'

export const listCategories = () => api.get<{ code: number; data: CategoryDto[] }>('/categories')
export const getCategory = (id: number) => api.get(`/categories/${id}`)
export const createCategory = (dto: CreateCategoryDto) => api.post('/categories', dto)
export const updateCategory = (id: number, dto: CreateCategoryDto) => api.put(`/categories/${id}`, dto)
export const deleteCategory = (id: number) => api.delete(`/categories/${id}`)
