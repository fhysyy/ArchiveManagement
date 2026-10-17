<template>
  <div class="page">
    <h3 style="margin-top: 0">条件检索</h3>

    <el-card shadow="never" style="margin-bottom: 12px">
      <el-form :inline="true" :model="q" @submit.prevent>
        <el-form-item label="门类">
          <el-select v-model="q.categoryId" placeholder="全部" clearable style="width: 160px">
            <el-option v-for="c in categories" :key="c.id" :label="c.name" :value="c.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="年度">
          <el-input v-model.number="q.yearStart" placeholder="起" style="width: 90px" />
          <span style="margin: 0 6px">~</span>
          <el-input v-model.number="q.yearEnd" placeholder="止" style="width: 90px" />
        </el-form-item>
        <el-form-item label="题名">
          <el-autocomplete
            v-model="q.title"
            :fetch-suggestions="querySuggest"
            placeholder="关键词"
            style="width: 180px"
            :trigger-on-focus="false" />
        </el-form-item>
        <el-form-item label="责任者">
          <el-input v-model="q.author" placeholder="归档单位/著录" style="width: 140px" />
        </el-form-item>
        <el-form-item label="保管期限">
          <el-select v-model="q.retention" placeholder="全部" clearable style="width: 120px">
            <el-option v-for="r in retentionOpts" :key="r" :label="r" :value="r" />
          </el-select>
        </el-form-item>
        <el-form-item label="密级">
          <el-select v-model="q.secrecyLevel" placeholder="全部" clearable style="width: 120px">
            <el-option v-for="s in secrecyOpts" :key="s" :label="s" :value="s" />
          </el-select>
        </el-form-item>
        <el-form-item label="状态">
          <el-select v-model="q.status" placeholder="全部" clearable style="width: 120px">
            <el-option v-for="s in statusOpts" :key="s" :label="s" :value="s" />
          </el-select>
        </el-form-item>
        <el-form-item>
          <el-button type="primary" @click="onSearch">检索</el-button>
          <el-button @click="onReset">重置</el-button>
        </el-form-item>
      </el-form>
    </el-card>

    <el-table :data="rows" border v-loading="loading">
      <el-table-column prop="archiveNo" label="档号" width="170" />
      <el-table-column prop="title" label="题名" />
      <el-table-column prop="year" label="年度" width="80" />
      <el-table-column prop="retention" label="保管期限" width="100" />
        <el-table-column prop="secrecyLevel" label="密级" width="90" />
      <!-- 门类自定义字段列：按所选门类模板动态渲染 -->
      <el-table-column
        v-for="f in metaColumns"
        :key="f.key"
        :label="f.label"
        :prop="`__meta_${f.key}`"
        min-width="120"
        show-overflow-tooltip />
        <el-table-column prop="status" label="状态" width="90">
          <template #default="{ row }"><el-tag size="small" :type="statusType(row.status)">{{ statusLabel(row.status) }}</el-tag></template>
        </el-table-column>
      <el-table-column label="操作" width="100">
        <template #default="{ row }">
          <el-button size="small" type="primary" plain @click="openBorrow(row)">借阅</el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-pagination
      style="margin-top: 12px; justify-content: flex-end"
      layout="total, prev, pager, next"
      :total="total"
      :page-size="q.pageSize"
      :current-page="q.page"
      @current-change="onPage" />

    <!-- 借阅申请对话框 -->
    <el-dialog v-model="showBorrow" title="发起借阅申请" width="460px">
      <el-descriptions :column="1" border size="small">
        <el-descriptions-item label="档号">{{ cur?.archiveNo }}</el-descriptions-item>
        <el-descriptions-item label="题名">{{ cur?.title }}</el-descriptions-item>
      </el-descriptions>
      <el-form label-width="90px" style="margin-top: 12px">
        <el-form-item label="借阅天数"><el-input v-model.number="borrowForm.days" /></el-form-item>
        <el-form-item label="事由"><el-input v-model="borrowForm.reason" type="textarea" :rows="3" /></el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showBorrow = false">取消</el-button>
        <el-button type="primary" @click="onBorrow">提交申请</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, watch } from 'vue'
import api from '@/api/request'
import { searchArchives, suggest } from '@/api/search'
import { createBorrow } from '@/api/borrow'
import { listCategories } from '@/api/category'
import type { ArchiveDto, CategoryDto, SearchArchiveDto } from '@/types/models'
import { ElMessage } from 'element-plus'

