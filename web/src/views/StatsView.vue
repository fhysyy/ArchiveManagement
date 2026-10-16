<template>
  <div class="page">
    <h3 style="margin-top: 0">统计报表</h3>

    <el-tabs v-model="tab">
      <el-tab-pane label="档案总览" name="overview">
        <el-row :gutter="12" style="margin-bottom: 12px">
          <el-col :span="8"><el-card shadow="never"><div class="kpi"><div class="kpi-num">{{ ov?.total ?? 0 }}</div><div>档案总数</div></div></el-card></el-col>
          <el-col :span="8"><el-card shadow="never"><div class="kpi"><div class="kpi-num ok">{{ ov?.archived ?? 0 }}</div><div>已归档</div></div></el-card></el-col>
          <el-col :span="8"><el-card shadow="never"><div class="kpi"><div class="kpi-num warn">{{ ov?.draft ?? 0 }}</div><div>草稿</div></div></el-card></el-col>
        </el-row>
        <el-row :gutter="12">
          <el-col :span="12">
            <el-card shadow="never"><template #header>按门类</template>
              <el-table :data="ov?.byCategory ?? []" size="small"><el-table-column prop="categoryName" label="门类" /><el-table-column prop="count" label="数量" width="90" /></el-table>
            </el-card>
          </el-col>
          <el-col :span="12">
            <el-card shadow="never"><template #header>按年度</template>
              <el-table :data="ov?.byYear ?? []" size="small"><el-table-column prop="key" label="年度" /><el-table-column prop="count" label="数量" width="90" /></el-table>
            </el-card>
          </el-col>
        </el-row>
        <el-row :gutter="12" style="margin-top: 12px">
          <el-col :span="12">
            <el-card shadow="never"><template #header>按保管期限</template>
              <el-table :data="ov?.byRetention ?? []" size="small"><el-table-column prop="key" label="期限" /><el-table-column prop="count" label="数量" width="90" /></el-table>
            </el-card>
          </el-col>
          <el-col :span="12">
            <el-card shadow="never"><template #header>按密级</template>
              <el-table :data="ov?.bySecrecy ?? []" size="small"><el-table-column prop="key" label="密级" /><el-table-column prop="count" label="数量" width="90" /></el-table>
            </el-card>
          </el-col>
        </el-row>
      </el-tab-pane>

      <el-tab-pane label="借阅利用统计" name="borrow">
        <el-row :gutter="12" style="margin-bottom: 12px">
          <el-col :span="12"><el-card shadow="never"><div class="kpi"><div class="kpi-num">{{ bs?.totalBorrows ?? 0 }}</div><div>借阅总人次</div></div></el-card></el-col>
          <el-col :span="12"><el-card shadow="never"><div class="kpi"><div class="kpi-num warn">{{ bs?.pendingBorrows ?? 0 }}</div><div>待审批</div></div></el-card></el-col>
        </el-row>
        <el-row :gutter="12">
          <el-col :span="12">
            <el-card shadow="never"><template #header>高频档案 TOP10</template>
              <el-table :data="bs?.byTarget ?? []" size="small"><el-table-column prop="targetTitle" label="档案/文件" /><el-table-column prop="count" label="次数" width="80" /></el-table>
            </el-card>
          </el-col>
          <el-col :span="12">
            <el-card shadow="never"><template #header>高频利用者 TOP10</template>
              <el-table :data="bs?.byApplicant ?? []" size="small"><el-table-column prop="applicantName" label="利用者" /><el-table-column prop="count" label="次数" width="80" /></el-table>
            </el-card>
          </el-col>
        </el-row>
      </el-tab-pane>

      <el-tab-pane label="到期未鉴定预警" name="due">
        <el-alert type="warning" :closable="false" style="margin-bottom: 12px"
          title="保管期限已到期且尚未发起鉴定的档案，建议尽快处理" />
        <el-table :data="dw?.items ?? []" border v-loading="loading">
          <el-table-column prop="archive.archiveNo" label="档号" width="170" />
          <el-table-column prop="archive.title" label="题名" />
          <el-table-column prop="archive.retention" label="期限" width="90" />
          <el-table-column prop="expiryYear" label="到期年度" width="100" />
          <el-table-column label="已鉴定" width="90">
            <template #default="{ row }"><el-tag size="small" :type="row.appraised ? 'success' : 'warning'">{{ row.appraised ? '是' : '否' }}</el-tag></template>
          </el-table-column>
        </el-table>
      </el-tab-pane>
    </el-tabs>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, watch } from 'vue'
import { statsOverview, statsBorrow, statsDueWarning } from '@/api/stats'
import type { StatsOverviewDto, BorrowStatsDto, DueWarningDto } from '@/types/models'

const tab = ref('overview')
const ov = ref<StatsOverviewDto>()
const bs = ref<BorrowStatsDto>()
const dw = ref<DueWarningDto>()
const loading = ref(false)

async function loadOverview() { const { data } = await statsOverview(); ov.value = data.data }
async function loadBorrow() { const { data } = await statsBorrow(); bs.value = data.data }
async function loadDue() {
  loading.value = true
  try { const { data } = await statsDueWarning(); dw.value = data.data } finally { loading.value = false }
}
function refresh() {
  if (tab.value === 'overview') loadOverview()
  else if (tab.value === 'borrow') loadBorrow()
  else loadDue()
}
watch(tab, refresh)
onMounted(loadOverview)
</script>

<style scoped>
.page { padding: 16px; }
.kpi { text-align: center; color: #475569; }
.kpi-num { font-size: 28px; font-weight: 600; color: #1e293b; }
.kpi-num.ok { color: #16a34a; }
.kpi-num.warn { color: #d97706; }
</style>
