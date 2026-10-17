import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import { fileURLToPath, URL } from 'node:url'

export default defineConfig({
  plugins: [vue()],
  resolve: {
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) }
  },
  build: {
    chunkSizeWarningLimit: 1000,
    rollupOptions: {
      output: {
        // 拆分大依赖，避免单包 1MB+
        manualChunks: {
          vue: ['vue', 'vue-router', 'pinia'],
          'element-plus': ['element-plus', '@element-plus/icons-vue'],
          axios: ['axios']
        }
      }
    }
  },
  server: {
    port: 5173,
    proxy: {
      // 开发期把 /api 代理到后端。后端 http 端口见 launchSettings.json 的 http 配置（当前为 5246）
      '/api': 'http://localhost:5246'
    }
  }
})
