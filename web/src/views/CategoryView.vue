<template>
  <div class="page">
    <div style="margin-bottom: 12px">
      <el-button type="primary" @click="openCreate">新增门类</el-button>
    </div>
    <el-table :data="rows" border>
      <el-table-column prop="code" label="代码" width="90" />
      <el-table-column prop="name" label="名称" />
      <el-table-column prop="defaultRetention" label="默认保管期限" width="130" />
      <el-table-column label="案卷级字段" width="110">
        <template #default="{ row }">{{ parsedCount(row.templateJson) }} 项</template>
      </el-table-column>
      <el-table-column label="文件级字段" width="110">
        <template #default="{ row }">{{ parsedCount(row.fileTemplateJson) }} 项</template>
      </el-table-column>
      <el-table-column label="操作" width="200">
        <template #default="{ row }">
          <el-button size="small" @click="openEdit(row)">编辑</el-button>
          <el-button size="small" @click="goArchives(row)">档案 ›</el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-dialog
      v-model="showCreate"
      :title="editingId ? '编辑门类' : '新增门类'"
      width="88%"
      class="cat-dialog"
    >
      <el-form label-width="110px">
        <el-form-item label="代码"><el-input v-model="form.code" placeholder="如 DZ" :disabled="!!editingId" /></el-form-item>
        <el-form-item label="名称"><el-input v-model="form.name" /></el-form-item>
        <el-form-item label="默认保管期限"><el-input v-model="form.defaultRetention" placeholder="如 30年" /></el-form-item>
        <el-form-item label="说明"><el-input v-model="form.description" placeholder="可选" /></el-form-item>
      </el-form>

      <!-- 两级元数据模板：案卷级 / 卷内文件级，各自独立配置 -->
      <el-tabs v-model="tplTab" class="tpl-tabs">
        <el-tab-pane name="archive">
          <template #label>
            <span>案卷级模板 <el-tag size="small" type="info">{{ parsedCount(form.templateJson) }}</el-tag></span>
          </template>
          <div class="meta-block">
            <div class="meta-title">案卷级元数据模板（新建档案/案卷时填写）</div>
            <MetadataDesigner v-model="form.templateJson" />
          </div>
        </el-tab-pane>
        <el-tab-pane name="file">
          <template #label>
            <span>文件级模板 <el-tag size="small" type="success">{{ parsedCount(form.fileTemplateJson) }}</el-tag></span>
          </template>
          <div class="meta-block">
            <div class="meta-title">卷内文件级元数据模板（新建文件/件时填写）</div>
            <MetadataDesigner v-model="form.fileTemplateJson" />
          </div>
        </el-tab-pane>
      </el-tabs>

      <template #footer>
        <el-button @click="showCreate = false">取消</el-button>
        <el-button type="primary" @click="onSubmit">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { listCategories, createCategory, getCategory, updateCategory } from '@/api/category'
import MetadataDesigner from '@/components/MetadataDesigner.vue'
import type { CategoryDto, CreateCategoryDto } from '@/types/models'
import { ElMessage } from 'element-plus'

const router = useRouter()
const rows = ref<CategoryDto[]>([])
const showCreate = ref(false)
const editingId = ref<number | null>(null)
const form = ref<CreateCategoryDto>({
  code: '', name: '', templateJson: '[]', fileTemplateJson: '[]', defaultRetention: '', description: ''
})
const tplTab = ref('archive')

function parsedCount(json: string) {
  try { return JSON.parse(json || '[]').length } catch { return 0 }
}
async function load() {
  const { data } = await listCategories()
  rows.value = data.data
}
function goArchives(row: CategoryDto) {
  router.push({ path: '/archives', query: { categoryId: String(row.id), categoryName: row.name } })
}
function openCreate() {
  editingId.value = null
  tplTab.value = 'archive'
  form.value = {
    code: '', name: '', templateJson: '[]', fileTemplateJson: '[]', defaultRetention: '', description: ''
  }
  showCreate.value = true
}
async function openEdit(row: CategoryDto) {
  const { data } = await getCategory(row.id)
  const c = data.data
  form.value = {
    code: c.code, name: c.name,
    templateJson: c.templateJson || '[]',
    fileTemplateJson: c.fileTemplateJson || '[]',
    defaultRetention: c.defaultRetention, description: c.description
  }
  editingId.value = row.id
  tplTab.value = 'archive'
  showCreate.value = true
}
async function onSubmit() {
  if (editingId.value) await updateCategory(editingId.value, form.value)
  else await createCategory(form.value)
  showCreate.value = false
  ElMessage.success('已保存')
  load()
}
onMounted(load)
</script>

<!-- 非 scoped：el-dialog 会被 teleport 到 body，scoped 选择器命中不到 -->
<style>
.cat-dialog {
  max-width: 1400px;
  min-width: 720px;
}
.cat-dialog .el-dialog__body {
  padding-top: 12px;
}
</style>

<style scoped>
.meta-block {
  margin-top: 4px;
  padding: 10px 12px;
  border: 1px solid #e2e8f0;
  border-radius: 6px;
  background: #f8fafc;
}
.meta-title {
  font-size: 13px;
  color: #334155;
  font-weight: 600;
  margin-bottom: 8px;
}
</style>
