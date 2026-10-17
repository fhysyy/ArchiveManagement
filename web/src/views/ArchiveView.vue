<template>
  <div class="page">
    <div class="crumb" v-if="categoryName">
      门类：<b>{{ categoryName }}</b>
      <el-button link type="primary" @click="router.push('/categories')">‹ 返回门类</el-button>
    </div>

    <div style="margin: 12px 0">
      <el-button type="primary" @click="openCreate">新增档案（案卷）</el-button>
      <el-button @click="showImport = true">批量导入</el-button>
    </div>

    <el-table :data="rows" border>
      <el-table-column prop="archiveNo" label="档号" width="170" />
      <el-table-column prop="title" label="题名" />
      <el-table-column prop="status" label="状态" width="90">
        <template #default="{ row }"><el-tag size="small" :type="statusType(row.status)">{{ statusLabel(row.status) }}</el-tag></template>
      </el-table-column>
      <el-table-column label="操作" width="230">
        <template #default="{ row }">
          <el-button size="small" @click="goFiles(row)">文件 ›</el-button>
          <el-button size="small" type="info" plain @click="openTree(row)">三级树</el-button>
          <el-button v-if="row.status === 'Draft'" size="small" type="primary" plain @click="onSubmit(row)">提交归档</el-button>
          <template v-else-if="row.status === 'Submitted'">
            <el-button size="small" type="success" plain @click="openAudit(row, 'approve')">通过</el-button>
            <el-button size="small" type="danger" plain @click="openAudit(row, 'reject')">退回</el-button>
          </template>
          <span v-else>-</span>
        </template>
      </el-table-column>
    </el-table>

    <el-dialog v-model="showCreate" title="新增档案（展示动态元数据）" width="640px">
      <el-form label-width="100px">
        <el-form-item label="门类">
          <el-select
            v-model="form.categoryId"
            placeholder="请选择门类"
            style="width: 100%"
            @change="onCategoryChange"
          >
            <el-option v-for="c in categories" :key="c.id" :label="`${c.name}（${c.code}）`" :value="c.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="题名"><el-input v-model="form.title" /></el-form-item>
        <el-form-item label="年度"><el-input v-model.number="form.year" /></el-form-item>
        <!-- 动态元数据表单：根据所选门类 template_json 渲染 -->
        <DynamicForm v-if="template" :template="template" v-model="dynamic" />
        <div v-else class="no-tpl">该门类暂无元数据模板</div>
      </el-form>
      <template #footer>
        <el-button @click="showCreate = false">取消</el-button>
        <el-button type="primary" @click="onCreate">保存</el-button>
      </template>
    </el-dialog>

    <!-- 三级树：档案 → 文件 → 附件 -->
    <el-dialog v-model="showTree" :title="`三级结构 —— ${treeTitle}`" width="620px">
      <el-tree :data="treeData" :props="treeProps" default-expand-all node-key="id">
        <template #default="{ data }">
          <span>{{ data.label }}</span>
          <el-tag v-if="data.type" size="small" style="margin-left: 8px">{{ data.type }}</el-tag>
        </template>
      </el-tree>
      <template #footer><el-button @click="showTree = false">关闭</el-button></template>
    </el-dialog>

    <!-- 批量导入：一行一条题名 -->
    <el-dialog v-model="showImport" title="批量导入档案" width="520px">
      <el-form label-width="90px">
        <el-form-item label="门类">
          <el-select v-model="importCategoryId" placeholder="请选择门类" style="width: 100%">
            <el-option v-for="c in categories" :key="c.id" :label="`${c.name}（${c.code}）`" :value="c.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="年度"><el-input v-model.number="importYear" style="width: 160px" /></el-form-item>
        <el-form-item label="题名清单">
          <el-input v-model="importText" type="textarea" :rows="8" placeholder="每行一条档案题名" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showImport = false">取消</el-button>
        <el-button type="primary" @click="onImport">开始导入</el-button>
      </template>
    </el-dialog>

    <!-- T13 审核对话框 -->
    <el-dialog v-model="showAudit" :title="auditAction === 'approve' ? '审核通过' : '退回'" width="420px">
      <el-form label-width="80px">
        <el-form-item label="意见"><el-input v-model="auditComment" type="textarea" :rows="3" :placeholder="auditAction === 'reject' ? '退回理由' : '可选'" /></el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showAudit = false">取消</el-button>
        <el-button :type="auditAction === 'approve' ? 'success' : 'danger'" @click="onAudit">确定</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import api from '@/api/request'
import { getCategory, listCategories } from '@/api/category'
import DynamicForm from '@/components/DynamicForm.vue'
import type { ArchiveDto, CreateArchiveDto, CategoryDto } from '@/types/models'
import { ElMessage } from 'element-plus'

const route = useRoute()
const router = useRouter()

const rows = ref<ArchiveDto[]>([])
const showCreate = ref(false)
const template = ref('')
const categories = ref<CategoryDto[]>([])
const dynamic = ref<Record<string, unknown>>({})
const categoryId = (route.query.categoryId as string) || ''
const categoryName = (route.query.categoryName as string) || ''
const form = ref<CreateArchiveDto>({ categoryId: 0, title: '', year: 2026, dynamicFields: '{}' })

