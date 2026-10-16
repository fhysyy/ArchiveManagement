<template>
  <div class="login">
    <el-card class="box">
      <h2>📁 档案管理系统</h2>
      <el-form label-width="80px" @submit.prevent="onLogin">
        <el-form-item label="用户名">
          <el-input v-model="username" />
        </el-form-item>
        <el-form-item label="密码">
          <el-input v-model="password" type="password" />
        </el-form-item>
        <el-button type="primary" :loading="loading" @click="onLogin">登录</el-button>
      </el-form>
      <p class="tip">演示账号：admin / admin123</p>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/store/auth'
import { ElMessage } from 'element-plus'

const username = ref('admin')
const password = ref('admin123')
const loading = ref(false)
const router = useRouter()
const auth = useAuthStore()

async function onLogin() {
  loading.value = true
  try {
    await auth.login(username.value, password.value)
    router.push('/')
  } catch {
    ElMessage.error('登录失败，请检查账号密码')
  } finally {
    loading.value = false
  }
}
</script>

<style scoped>
.login { height: 100vh; display: flex; align-items: center; justify-content: center; background: #f5f7fa; }
.box { width: 360px; }
.tip { margin-top: 12px; color: #6b7280; font-size: 12px; }
</style>
