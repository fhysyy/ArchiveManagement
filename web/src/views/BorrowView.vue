<template>
  <div class="page">
    <h3 style="margin-top: 0">借阅利用</h3>

    <el-tabs v-model="tab">
      <el-tab-pane label="我的申请" name="applicant">
        <el-table :data="applicantRows" border v-loading="loadingA">
          <el-table-column prop="targetTitle" label="档案/文件" />
          <el-table-column prop="status" label="状态" width="100" />
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
import { listBorrow, approveBorrow, rejectBorrow, returnBorrow, renewBorrow } from '@/api/borrow'
import type { BorrowDto, BorrowAuditDto } from '@/types/models'
import { ElMessage } from 'element-plus'

const tab = ref('applicant')
const applicantRows = ref<BorrowDto[]>([])
const approverRows = ref<BorrowDto[]>([])
const loadingA = ref(false)
const loadingB = ref(false)

const showAudit = ref(false)
const auditAction = ref<'approve' | 'reject'>('approve')
const auditTitle = ref('')
const cur = ref<BorrowDto | null>(null)
const auditForm = reactive<BorrowAuditDto>({ days: 7, comment: '' })

async function loadApplicant() {
  loadingA.value = true
  try {
    const { data } = await listBorrow('applicant')
    applicantRows.value = data.data
  } finally { loadingA.value = false }
}
async function loadApprover() {
  loadingB.value = true
  try {
    const { data } = await listBorrow('approver')
    approverRows.value = data.data
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
  await renewBorrow(row.id, 7)
  ElMessage.success('已续期 7 天')
  loadApplicant()
}

onMounted(loadApplicant)
</script>

<style scoped>
.page { padding: 16px; }
</style>