const categories = ref<CategoryDto[]>([])
const rows = ref<ArchiveDto[]>([])
const total = ref(0)
const loading = ref(false)
const retentionOpts = ['永久', '30年', '10年']
const secrecyOpts = ['公开', '内部', '秘密', '机密', '绝密']
const statusOpts = ['Draft', 'Submitted', 'Archived', 'Rejected', 'Destroyed']

const statusMap: Record<string, { label: string; type: string }> = {
  Draft: { label: '草稿', type: 'info' },
  Submitted: { label: '待审核', type: 'warning' },
  Archived: { label: '已归档', type: 'success' },
  Rejected: { label: '退回', type: 'danger' },
  Destroyed: { label: '已销毁', type: 'danger' }
}
function statusLabel(s: string) { return statusMap[s]?.label ?? s }
function statusType(s: string) { return (statusMap[s]?.type ?? '') as '' | 'info' | 'success' | 'warning' | 'danger' }

const q = reactive<SearchArchiveDto & { page: number; pageSize: number }>({
  page: 1, pageSize: 20, sortBy: 'Id', sortDir: 'desc'
})

const showBorrow = ref(false)
const cur = ref<ArchiveDto | null>(null)
const borrowForm = reactive<{ days: number; reason: string }>({ days: 7, reason: '' })

async function loadCategories() {
  const { data } = await listCategories()
  categories.value = data.data
}
// 检索联想
// 所选门类的自定义字段（用于结果表动态列）
const metaColumns = ref<{ key: string; label: string }[]>([])
function refreshMetaColumns() {
  const c = categories.value.find(x => x.id === q.categoryId)
  const tpl = safeParse(c?.templateJson)
  metaColumns.value = Array.isArray(tpl)
    ? tpl.filter((f: any) => f?.key).map((f: any) => ({ key: f.key, label: f.label || f.key }))
    : []
}

async function querySuggest(kw: string, cb: (arr: { value: string }[]) => void) {
  if (!kw) { cb([]); return }
  const { data } = await suggest(kw)
  cb((data.data ?? []).map(s => ({ value: s })))
}
async function onSearch() {
  q.page = 1
  await doSearch()
}
async function onReset() {
  Object.assign(q, { categoryId: undefined, yearStart: undefined, yearEnd: undefined, title: '', author: '', retention: '', secrecyLevel: '', status: '', page: 1 })
  await doSearch()
}
async function onPage(p: number) {
  q.page = p
  await doSearch()
}
async function doSearch() {
  loading.value = true
  try {
    const payload: SearchArchiveDto = {
      categoryId: q.categoryId, yearStart: q.yearStart, yearEnd: q.yearEnd,
      title: q.title || undefined, author: q.author || undefined,
      retention: q.retention || undefined, secrecyLevel: q.secrecyLevel || undefined,
      status: q.status || undefined, page: q.page, pageSize: q.pageSize,
      sortBy: q.sortBy, sortDir: q.sortDir
    }
    const { data } = await searchArchives(payload)
    // 把 dynamicFields 里的自定义字段摊平成 __meta_xxx，供动态列展示
    rows.value = (data.data.items ?? []).map((a: any) => {
      const meta = safeParse(a.dynamicFields)
      const flat: any = { ...a }
      for (const f of metaColumns.value) flat[`__meta_${f.key}`] = meta?.[f.key] ?? ''
      return flat
    })
    total.value = data.data.total
  } finally {
    loading.value = false
  }
}
function safeParse(json?: string) {
  try { return JSON.parse(json || '{}') } catch { return {} }
}
function openBorrow(row: ArchiveDto) {
  cur.value = row
  borrowForm.days = 7
  borrowForm.reason = ''
  showBorrow.value = true
}
async function onBorrow() {
  if (!cur.value) return
  await createBorrow({ targetType: 'archive', targetId: cur.value.id, days: borrowForm.days, reason: borrowForm.reason })
  showBorrow.value = false
  ElMessage.success('借阅申请已提交，等待审批')
}

// 切换门类 → 同步结果表的自定义字段列
watch(() => q.categoryId, () => refreshMetaColumns())

onMounted(async () => {
  await loadCategories()
  refreshMetaColumns()
  await doSearch()
})
</script>

<style scoped>
.page { padding: 16px; }
</style>
