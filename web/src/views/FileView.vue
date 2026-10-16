<template>
  <div class="page">
    <div class="crumb">
      门类：<b>{{ categoryName }}</b> ／ 档案：<b>{{ archiveTitle }}</b>
      <el-button link type="primary" @click="router.push({ path: '/archives', query: { categoryId, categoryName } })">‹ 返回档案</el-button>
    </div>

    <div style="margin: 12px 0">
      <el-button type="primary" @click="openCreate">新增文件（件）</el-button>
    </div>

    <el-table :data="rows" border>
      <el-table-column prop="title" label="题名" />
      <el-table-column prop="author" label="作者" width="120" />
      <el-table-column prop="docDate" label="日期" width="120" />
      <el-table-column prop="pages" label="页数" width="70" />
      <el-table-column label="操作" width="200">
        <template #default="{ row }">
          <el-button size="small" @click="openEdit(row)">编辑</el-button>
          <el-button size="small" type="danger" @click="onDelete(row)">删除</el-button>
          <el-button size="small" @click="goAttachments(row)">附件 ›</el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-dialog v-model="showForm" :title="editingId ? '编辑文件' : '新增文件'" width="640px">
      <el-form label-width="100px">
        <el-form-item label="题名"><el-input v-model="form.title" /></el-form-item>
        <el-form-item label="作者"><el-input v-model="form.author" /></el-form-item>
        <el-form-item label="日期"><el-input v-model="form.docDate" placeholder="YYYY-MM-DD" /></el-form-item>
        <el-form-item label="页数"><el-input v-model.number="form.pages" /></el-form-item>
        <el-form-item label="文号"><el-input v-model="form.fileNo" /></el-form-item>
        <!-- 动态元数据：取自门类的「文件级模板」 -->
        <template v-if="template">
          <el-divider content-position="left">
            <span class="divider-text">门类扩展字段（{{ categoryName || '当前门类' }} · 文件级）</span>
          </el-divider>
          <DynamicForm :template="template" v-model="dynamic" />
        </template>
        <div v-else class="no-tpl">该门类暂无文件级元数据模板</div>
      </el-form>
      <template #footer>
        <el-button @click="showForm = false">取消</el-button>
        <el-button type="primary" @click="onSubmit">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { listFiles, createFile, updateFile, deleteFile } from '@/api/file'
import { getCategory } from '@/api/category'
import DynamicForm from '@/components/DynamicForm.vue'
import type { FileItemDto, CreateFileItemDto } from '@/types/models'
import { ElMessage, ElMessageBox } from 'element-plus'

const route = useRoute()
const router = useRouter()

const archiveId = (route.query.archiveId as string) || ''
const archiveTitle = (route.query.archiveTitle as string) || ''
const categoryId = (route.query.categoryId as string) || ''
const categoryName = (route.query.categoryName as string) || ''

const rows = ref<FileItemDto[]>([])
const showForm = ref(false)
const editingId = ref<number | null>(null)
const template = ref('')
const dynamic = ref<Record<string, unknown>>({})
const form = ref<CreateFileItemDto>({
  archiveId: Number(archiveId), title: '', docDate: '', author: '', pages: undefined, fileNo: '', dynamicFields: '{}'
})

async function load() {
  if (!archiveId) return
  const { data } = await listFiles(Number(archiveId))
  rows.value = data.data
}
async function loadTemplate() {
  if (!categoryId) { template.value = ''; return }
  const { data } = await getCategory(Number(categoryId))
  const c = data.data
  // 优先用「文件级模板」；老门类该列为空时回退到案卷级模板，保证兼容
  template.value = (c.fileTemplateJson && c.fileTemplateJson !== '[]')
    ? c.fileTemplateJson
    : (c.templateJson || '[]')
}
function openCreate() {
  editingId.value = null
  dynamic.value = {}
  form.value = {
    archiveId: Number(archiveId), title: '', docDate: '', author: '', pages: undefined, fileNo: '', dynamicFields: '{}'
  }
  showForm.value = true
}
function openEdit(row: FileItemDto) {
  editingId.value = row.id
  form.value = {
    archiveId: row.archiveId, title: row.title, docDate: row.docDate, author: row.author,
    pages: row.pages, fileNo: row.fileNo, dynamicFields: row.dynamicFields || '{}'
  }
  try { dynamic.value = JSON.parse(row.dynamicFields || '{}') } catch { dynamic.value = {} }
  showForm.value = true
}
async function onSubmit() {
  form.value.dynamicFields = JSON.stringify(dynamic.value)
  if (editingId.value) await updateFile(editingId.value, form.value)
  else await createFile(form.value)
  showForm.value = false
  ElMessage.success('已保存')
  load()
}
async function onDelete(row: FileItemDto) {
  await ElMessageBox.confirm(`确认删除文件「${row.title}」？`, '提示', { type: 'warning' })
  await deleteFile(row.id)
  ElMessage.success('已删除')
  load()
}
function goAttachments(row: FileItemDto) {
  router.push({
    path: '/attachments',
    query: {
      fileId: String(row.id), fileTitle: row.title,
      archiveId, archiveTitle, categoryId, categoryName
    }
  })
}
onMounted(() => { loadTemplate(); load() })
</script>

<style scoped>
.crumb { margin-bottom: 4px; color: #475569; }
.no-tpl { color: #94a3b8; font-size: 12px; }
.divider-text { color: #475569; font-size: 12px; }
</style>
