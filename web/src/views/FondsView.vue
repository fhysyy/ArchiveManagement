<template>
  <div class="page">
    <h3 style="margin-top: 0">全宗管理</h3>

    <el-alert type="info" :closable="false" style="margin-bottom: 12px"
      title="全宗号用于档号规则中的 {fonds} 占位符。一期为单全宗模式，多全宗的表结构与接口已预留。" />

    <el-table :data="rows" border v-loading="loading">
      <el-table-column prop="code" label="全宗号" width="120" />
      <el-table-column prop="name" label="全宗名称" />
      <el-table-column prop="description" label="说明" />
      <el-table-column prop="categoryCount" label="门类数" width="90" />
      <el-table-column prop="archiveCount" label="档案数" width="90" />
      <el-table-column label="状态" width="90">
        <template #default="{ row }">
          <el-tag size="small" :type="row.status === 1 ? 'success' : 'info'">{{ row.status === 1 ? '启用' : '停用' }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="操作" width="100">
        <template #default="{ row }">
          <el-button size="small" type="primary" plain @click="openEdit(row)">编辑</el-button>
        </template>
      </el-table-column>
    </el-table>

    <div style="margin-top: 12px">
      <el-button type="primary" plain @click="openCreate">新增全宗</el-button>
    </div>

    <el-dialog v-model="showForm" :title="form.id ? '编辑全宗' : '新增全宗'" width="480px">
      <el-form label-width="90px">
        <el-form-item label="全宗号" required>
          <el-input v-model="form.code" placeholder="如 F1" :disabled="!!form.id" />
        </el-form-item>
        <el-form-item label="全宗名称" required><el-input v-model="form.name" /></el-form-item>
        <el-form-item label="说明"><el-input v-model="form.description" type="textarea" :rows="3" /></el-form-item>
        <el-form-item label="状态">
          <el-switch v-model="form.statusOn" active-text="启用" inactive-text="停用" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showForm = false">取消</el-button>
        <el-button type="primary" @click="onSubmit">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { listFonds, saveFonds } from '@/api/fonds'
import type { FondsDto } from '@/types/models'
import { ElMessage } from 'element-plus'

const rows = ref<FondsDto[]>([])
const loading = ref(false)
const showForm = ref(false)
const form = reactive({ id: 0, code: '', name: '', description: '', statusOn: true })

async function load() {
  loading.value = true
  try { const { data } = await listFonds(); rows.value = data.data } finally { loading.value = false }
}

function openCreate() {
  form.id = 0; form.code = ''; form.name = ''; form.description = ''; form.statusOn = true
  showForm.value = true
}
function openEdit(row: FondsDto) {
  form.id = row.id; form.code = row.code; form.name = row.name
  form.description = row.description ?? ''; form.statusOn = row.status === 1
  showForm.value = true
}
async function onSubmit() {
  if (!form.code.trim() || !form.name.trim()) { ElMessage.warning('全宗号与名称不能为空'); return }
  await saveFonds({
    id: form.id, code: form.code.trim(), name: form.name.trim(),
    description: form.description, status: form.statusOn ? 1 : 0
  })
  showForm.value = false
  ElMessage.success('已保存')
  load()
}

onMounted(load)
</script>

<style scoped>
.page { padding: 16px; }
</style>
