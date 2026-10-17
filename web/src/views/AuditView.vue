<template>
  <div class="page">
    <h3 style="margin-top: 0">审计日志（只读）</h3>

    <el-form :inline="true" style="margin-bottom: 12px">
      <el-form-item label="起始"><el-date-picker v-model="from" type="datetime" placeholder="可选" value-format="YYYY-MM-DDTHH:mm:ss" /></el-form-item>
      <el-form-item label="截止"><el-date-picker v-model="to" type="datetime" placeholder="可选" value-format="YYYY-MM-DDTHH:mm:ss" /></el-form-item>
      <el-form-item label="动作">
        <el-input v-model="action" placeholder="如 archive.create" clearable style="width: 200px" />
      </el-form-item>
      <el-form-item>
        <el-button type="primary" @click="load(1)">查询</el-button>
        <el-button @click="reset">重置</el-button>
      </el-form-item>
    </el-form>

    <el-table :data="rows" border v-loading="loading">
      <el-table-column prop="ts" label="时间" width="180" />
      <el-table-column prop="userName" label="操作人" width="120" />
      <el-table-column prop="action" label="动作" width="200" />
      <el-table-column prop="targetType" label="对象类型" width="110" />
      <el-table-column prop="targetId" label="对象ID" width="90" />
      <el-table-column prop="ip" label="IP" width="130" />
      <el-table-column prop="detail" label="明细" />
    </el-table>

    <el-pagination style="margin-top: 12px; justify-content: flex-end"
      v-model:current-page="page" v-model:page-size="size"
      :page-sizes="[20, 50, 100]" :total="total"
      layout="total, sizes, prev, pager, next" @current-change="load" @size-change="load(1)" />
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { listAuditLogsPaged } from '@/api/audit'
import type { AuditLogDto } from '@/types/models'

const rows = ref<AuditLogDto[]>([])
const loading = ref(false)
const from = ref<string>('')
const to = ref<string>('')
const action = ref('')
const page = ref(1)
const size = ref(20)
const total = ref(0)

async function load(p?: number) {
  if (p) page.value = p
  loading.value = true
  try {
    const { data } = await listAuditLogsPaged({
      from: from.value || undefined,
      to: to.value || undefined,
      action: action.value || undefined,
      page: page.value,
      pageSize: size.value
    })
    rows.value = data.data.items ?? []
    total.value = data.data.total ?? 0
  } finally { loading.value = false }
}
function reset() { from.value = ''; to.value = ''; action.value = ''; load(1) }
onMounted(() => load())
</script>

<style scoped>
.page { padding: 16px; }
</style>
