<template>
  <div class="page">
    <h3 style="margin-top: 0">借阅利用</h3>

    <el-tabs v-model="tab">
      <el-tab-pane label="我的申请" name="applicant">
        <el-form :inline="true" style="margin-bottom: 8px">
          <el-form-item label="状态">
            <el-select v-model="filterA" clearable placeholder="全部" style="width: 140px">
              <el-option v-for="s in statusOpts" :key="s.value" :label="s.label" :value="s.value" />
            </el-select>
          </el-form-item>
          <el-form-item>
            <el-button type="primary" plain @click="loadApplicant(1)">查询</el-button>
          </el-form-item>
        </el-form>

        <el-table :data="applicantRows" border v-loading="loadingA">
          <el-table-column prop="targetTitle" label="档案/文件" />
          <el-table-column prop="status" label="状态" width="110">
            <template #default="{ row }">
              <el-tag size="small" :type="statusType(row.status)">{{ statusLabel(row.status) }}</el-tag>
            </template>
          </el-table-column>
          <el-table-column prop="days" label="天数" width="70" />
          <el-table-column prop="approvedByName" label="审批人" width="100" />
          <el-table-column prop="expireAt" label="到期" width="170" />
          <el-table-column label="操作" width="160">
            <template #default="{ row }">
              <template v-if="row.status === 'Approved'">
                <el-button size="small" type="success" plain @click="onReturn(row)">归还</el-button>
                <el-button size="small" type="warning" plain @click="onRenew(row)">续期</el-button>
              </template>
              <span v-else>-</span>
            </template>
          </el-table-column>
        </el-table>

        <el-pagination style="margin-top: 12px; justify-content: flex-end"
          v-model:current-page="pageA" v-model:page-size="sizeA"
          :page-sizes="[10, 20, 50]" :total="totalA"
          layout="total, sizes, prev, pager, next" @current-change="loadApplicant" @size-change="loadApplicant(1)" />
      </el-tab-pane>

      <el-tab-pane label="待我审批" name="approver">
        <el-table :data="approverRows" border v-loading="loadingB">
          <el-table-column prop="applicantName" label="申请人" width="110" />
          <el-table-column prop="targetTitle" label="档案/文件" />
          <el-table-column prop="reason" label="事由" />
          <el-table-column prop="days" label="天数" width="70" />
          <el-table-column prop="createdAt" label="申请时间" width="170" />
          <el-table-column label="操作" width="180">
            <template #default="{ row }">
              <el-button size="small" type="primary" plain @click="onApprove(row)">通过</el-button>
              <el-button size="small" type="danger" plain @click="onReject(row)">驳回</el-button>
            </template>
          </el-table-column>
        </el-table>

        <el-pagination style="margin-top: 12px; justify-content: flex-end"
          v-model:current-page="pageB" v-model:page-size="sizeB"
          :page-sizes="[10, 20, 50]" :total="totalB"
          layout="total, sizes, prev, pager, next" @current-change="loadApprover" @size-change="loadApprover(1)" />
      </el-tab-pane>
    </el-tabs>

    <!-- 审批对话框（通过/驳回共用，可填天数或意见） -->
    <el-dialog v-model="showAudit" :title="auditTitle" width="420px">
      <el-form label-width="80px">
        <el-form-item v-if="auditAction === 'approve'" label="借阅天数">
          <el-input v-model.number="auditForm.days" />
        </el-form-item>
        <el-form-item label="意见">
          <el-input v-model="auditForm.comment" type="textarea" :rows="3" :placeholder="auditAction === 'reject' ? '驳回理由' : '可选'" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showAudit = false">取消</el-button>
        <el-button :type="auditAction === 'approve' ? 'primary' : 'danger'" @click="onSubmitAudit">确定</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, watch } from 'vue'
import { listBorrowPaged, approveBorrow, rejectBorrow, returnBorrow, renewBorrow } from '@/api/borrow'
import type { BorrowDto, BorrowAuditDto } from '@/types/models'
import { ElMessage, ElMessageBox } from 'element-plus'

