<template>
  <div class="page">
    <h3 style="margin-top: 0">鉴定销毁</h3>

    <!-- 到期未鉴定预警 -->
    <el-alert v-if="pendingCount > 0" type="warning" :closable="false" show-icon style="margin-bottom: 12px"
      :title="`有 ${pendingCount} 卷档案保管期限已到期但尚未发起鉴定提议，请及时处理`" />

    <el-tabs v-model="tab">
      <el-tab-pane :label="`到期保管期限清单(${totalDue})`" name="due">
        <el-form :inline="true" style="margin-bottom: 8px">
          <el-form-item>
            <el-checkbox v-model="onlyPending" @change="loadDue(1)">仅看未发起鉴定的</el-checkbox>
          </el-form-item>
          <el-form-item>
            <el-button type="primary" plain @click="loadDue(1)">刷新</el-button>
          </el-form-item>
        </el-form>

        <el-table :data="dueRows" border v-loading="loadingDue">
          <el-table-column prop="archive.archiveNo" label="档号" width="170" />
          <el-table-column prop="archive.title" label="题名" />
          <el-table-column prop="archive.retention" label="保管期限" width="100" />
          <el-table-column prop="expiryYear" label="到期年度" width="100" />
          <el-table-column label="鉴定状态" width="120">
            <template #default="{ row }">
              <el-tag v-if="row.appraisalId" size="small" :type="apprStatusType(row.appraisalStatus)">
                {{ apprStatusLabel(row.appraisalStatus) }}
              </el-tag>
              <el-tag v-else size="small" type="warning">未提议</el-tag>
            </template>
          </el-table-column>
          <el-table-column label="操作" width="130">
            <template #default="{ row }">
              <el-button v-if="!row.appraisalId" size="small" type="primary" plain @click="onCreateAppraisal(row.archive)">发起鉴定</el-button>
              <span v-else style="color:#909399">已在途</span>
            </template>
          </el-table-column>
        </el-table>

        <el-pagination style="margin-top: 12px; justify-content: flex-end"
          v-model:current-page="pageDue" v-model:page-size="sizeDue"
          :page-sizes="[10, 20, 50]" :total="totalDue"
          layout="total, sizes, prev, pager, next" @current-change="loadDue" @size-change="loadDue(1)" />
      </el-tab-pane>

      <el-tab-pane :label="`鉴定任务(${totalTasks})`" name="tasks">
        <el-form :inline="true" style="margin-bottom: 8px">
          <el-form-item label="状态">
            <el-select v-model="filterStatus" clearable placeholder="全部" style="width: 140px" @change="loadTasks(1)">
              <el-option v-for="s in statusOpts" :key="s.value" :label="s.label" :value="s.value" />
            </el-select>
          </el-form-item>
        </el-form>

        <el-table :data="taskRows" border v-loading="loadingTasks">
          <el-table-column prop="targetTitle" label="档案/文件" />
          <el-table-column prop="proposeReason" label="提议理由" />
          <el-table-column prop="status" label="状态" width="110">
            <template #default="{ row }"><el-tag size="small" :type="apprStatusType(row.status)">{{ apprStatusLabel(row.status) }}</el-tag></template>
          </el-table-column>
          <el-table-column prop="approvedByName" label="审批人" width="100" />
          <el-table-column prop="createdAt" label="提议时间" width="170" />
          <el-table-column label="操作" width="220">
            <template #default="{ row }">
              <template v-if="row.status === 'Pending'">
                <el-button size="small" type="success" plain @click="onApprove(row, 'keep')">续存</el-button>
                <el-button size="small" type="danger" plain @click="onApprove(row, 'destroy')">销毁</el-button>
              </template>
              <el-button v-else-if="row.status === 'Approved'" size="small" type="danger" @click="onExecute(row)">执行销毁</el-button>
              <span v-else>-</span>
            </template>
          </el-table-column>
        </el-table>

        <el-pagination style="margin-top: 12px; justify-content: flex-end"
          v-model:current-page="pageTask" v-model:page-size="sizeTask"
          :page-sizes="[10, 20, 50]" :total="totalTasks"
          layout="total, sizes, prev, pager, next" @current-change="loadTasks" @size-change="loadTasks(1)" />
      </el-tab-pane>
    </el-tabs>

    <el-dialog v-model="showReason" title="发起鉴定提议" width="420px">
      <el-form label-width="80px">
        <el-form-item label="档案"><span>{{ reasonArchive?.title }}</span></el-form-item>
        <el-form-item label="理由"><el-input v-model="reasonText" type="textarea" :rows="3" /></el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showReason = false">取消</el-button>
        <el-button type="primary" @click="onSubmitAppraisal">提交提议</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, watch } from 'vue'
