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
              <BarChart :items="byCategory" color="#409eff" />
            </el-card>
          </el-col>
          <el-col :span="12">
            <el-card shadow="never"><template #header>按年度</template>
              <BarChart :items="byYear" color="#67c23a" />
            </el-card>
          </el-col>
        </el-row>
        <el-row :gutter="12" style="margin-top: 12px">
          <el-col :span="12">
            <el-card shadow="never"><template #header>按保管期限</template>
              <BarChart :items="byRetention" color="#e6a23c" />
            </el-card>
          </el-col>
          <el-col :span="12">
            <el-card shadow="never"><template #header>按密级</template>
              <BarChart :items="bySecrecy" color="#f56c6c" />
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
              <BarChart :items="byTarget" color="#409eff" />
            </el-card>
          </el-col>
          <el-col :span="12">
            <el-card shadow="never"><template #header>高频利用者 TOP10</template>
              <BarChart :items="byApplicant" color="#909399" />
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
import { ref, computed, onMounted, watch } from 'vue'
import { statsOverview, statsBorrow, statsDueWarning } from '@/api/stats'
import BarChart from '@/components/BarChart.vue'
import type { StatsOverviewDto, BorrowStatsDto, DueWarningDto } from '@/types/models'

const tab = ref('overview')
const ov = ref<StatsOverviewDto>()
const bs = ref<BorrowStatsDto>()
const dw = ref<DueWarningDto>()
const loading = ref(false)

// 统一成 { key, count } 供图表组件消费
const byCategory = computed(() => (ov.value?.byCategory ?? []).map(c => ({ key: c.categoryName, count: c.count })))
const byYear = computed(() => ov.value?.byYear ?? [])
const byRetention = computed(() => ov.value?.byRetention ?? [])
const bySecrecy = computed(() => ov.value?.bySecrecy ?? [])
const byTarget = computed(() => (bs.value?.byTarget ?? []).map(t => ({ key: t.targetTitle, count: t.count })))
const byApplicant = computed(() => (bs.value?.byApplicant ?? []).map(a => ({ key: a.applicantName, count: a.count })))

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
