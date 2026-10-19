import { defineStore } from 'pinia'
import { ref } from 'vue'
import api from '@/api/request'
import type { LoginResult } from '@/types/models'

const USER_KEY = 'user'

function readUser(): LoginResult | null {
  try {
    const raw = localStorage.getItem(USER_KEY)
    return raw ? (JSON.parse(raw) as LoginResult) : null
  } catch { return null }
}

export const useAuthStore = defineStore('auth', () => {
  const token = ref(localStorage.getItem('token') || '')
  // 刷新页面后仍保留角色信息，供菜单按权限显示
  const user = ref<LoginResult | null>(readUser())

  async function login(username: string, password: string) {
    const { data } = await api.post('/auth/login', { username, password })
    token.value = data.data.token
    user.value = data.data
    localStorage.setItem('token', token.value)
    localStorage.setItem(USER_KEY, JSON.stringify(data.data))
    return user.value
  }

  function logout() {
    token.value = ''
    user.value = null
    localStorage.removeItem('token')
    localStorage.removeItem(USER_KEY)
  }

  return { token, user, login, logout }
})
