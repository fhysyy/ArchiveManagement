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
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { listAttachments, uploadAttachment, deleteAttachment, downloadAttachment } from '@/api/attachment'
import type { AttachmentDto } from '@/types/models'
import { ElMessage, ElMessageBox } from 'element-plus'
import type { UploadFile } from 'element-plus'

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
function onPreview(row: AttachmentDto) {
  // 在线预览：走 inline 预览端点，在新标签页打开
  window.open(`/api/attachments/${row.id}/preview`, '_blank')
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
</style>
