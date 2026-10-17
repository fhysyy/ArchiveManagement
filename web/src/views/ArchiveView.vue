<template>
  <div class="page">
    <div class="crumb" v-if="categoryName">
      门类：<b>{{ categoryName }}</b>
      <el-button link type="primary" @click="router.push('/categories')">‹ 返回门类</el-button>
    </div>

    <div style="margin: 12px 0">
      <el-button type="primary" @click="openCreate">新增档案（案卷）</el-button>
      <el-button @click="showImport = true">批量导入</el-button>
      <el-radio-group v-model="viewMode" style="margin-left: 12px" @change="onViewChange">
        <el-radio-button value="list">列表视图</el-radio-button>
        <el-radio-button value="tree">结构视图（项目 › 案卷 › 文件）</el-radio-button>
      </el-radio-group>
    </div>

    <!-- 结构视图：项目 → 案卷 → 卷内文件 -->
    <el-card v-if="viewMode === 'tree'" shadow="never" v-loading="loadingTree">
      <el-empty v-if="treeRows.length === 0" description="该门类下暂无档案" />
      <el-tree v-else :data="treeRows" :props="treeProps" node-key="id" default-expand-all>
        <template #default="{ data }">
          <span class="tree-node">
            <span>{{ data.label }}</span>
            <el-tag size="small" :type="nodeTagType(data.type)" style="margin-left: 8px">{{ nodeTagLabel(data.type) }}</el-tag>
            <el-tag v-if="data.type === 'archive' || data.type === 'project'" size="small" type="info" style="margin-left: 4px">
              {{ data.fileCount ?? 0 }} 件
            </el-tag>
            <el-tag v-if="data.status" size="small" :type="statusType(data.status)" style="margin-left: 4px">{{ statusLabel(data.status) }}</el-tag>
            <el-button v-if="data.type === 'archive'" link type="primary" style="margin-left: 8px"
              @click.stop="goFiles({ id: data.id, title: data.label } as ArchiveDto)">文件 ›</el-button>
          </span>
        </template>
      </el-tree>
    </el-card>

    <template v-else>
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

    <el-pagination style="margin-top: 12px; justify-content: flex-end"
      v-model:current-page="page" v-model:page-size="size"
      :page-sizes="[10, 20, 50]" :total="total"
      layout="total, sizes, prev, pager, next" @current-change="load" @size-change="load(1)" />
    </template>

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
        <!-- 基建类：项目 = 父档案，案卷挂在父档案下 -->
        <el-form-item label="是否项目">
          <el-switch v-model="form.isProject" active-text="是（项目级）" inactive-text="否（案卷级）" />
        </el-form-item>
        <el-form-item v-if="!form.isProject" label="所属项目">
          <el-select v-model="form.parentId" clearable placeholder="可留空（散卷）" style="width: 100%">
            <el-option v-for="p in projectOptions" :key="p.id" :label="`${p.archiveNo} ${p.title}`" :value="p.id" />
          </el-select>
        </el-form-item>
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

    <!-- 批量导入：Excel 文件 / 粘贴题名 -->
    <el-dialog v-model="showImport" title="批量导入档案" width="620px">
      <el-form label-width="90px">
        <el-form-item label="门类">
          <el-select v-model="importCategoryId" placeholder="请选择门类" style="width: 100%">
            <el-option v-for="c in categories" :key="c.id" :label="`${c.name}（${c.code}）`" :value="c.id" />
          </el-select>
        </el-form-item>
      </el-form>

      <el-tabs v-model="importTab">
        <el-tab-pane label="Excel 导入" name="excel">
          <el-alert type="info" :closable="false" style="margin-bottom: 12px"
            title="表头支持：题名 / 年度 / 保管期限 / 密级 / 归档单位 / 是否项目 / 所属项目档号，其余列按门类元数据字段自动映射" />
          <el-upload drag :auto-upload="false" :limit="1" accept=".xlsx,.xls"
            :on-change="onFileChange" :on-remove="onFileRemove" v-model:file-list="fileList">
            <el-icon class="el-icon--upload"><upload-filled /></el-icon>
            <div class="el-upload__text">拖拽文件到此，或<em>点击选择</em></div>
            <template #tip><div class="el-upload__tip">仅支持 .xlsx / .xls，单个文件不超过 20MB</div></template>
          </el-upload>
          <div style="margin-top: 10px">
            <el-button link type="primary" @click="onDownloadTemplate">下载导入模板（含门类字段）</el-button>
          </div>
        </el-tab-pane>

        <el-tab-pane label="粘贴题名" name="text">
          <el-form label-width="90px">
            <el-form-item label="年度"><el-input v-model.number="importYear" style="width: 160px" /></el-form-item>
            <el-form-item label="题名清单">
              <el-input v-model="importText" type="textarea" :rows="8" placeholder="每行一条档案题名" />
            </el-form-item>
          </el-form>
        </el-tab-pane>
      </el-tabs>

      <template #footer>
        <el-button @click="showImport = false">取消</el-button>
        <el-button type="primary" :loading="importing" @click="onImport">
          {{ importTab === 'excel' ? '开始导入' : '开始导入' }}
        </el-button>
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
import type { ArchiveDto, CreateArchiveDto, CategoryDto, ProjectTreeNode } from '@/types/models'
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
const projectOptions = ref<ArchiveDto[]>([]) // 同门类下可作为父节点的项目级档案
// 列表分页
const page = ref(1)
const size = ref(10)
const total = ref(0)

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