// 状态中文标签 / 标签类型
const statusMap: Record<string, { label: string; type: string }> = {
  Draft: { label: '草稿', type: 'info' },
  Submitted: { label: '待审核', type: 'warning' },
  Archived: { label: '已归档', type: 'success' },
  Rejected: { label: '退回', type: 'danger' },
  Destroyed: { label: '已销毁', type: 'danger' }
}
function statusLabel(s: string) { return statusMap[s]?.label ?? s }
function statusType(s: string) { return (statusMap[s]?.type ?? '') as '' | 'info' | 'success' | 'warning' | 'danger' }

// 三级树
const showTree = ref(false)
const treeTitle = ref('')
const treeData = ref<any[]>([])
const treeProps = { children: 'children', label: 'label' }
async function openTree(row: ArchiveDto) {
  treeTitle.value = row.title
  const { data } = await api.get(`/archives/${row.id}/tree`)
  const t = data.data
  treeData.value = [{
    label: `${t.archive?.archiveNo ?? ''} ${t.archive?.title ?? ''}`,
    type: '档案',
    children: (t.files ?? []).map((f: any) => ({
      label: `${f.file?.seqNo ?? '-'}. ${f.file?.title ?? ''}`,
      type: '文件',
      children: (f.attachments ?? []).map((a: any) => ({
        label: `${a.originalName ?? ''}（${formatSize(a.sizeBytes)}）`,
        type: '附件'
      }))
    }))
  }]
  showTree.value = true
}
function formatSize(b?: number) {
  if (!b) return '-'
  return b < 1024 ? `${b} B` : b < 1024 * 1024 ? `${(b / 1024).toFixed(1)} KB` : `${(b / 1024 / 1024).toFixed(1)} MB`
}

// 批量导入
const showImport = ref(false)
const importText = ref('')
const importYear = ref(new Date().getFullYear())
const importCategoryId = ref<number>(0)

// T13 审核对话框
const showAudit = ref(false)
const auditAction = ref<'approve' | 'reject'>('approve')
const auditComment = ref('')
const curRow = ref<ArchiveDto | null>(null)

async function load() {
  const params: Record<string, string> = {}
  if (categoryId) params.categoryId = categoryId
  const { data } = await api.get('/archives', { params })
  rows.value = data.data
}
// 加载门类下拉
async function loadCategories() {
  const { data } = await listCategories()
  categories.value = data.data
  // 默认门类：优先取路由带入的门类，否则取列表第一个
  const def = categoryId ? Number(categoryId) : (data.data[0]?.id ?? 0)
  form.value.categoryId = def
  importCategoryId.value = def
}
// 切换门类 → 重新拉取该门类的元数据模板，并清空已填的动态字段
async function onCategoryChange(id: number) {
  dynamic.value = {}
  await loadTemplate(id)
}
async function loadTemplate(id: number) {
  if (!id) { template.value = ''; return }
  const { data } = await getCategory(id)
  template.value = data.data.templateJson || '[]'
}
function openCreate() {
  dynamic.value = {}
  showCreate.value = true
  loadTemplate(form.value.categoryId)
}
async function onCreate() {
  form.value.dynamicFields = JSON.stringify(dynamic.value)
  await api.post('/archives', form.value)
  showCreate.value = false
  ElMessage.success('已创建，档号由系统按规则生成')
  load()
}
function goFiles(row: ArchiveDto) {
  router.push({
    path: '/files',
    query: {
      archiveId: String(row.id),
      archiveTitle: row.title,
      categoryId: categoryId || String(row.categoryId),
      categoryName: categoryName || ''
    }
  })
}

// 批量导入
async function onImport() {
  const titles = importText.value.split('\n').map(s => s.trim()).filter(Boolean)
  if (titles.length === 0) { ElMessage.warning('请至少填写一条题名'); return }
  const cid = importCategoryId.value
  if (!cid) { ElMessage.warning('请选择门类'); return }
  const { data } = await api.post('/archives/batch-import', {
    categoryId: cid,
    items: titles.map(t => ({ categoryId: cid, title: t, year: importYear.value, dynamicFields: '{}' }))
  })
  showImport.value = false
  importText.value = ''
  ElMessage.success(`导入完成：成功 ${data.data.created} 条，失败 ${data.data.failed} 条`)
  load()
}

// T13 提交归档 / 审核
async function onSubmit(row: ArchiveDto) {
  await api.post(`/archives/${row.id}/submit`)
  ElMessage.success('已提交，进入待审核')
  load()
}
function openAudit(row: ArchiveDto, action: 'approve' | 'reject') {
  curRow.value = row
  auditAction.value = action
  auditComment.value = ''
  showAudit.value = true
}
async function onAudit() {
  if (!curRow.value) return
  await api.post(`/archives/${curRow.value.id}/audit`, { action: auditAction.value, comment: auditComment.value })
  showAudit.value = false
  ElMessage.success(auditAction.value === 'approve' ? '已通过归档' : '已退回')
  load()
}
onMounted(async () => {
  await loadCategories()
  await load()
})
</script>

<style scoped>
.crumb { margin-bottom: 4px; color: #475569; }
.no-tpl { color: #94a3b8; font-size: 12px; }
</style>
