<template>
  <div class="page">
    <div class="crumb">
      门类：<b>{{ categoryName }}</b> ／ 档案：<b>{{ archiveTitle }}</b>
      <el-button link type="primary" @click="router.push({ path: '/archives', query: { categoryId, categoryName } })">‹ 返回档案</el-button>
    </div>

    <div style="margin: 12px 0">
      <el-button type="primary" @click="openCreate">新增文件（件）</el-button>
      <el-button @click="openCatalog">卷内目录（打印/导出）</el-button>
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

    <!-- 卷内文件目录：可打印 / 导出 CSV -->
    <el-dialog v-model="showCatalog" title="卷内文件目录" width="900px">
      <div id="catalog-area">
        <div class="cat-title">卷 内 文 件 目 录</div>
        <div class="cat-meta">
          <span>档号：{{ archiveNo || '-' }}</span>
          <span>案卷题名：{{ archiveTitle || '-' }}</span>
          <span>门类：{{ categoryName || '-' }}</span>
        </div>
        <table class="cat-table">
          <thead>
            <tr>
              <th style="width: 56px">序号</th>
              <th style="width: 130px">文号</th>
              <th style="width: 110px">责任者</th>
              <th>题名</th>
              <th style="width: 100px">日期</th>
              <th style="width: 60px">页号</th>
              <th style="width: 90px">备注</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(f, i) in rows" :key="f.id">
              <td class="c">{{ f.seqNo ?? i + 1 }}</td>
              <td>{{ f.fileNo || '' }}</td>
              <td>{{ f.author || '' }}</td>
              <td>{{ f.title }}</td>
              <td>{{ f.docDate || '' }}</td>
              <td class="c">{{ f.pages ?? '' }}</td>
              <td></td>
            </tr>
            <tr v-if="rows.length === 0"><td colspan="7" class="c">（暂无卷内文件）</td></tr>
          </tbody>
          <tfoot>
            <tr>
              <td colspan="5" class="c">合计</td>
              <td class="c">{{ totalPages }}</td>
              <td class="c">{{ rows.length }} 件</td>
            </tr>
          </tfoot>
        </table>
      </div>
      <template #footer>
        <el-button @click="showCatalog = false">关闭</el-button>
        <el-button @click="onExportCsv">导出 CSV</el-button>
        <el-button type="primary" @click="onPrint">打印</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import api from '@/api/request'
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
// ===== 卷内文件目录：打印 / 导出 =====
const showCatalog = ref(false)
const archiveNo = ref('')
const totalPages = computed(() => rows.value.reduce((s, f) => s + (f.pages ?? 0), 0))

async function openCatalog() {
  await load() // 保证取到最新件
  try {
    const { data } = await api.get(`/archives/${archiveId}`)
    archiveNo.value = data.data?.archiveNo ?? ''
  } catch { archiveNo.value = '' }
  showCatalog.value = true
}

function catalogRows(): string[][] {
  return rows.value.map((f, i) => [
    String(f.seqNo ?? i + 1), f.fileNo ?? '', f.author ?? '', f.title ?? '',
    f.docDate ?? '', String(f.pages ?? ''), ''
  ])
}

function onExportCsv() {
  const head = ['序号', '文号', '责任者', '题名', '日期', '页号', '备注']
  const esc = (s: string) => `"${String(s).replace(/"/g, '""')}"`
  const lines = [head, ...catalogRows()].map(r => r.map(esc).join(','))
  const csv = '﻿' + lines.join('\r\n') // BOM，避免 Excel 打开乱码
  const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' })
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `卷内目录_${archiveNo.value || archiveId}.csv`
  a.click()
  URL.revokeObjectURL(url)
  ElMessage.success('已导出 CSV')
}

function onPrint() {
  const el = document.getElementById('catalog-area')
  if (!el) return
  const w = window.open('', '_blank', 'width=900,height=700')
  if (!w) { ElMessage.warning('浏览器阻止了弹出窗口，请允许后重试'); return }
  w.document.write(`<!DOCTYPE html><html><head><meta charset="utf-8"><title>卷内文件目录 ${archiveNo.value}</title>
<style>
  body{font-family:"SimSun","宋体",serif;margin:24px;color:#000}
  .cat-title{text-align:center;font-size:20px;font-weight:700;letter-spacing:4px;margin-bottom:10px}
  .cat-meta{display:flex;justify-content:space-between;font-size:12px;margin-bottom:8px}
  table{border-collapse:collapse;width:100%;font-size:12px}
  th,td{border:1px solid #000;padding:5px 6px;vertical-align:top}
  th{background:#f2f2f2;font-weight:600;text-align:center}
  td.c{text-align:center}
  tfoot td{font-weight:600}
  @page{size:A4 portrait;margin:16mm}
</style></head><body>${el.innerHTML}</body></html>`)
  w.document.close()
  w.focus()
  w.print()
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

<style>
/* 卷内目录（dialog 内容被 teleport 到 body，故用非 scoped 样式） */
#catalog-area .cat-title { text-align: center; font-size: 18px; font-weight: 700; letter-spacing: 4px; margin-bottom: 10px; }
#catalog-area .cat-meta { display: flex; justify-content: space-between; font-size: 12px; color: #475569; margin-bottom: 8px; }
#catalog-area .cat-table { border-collapse: collapse; width: 100%; font-size: 12px; }
#catalog-area .cat-table th,
#catalog-area .cat-table td { border: 1px solid #d0d7de; padding: 5px 6px; vertical-align: top; }
#catalog-area .cat-table th { background: #f5f7fa; font-weight: 600; text-align: center; }
#catalog-area .cat-table td.c { text-align: center; }
#catalog-area .cat-table tfoot td { font-weight: 600; background: #fafafa; text-align: center; }
</style>
