import { defineStore } from 'pinia'
import { ref } from 'vue'
import api from '@/api/request'
import type { LoginResult } from '@/types/models'

export const useAuthStore = defineStore('auth', () => {
  const token = ref(localStorage.getItem('token') || '')
  const user = ref<LoginResult | null>(null)

  async function login(username: string, password: string) {
    const { data } = await api.post('/auth/login', { username, password })
    token.value = data.data.token
    user.value = data.data
    localStorage.setItem('token', token.value)
    return user.value
  }

  function logout() {
    token.value = ''
    user.value = null
    localStorage.removeItem('token')
  }

  return { token, user, login, logout }
})