import { listDuePaged, createAppraisal, listAppraisalPaged, approveAppraisal, executeAppraisal } from '@/api/appraisal'
import type { DueArchiveDto, AppraisalDto, ArchiveDto } from '@/types/models'
import { ElMessage, ElMessageBox } from 'element-plus'

const tab = ref('due')
const dueRows = ref<DueArchiveDto[]>([])
const taskRows = ref<AppraisalDto[]>([])
const loadingDue = ref(false)
const loadingTasks = ref(false)

// 分页
const pageDue = ref(1)
const sizeDue = ref(10)
const totalDue = ref(0)
const onlyPending = ref(false)
const pendingCount = ref(0)

const pageTask = ref(1)
const sizeTask = ref(10)
const totalTasks = ref(0)
const filterStatus = ref('')

const statusOpts = [
  { value: 'Pending', label: '待审批' },
  { value: 'Approved', label: '待销毁' },
  { value: 'Destroyed', label: '已销毁' },
  { value: 'Kept', label: '续存' }
]

const showReason = ref(false)
const reasonArchive = ref<ArchiveDto | null>(null)
const reasonText = ref('')

const apprStatusMap: Record<string, { label: string; type: string }> = {
  Pending: { label: '待审批', type: 'warning' },
  Approved: { label: '待销毁', type: 'danger' },
  Destroyed: { label: '已销毁', type: 'info' },
  Kept: { label: '续存', type: 'success' }
}
function apprStatusLabel(s: string) { return apprStatusMap[s]?.label ?? (s || '未提议') }
function apprStatusType(s: string) { return (apprStatusMap[s]?.type ?? '') as '' | 'info' | 'success' | 'warning' | 'danger' }

async function loadDue(p?: number) {
  if (p) pageDue.value = p
  loadingDue.value = true
  try {
    const { data } = await listDuePaged(pageDue.value, sizeDue.value, onlyPending.value)
    dueRows.value = data.data.items ?? []
    totalDue.value = data.data.total
  } finally { loadingDue.value = false }
}

/** 预警：已到期但尚未发起鉴定的数量（不受分页/筛选影响） */
async function loadPendingCount() {
  try {
    const { data } = await listDuePaged(1, 1, true)
    pendingCount.value = data.data.total
  } catch { pendingCount.value = 0 }
}

async function loadTasks(p?: number) {
  if (p) pageTask.value = p
  loadingTasks.value = true
  try {
    const { data } = await listAppraisalPaged(pageTask.value, sizeTask.value, filterStatus.value || undefined)
    taskRows.value = data.data.items ?? []
    totalTasks.value = data.data.total
  } finally { loadingTasks.value = false }
}

function refresh() { if (tab.value === 'due') loadDue(); else loadTasks() }
watch(tab, refresh)

function onCreateAppraisal(a: ArchiveDto) {
  reasonArchive.value = a
  reasonText.value = ''
  showReason.value = true
}
async function onSubmitAppraisal() {
  if (!reasonArchive.value) return
  await createAppraisal({ targetType: 'archive', targetId: reasonArchive.value.id, proposeReason: reasonText.value })
  showReason.value = false
  ElMessage.success('鉴定提议已提交')
  loadTasks()
  loadDue()
  loadPendingCount()
}
async function onApprove(row: AppraisalDto, action: 'keep' | 'destroy') {
  await approveAppraisal(row.id, { action })
  ElMessage.success(action === 'keep' ? '已续存' : '已批准销毁（待执行）')
  loadTasks()
}
async function onExecute(row: AppraisalDto) {
  await ElMessageBox.confirm('执行销毁后档案将置为「已销毁」且不可恢复，确认继续？', '危险操作', { type: 'warning' })
  await executeAppraisal(row.id)
  ElMessage.success('已执行销毁，档案置为已销毁')
  loadTasks()
  loadDue()
  loadPendingCount()
}

onMounted(async () => {
  await loadDue()
  await loadPendingCount()
})
</script>

<style scoped>
.page { padding: 16px; }
</style>