// 视图模式：列表 / 结构树
const viewMode = ref<'list' | 'tree'>('list')
const treeRows = ref<ProjectTreeNode[]>([])
const loadingTree = ref(false)
async function loadProjectTree() {
  if (!form.value.categoryId && !categoryId) return
  const cid = Number(categoryId) || form.value.categoryId
  loadingTree.value = true
  try {
    const { data } = await api.get('/archives/project-tree', { params: { categoryId: cid } })
    treeRows.value = data.data ?? []
  } finally { loadingTree.value = false }
}
function onViewChange(v: string) {
  if ((v as string) === 'tree') loadProjectTree()
  else load()
}
const nodeTagMap: Record<string, { label: string; type: string }> = {
  project: { label: '项目', type: 'warning' },
  archive: { label: '案卷', type: 'success' },
  file: { label: '文件', type: 'info' }
}
function nodeTagLabel(t: string) { return nodeTagMap[t]?.label ?? t }
function nodeTagType(t: string) { return (nodeTagMap[t]?.type ?? 'info') as 'info' | 'success' | 'warning' | 'danger' }

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
const importTab = ref('excel')
const importText = ref('')
const importYear = ref(new Date().getFullYear())
const importCategoryId = ref<number>(0)
const fileList = ref<any[]>([])
const importing = ref(false)

function onFileChange(file: any) { fileList.value = [file] }
function onFileRemove() { fileList.value = [] }

async function onDownloadTemplate() {
  const cid = importCategoryId.value
  if (!cid) { ElMessage.warning('请先选择门类'); return }
  const res = await api.get('/archives/import-template', { params: { categoryId: cid }, responseType: 'blob' })
  const url = URL.createObjectURL(new Blob([res.data]))
  const a = document.createElement('a')
  a.href = url
  a.download = `导入模板.xlsx`
  a.click()
  URL.revokeObjectURL(url)
}

// T13 审核对话框
const showAudit = ref(false)
const auditAction = ref<'approve' | 'reject'>('approve')
const auditComment = ref('')
const curRow = ref<ArchiveDto | null>(null)

async function load(p?: number) {
  // 结构视图下改刷项目树，保证新增/导入/归档后视图同步
  if (viewMode.value === 'tree') { await loadProjectTree(); return }
  if (p) page.value = p
  const params: Record<string, string | number> = { page: page.value, pageSize: size.value }
  if (categoryId) params.categoryId = categoryId
  const { data } = await api.get('/archives', { params })
  rows.value = data.data.items ?? []
  total.value = data.data.total ?? 0
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
  form.value.parentId = null
  await loadTemplate(id)
  await loadProjectOptions(id)
}
// 拉取可作为父节点的项目级档案（IsProject=true）
async function loadProjectOptions(id: number) {
  if (!id) { projectOptions.value = []; return }
  try {
    const { data } = await api.get('/archives', { params: { categoryId: id } })
    const list: ArchiveDto[] = Array.isArray(data.data) ? data.data : (data.data.items ?? [])
    projectOptions.value = list.filter(a => a.isProject)
  } catch { projectOptions.value = [] }
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
  if (!form.value.isProject) {
    // 案卷级：parentId 为空则显式置 null，避免残留上次选择
    form.value.parentId = form.value.parentId ?? null
  } else {
    form.value.parentId = null
  }
  await api.post('/archives', form.value)
  showCreate.value = false
  ElMessage.success('已创建，档号由系统按规则生成')
  await loadProjectOptions(form.value.categoryId)
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

// 批量导入：Excel 走 /import-excel，粘贴文本走 /batch-import
async function onImport() {
  const cid = importCategoryId.value
  if (!cid) { ElMessage.warning('请选择门类'); return }

  if (importTab.value === 'excel') {
    if (fileList.value.length === 0) { ElMessage.warning('请先选择 Excel 文件'); return }
    const fd = new FormData()
    fd.append('file', fileList.value[0].raw)
    fd.append('categoryId', String(cid))
    importing.value = true
    try {
      const { data } = await api.post('/archives/import-excel', fd, {
        headers: { 'Content-Type': 'multipart/form-data' }
      })
      const r = data.data
      const errs = (r.errors ?? []).slice(0, 3).join('；')
      if (r.failed > 0)
        ElMessage.warning(`导入完成：成功 ${r.created} 条，失败 ${r.failed} 条${errs ? '。' + errs : ''}`)
      else
        ElMessage.success(`导入完成：成功 ${r.created} 条`)
      showImport.value = false
      fileList.value = []
      load()
    } finally { importing.value = false }
    return
  }

  const titles = importText.value.split('\n').map(s => s.trim()).filter(Boolean)
  if (titles.length === 0) { ElMessage.warning('请至少填写一条题名'); return }
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
  await loadProjectOptions(form.value.categoryId)
  await load()
})
</script>

<style scoped>
.crumb { margin-bottom: 4px; color: #475569; }
.no-tpl { color: #94a3b8; font-size: 12px; }
</style>
