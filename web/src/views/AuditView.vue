<template>
  <div class="page">
    <h3 style="margin-top: 0">审计日志（只读）</h3>

    <el-form :inline="true" style="margin-bottom: 12px">
      <el-form-item label="起始"><el-date-picker v-model="from" type="datetime" placeholder="可选" value-format="YYYY-MM-DDTHH:mm:ss" /></el-form-item>
      <el-form-item label="截止"><el-date-picker v-model="to" type="datetime" placeholder="可选" value-format="YYYY-MM-DDTHH:mm:ss" /></el-form-item>
      <el-form-item><el-button type="primary" @click="load">查询</el-button><el-button @click="reset">重置</el-button></el-form-item>
    </el-form>

    <el-table :data="rows" border v-loading="loading">
      <el-table-column prop="ts" label="时间" width="180" />
      <el-table-column prop="userName" label="操作人" width="120" />
      <el-table-column prop="action" label="动作" width="180" />
      <el-table-column prop="targetType" label="对象类型" width="110" />
      <el-table-column prop="targetId" label="对象ID" width="90" />
      <el-table-column prop="detail" label="明细" />
    </el-table>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { listAuditLogs } from '@/api/audit'
import type { AuditLogDto } from '@/types/models'

const rows = ref<AuditLogDto[]>([])
const loading = ref(false)
const from = ref<string>('')
const to = ref<string>('')

async function load() {
  loading.value = true
  try {
    const { data } = await listAuditLogs(from.value || undefined, to.value || undefined)
    rows.value = data.data
  } finally { loading.value = false }
}
function reset() { from.value = ''; to.value = ''; load() }
onMounted(load)
</script>

<style scoped>
.page { padding: 16px; }
</style>
