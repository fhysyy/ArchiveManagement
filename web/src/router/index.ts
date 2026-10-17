import { createRouter, createWebHistory } from 'vue-router'
import LoginView from '@/views/LoginView.vue'
import MainLayout from '@/layout/MainLayout.vue'
import CategoryView from '@/views/CategoryView.vue'
import FondsView from '@/views/FondsView.vue'
import ArchiveView from '@/views/ArchiveView.vue'
import FileView from '@/views/FileView.vue'
import AttachmentView from '@/views/AttachmentView.vue'
import SearchView from '@/views/SearchView.vue'
import BorrowView from '@/views/BorrowView.vue'
import AppraisalView from '@/views/AppraisalView.vue'
import StatsView from '@/views/StatsView.vue'
import AuditView from '@/views/AuditView.vue'
import SystemView from '@/views/SystemView.vue'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/login', name: 'login', component: LoginView },
    {
      path: '/',
      component: MainLayout,
      meta: { requiresAuth: true },
      children: [
        { path: '', redirect: '/categories' },
        { path: 'fonds', name: 'fonds', component: FondsView },
        { path: 'categories', name: 'categories', component: CategoryView },
        { path: 'archives', name: 'archives', component: ArchiveView },
        { path: 'files', name: 'files', component: FileView },
        { path: 'attachments', name: 'attachments', component: AttachmentView },
        { path: 'search', name: 'search', component: SearchView },
        { path: 'borrow', name: 'borrow', component: BorrowView },
        { path: 'appraisal', name: 'appraisal', component: AppraisalView },
        { path: 'stats', name: 'stats', component: StatsView },
        { path: 'audit-logs', name: 'audit-logs', component: AuditView },
        { path: 'system', name: 'system', component: SystemView }
      ]
    }
  ]
})

// 路由守卫：未登录跳登录页
router.beforeEach((to) => {
  const token = localStorage.getItem('token')
  if (to.meta.requiresAuth && !token) return '/login'
  if (to.path === '/login' && token) return '/'
  return true
})

export default router
