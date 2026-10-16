<template>
  <div class="page">
    <h3 style="margin-top: 0">鉴定销毁</h3>

    <el-tabs v-model="tab">
      <el-tab-pane label="到期保管期限清单" name="due">
        <el-alert type="info" :closable="false" style="margin-bottom: 12px"
          title="下列档案保管期限已到期（『永久』不列入），可发起鉴定提议" />
        <el-table :data="dueRows" border v-loading="loadingDue">
          <el-table-column prop="archive.archiveNo" label="档号" width="170" />
          <el-table-column prop="archive.title" label="题名" />
          <el-table-column prop="archive.retention" label="保管期限" width="100" />
          <el-table-column prop="expiryYear" label="到期年度" width="100" />
          <el-table-column label="操作" width="120">
            <template #default="{ row }">
              <el-button size="small" type="primary" plain @click="onCreateAppraisal(row.archive)">发起鉴定</el-button>
            </template>
          </el-table-column>
        </el-table>
      </el-tab-pane>

      <el-tab-pane label="鉴定任务" name="tasks">
        <el-table :data="taskRows" border v-loading="loadingTasks">
          <el-table-column prop="targetTitle" label="档案/文件" />
          <el-table-column prop="proposeReason" label="提议理由" />
          <el-table-column prop="status" label="状态" width="100">
            <template #default="{ row }"><el-tag size="small" :type="apprStatusType(row.status)">{{ apprStatusLabel(row.status) }}</el-tag></template>
          </el-table-column>
          <el-table-column prop="approvedByName" label="审批人" width="100" />
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
import { listDue, createAppraisal, listAppraisal, approveAppraisal, executeAppraisal } from '@/api/appraisal'
import type { DueArchiveDto, AppraisalDto, ArchiveDto } from '@/types/models'
import { ElMessage } from 'element-plus'

const tab = ref('due')
const dueRows = ref<DueArchiveDto[]>([])
const taskRows = ref<AppraisalDto[]>([])
const loadingDue = ref(false)
const loadingTasks = ref(false)

const showReason = ref(false)
const reasonArchive = ref<ArchiveDto | null>(null)
const reasonText = ref('')

const apprStatusMap: Record<string, { label: string; type: string }> = {
  Pending: { label: '待审批', type: 'warning' },
  Approved: { label: '待销毁', type: 'danger' },
  Destroyed: { label: '已销毁', type: 'info' },
  Kept: { label: '续存', type: 'success' }
}
function apprStatusLabel(s: string) { return apprStatusMap[s]?.label ?? s }
function apprStatusType(s: string) { return (apprStatusMap[s]?.type ?? '') as '' | 'info' | 'success' | 'warning' | 'danger' }

async function loadDue() {
  loadingDue.value = true
  try { const { data } = await listDue(); dueRows.value = data.data } finally { loadingDue.value = false }
}
async function loadTasks() {
  loadingTasks.value = true
  try { const { data } = await listAppraisal(); taskRows.value = data.data } finally { loadingTasks.value = false }
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
}
async function onApprove(row: AppraisalDto, action: 'keep' | 'destroy') {
  await approveAppraisal(row.id, { action })
  ElMessage.success(action === 'keep' ? '已续存' : '已批准销毁（待执行）')
  loadTasks()
}
async function onExecute(row: AppraisalDto) {
  await executeAppraisal(row.id)
  ElMessage.success('已执行销毁，档案置为已销毁')
  loadTasks()
}

onMounted(loadDue)
</script>

<style scoped>
.page { padding: 16px; }
</style>
