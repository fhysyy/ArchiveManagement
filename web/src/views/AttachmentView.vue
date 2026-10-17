<template>
  <div class="page">
    <div class="crumb">
      门类：<b>{{ categoryName }}</b> ／ 档案：<b>{{ archiveTitle }}</b> ／ 文件：<b>{{ fileTitle }}</b>
      <el-button link type="primary" @click="router.push({ path: '/files', query: { archiveId, archiveTitle, categoryId, categoryName } })">‹ 返回文件</el-button>
    </div>

    <div style="margin: 12px 0; display: flex; align-items: center; gap: 12px">
      <el-upload
        :auto-upload="false"
        :show-file-list="false"
        :on-change="onPick"
      >
        <el-button type="primary">上传附件</el-button>
      </el-upload>
      <span class="tip">仅存对象 key + SHA256，落盘在本地存储目录</span>
    </div>

    <el-table :data="rows" border>
      <el-table-column prop="originalName" label="文件名" />
      <el-table-column prop="mimeType" label="类型" width="120" />
      <el-table-column label="大小" width="110">
        <template #default="{ row }">{{ formatSize(row.sizeBytes) }}</template>
      </el-table-column>
      <el-table-column prop="hash" label="SHA256" show-overflow-tooltip />
      <el-table-column label="操作" width="230">
        <template #default="{ row }">
          <el-button size="small" @click="onDownload(row)">下载</el-button>
          <el-button size="small" type="primary" plain @click="onPreview(row)">预览</el-button>
          <el-button size="small" type="danger" @click="onDelete(row)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>

    <!-- 带水印的在线预览 -->
    <el-dialog v-model="showPreview" :title="`预览 —— ${previewRow?.originalName ?? ''}`" width="70%">
      <div class="preview-wrap">
        <img v-if="previewRow && isImage(previewRow)" :src="previewUrl(previewRow)" class="preview-img" />
        <iframe v-else-if="previewRow" :src="previewUrl(previewRow)" class="preview-frame" />
        <div class="watermark-layer" v-if="previewRow">
          <span v-for="i in 24" :key="i">{{ watermarkText }}</span>
        </div>
      </div>
      <div class="preview-tip">预览已叠加访客水印（前端 DOM 水印，防随手截屏；强防泄密需服务端渲染水印）</div>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { listAttachments, uploadAttachment, deleteAttachment, downloadAttachment } from '@/api/attachment'
import type { AttachmentDto } from '@/types/models'
import { useAuthStore } from '@/store/auth'
import { ElMessage, ElMessageBox } from 'element-plus'
import type { UploadFile } from 'element-plus'

const auth = useAuthStore()

const route = useRoute()
const router = useRouter()

const fileId = (route.query.fileId as string) || ''
const fileTitle = (route.query.fileTitle as string) || ''
const archiveId = (route.query.archiveId as string) || ''
const archiveTitle = (route.query.archiveTitle as string) || ''
const categoryId = (route.query.categoryId as string) || ''
const categoryName = (route.query.categoryName as string) || ''

const rows = ref<AttachmentDto[]>([])

async function load() {
  if (!fileId) return
  const { data } = await listAttachments(Number(fileId))
  rows.value = data.data
}
async function onPick(upload: UploadFile) {
  const raw = upload.raw
  if (!raw) return
  await uploadAttachment(Number(fileId), raw)
  ElMessage.success('上传成功')
  load()
}
async function onDownload(row: AttachmentDto) {
  await downloadAttachment(row.id, row.originalName)
}
// 在线预览（带访客水印覆盖层）
const showPreview = ref(false)
const previewRow = ref<AttachmentDto | null>(null)
const watermarkText = ref('')
function onPreview(row: AttachmentDto) {
  previewRow.value = row
  const who = auth.user?.name || auth.user?.username || '访客'
  watermarkText.value = `${who} · ${new Date().toLocaleString('zh-CN')}`
  showPreview.value = true
}
function previewUrl(row: AttachmentDto) {
  const t = localStorage.getItem('token')
  return `/api/attachments/${row.id}/preview${t ? `?token=${encodeURIComponent(t)}` : ''}`
}
function isImage(row: AttachmentDto) {
  return (row.mimeType || '').startsWith('image/')
}
async function onDelete(row: AttachmentDto) {
  await ElMessageBox.confirm(`确认删除附件「${row.originalName}」？`, '提示', { type: 'warning' })
  await deleteAttachment(row.id)
  ElMessage.success('已删除')
  load()
}
function formatSize(b?: number) {
  if (!b) return '-'
  return b < 1024 ? `${b} B` : `${(b / 1024).toFixed(1)} KB`
}
onMounted(load)
</script>

<style scoped>
.crumb { margin-bottom: 4px; color: #475569; }
.tip { color: #94a3b8; font-size: 12px; }
.preview-wrap { position: relative; height: 60vh; background: #f1f5f9; overflow: hidden; }
.preview-img { max-width: 100%; max-height: 100%; display: block; margin: 0 auto; }
.preview-frame { width: 100%; height: 100%; border: 0; }
.watermark-layer {
  position: absolute; inset: 0; pointer-events: none;
  display: flex; flex-wrap: wrap; gap: 60px; align-content: space-around; justify-content: space-around;
  transform: rotate(-25deg);
}
.watermark-layer span {
  color: rgba(30, 41, 59, 0.16); font-size: 15px; font-weight: 600; white-space: nowrap;
}
.preview-tip { margin-top: 8px; color: #94a3b8; font-size: 12px; }
</style>