const tab = ref('applicant')

const applicantRows = ref<BorrowDto[]>([])
const approverRows = ref<BorrowDto[]>([])
const loadingA = ref(false)
const loadingB = ref(false)

// 分页状态
const pageA = ref(1)
const sizeA = ref(10)
const totalA = ref(0)
const filterA = ref('')
const pageB = ref(1)
const sizeB = ref(10)
const totalB = ref(0)

const statusOpts = [
  { value: 'Pending', label: '待审批' },
  { value: 'Approved', label: '已通过' },
  { value: 'Rejected', label: '已驳回' },
  { value: 'Returned', label: '已归还' },
  { value: 'Expired', label: '已逾期' }
]
const statusMap: Record<string, { label: string; type: string }> = {
  Pending: { label: '待审批', type: 'warning' },
  Approved: { label: '已通过', type: 'success' },
  Rejected: { label: '已驳回', type: 'info' },
  Returned: { label: '已归还', type: 'info' },
  Expired: { label: '已逾期', type: 'danger' }
}
function statusLabel(s: string) { return statusMap[s]?.label ?? s }
function statusType(s: string) { return (statusMap[s]?.type ?? '') as '' | 'info' | 'success' | 'warning' | 'danger' }

const showAudit = ref(false)
const auditAction = ref<'approve' | 'reject'>('approve')
const auditTitle = ref('')
const cur = ref<BorrowDto | null>(null)
const auditForm = reactive<BorrowAuditDto>({ days: 7, comment: '' })

async function loadApplicant(p?: number) {
  if (p) pageA.value = p
  loadingA.value = true
  try {
    const { data } = await listBorrowPaged('applicant', pageA.value, sizeA.value)
    let items = data.data.items ?? []
    if (filterA.value) items = items.filter(x => x.status === filterA.value)
    applicantRows.value = items
    totalA.value = filterA.value ? items.length : data.data.total
  } finally { loadingA.value = false }
}

async function loadApprover(p?: number) {
  if (p) pageB.value = p
  loadingB.value = true
  try {
    const { data } = await listBorrowPaged('approver', pageB.value, sizeB.value)
    approverRows.value = data.data.items ?? []
    totalB.value = data.data.total
  } finally { loadingB.value = false }
}

function refresh() {
  if (tab.value === 'applicant') loadApplicant()
  else loadApprover()
}
watch(tab, refresh)

function onApprove(row: BorrowDto) {
  cur.value = row
  auditAction.value = 'approve'
  auditTitle.value = '审批通过'
  auditForm.days = row.days ?? 7
  auditForm.comment = ''
  showAudit.value = true
}
function onReject(row: BorrowDto) {
  cur.value = row
  auditAction.value = 'reject'
  auditTitle.value = '驳回申请'
  auditForm.days = row.days ?? 7
  auditForm.comment = ''
  showAudit.value = true
}
async function onSubmitAudit() {
  if (!cur.value) return
  if (auditAction.value === 'approve')
    await approveBorrow(cur.value.id, { days: auditForm.days, comment: auditForm.comment })
  else
    await rejectBorrow(cur.value.id, { comment: auditForm.comment })
  showAudit.value = false
  ElMessage.success('已处理')
  loadApprover()
}
async function onReturn(row: BorrowDto) {
  await returnBorrow(row.id)
  ElMessage.success('已归还')
  loadApplicant()
}
async function onRenew(row: BorrowDto) {
  const { value } = await ElMessageBox.prompt('续期天数', '借阅续期', {
    inputValue: '7', inputPattern: /^\d{1,3}$/, inputErrorMessage: '请输入 1-999 的数字'
  })
  await renewBorrow(row.id, Number(value))
  ElMessage.success(`已续期 ${value} 天`)
  loadApplicant()
}

onMounted(() => loadApplicant())
</script>

<style scoped>
.page { padding: 16px; }
</style>
