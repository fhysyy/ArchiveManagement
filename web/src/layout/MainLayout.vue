<template>
  <el-container style="height: 100vh">
    <el-aside width="200px" style="background: #1e293b">
      <div class="logo">📁 档案管理系统</div>
      <el-menu router :default-active="$route.path" background-color="#1e293b" text-color="#cbd5e1" active-text-color="#fff">
        <el-menu-item index="/fonds">全宗管理</el-menu-item>
        <el-menu-item index="/categories">门类管理</el-menu-item>
        <el-menu-item index="/archives">档案管理</el-menu-item>
        <el-menu-item index="/search">条件检索</el-menu-item>
        <el-menu-item index="/borrow">借阅利用</el-menu-item>
        <el-menu-item index="/appraisal">鉴定销毁</el-menu-item>
        <el-menu-item index="/stats">统计报表</el-menu-item>
        <el-menu-item index="/audit-logs">审计日志</el-menu-item>
        <el-menu-item index="/system">系统管理</el-menu-item>
      </el-menu>
    </el-aside>
    <el-container>
      <el-header style="display: flex; align-items: center; justify-content: flex-end; gap: 12px; background: #fff; border-bottom: 1px solid #e3e8ef">
        <span>欢迎 {{ auth.user?.name }}</span>
        <el-button text @click="onLogout">退出</el-button>
      </el-header>
      <el-main><router-view /></el-main>
    </el-container>
  </el-container>
</template>

<script setup lang="ts">
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/store/auth'

const router = useRouter()
const auth = useAuthStore()

function onLogout() {
  auth.logout()
  router.push('/login')
}
</script>

<style scoped>
.logo { padding: 16px; font-weight: 600; color: #fff; }
</style>
