<template>
  <div class="meta-designer">
    <div class="hint">
      可视化配置本门类的元数据字段，无需手敲 JSON。标识用英文字段名（同门类内不重复）；选「下拉」类型时请添加选项。
    </div>

    <!-- 表格放在可横向滚动的容器里：宽度不够时在容器内滚动，绝不溢出到模态框外 -->
    <div class="table-wrap">
      <el-table :data="fields" border size="small" style="margin-top: 8px; min-width: 760px">
        <el-table-column label="#" width="56">
          <template #default="{ $index }">
            <el-button link :disabled="$index === 0" @click="move($index, -1)" title="上移">↑</el-button>
            <el-button link :disabled="$index === fields.length - 1" @click="move($index, 1)" title="下移">↓</el-button>
          </template>
        </el-table-column>
        <el-table-column label="标识 key" width="140">
          <template #default="{ row }">
            <el-input v-model="row.key" placeholder="如 file_no" />
          </template>
        </el-table-column>
        <el-table-column label="标题 label" width="140">
          <template #default="{ row }">
            <el-input v-model="row.label" placeholder="如 文号" />
          </template>
        </el-table-column>
        <el-table-column label="类型" width="104">
          <template #default="{ row }">
            <el-select v-model="row.type" @change="onTypeChange(row)">
              <el-option label="文本" value="text" />
              <el-option label="下拉" value="select" />
              <el-option label="日期" value="date" />
              <el-option label="数字" value="number" />
            </el-select>
          </template>
        </el-table-column>
        <el-table-column label="必填" width="60" align="center">
          <template #default="{ row }">
            <el-switch v-model="row.required" />
          </template>
        </el-table-column>
        <el-table-column label="下拉选项（仅下拉类型）" min-width="200">
          <template #default="{ row }">
            <el-select
              v-if="row.type === 'select'"
              v-model="row.enum"
              multiple
              filterable
              allow-create
              default-first-option
              placeholder="输入选项后回车添加"
              style="width: 100%"
            >
              <el-option v-for="o in (row.enum || [])" :key="o" :label="o" :value="o" />
            </el-select>
            <span v-else class="muted">—</span>
          </template>
        </el-table-column>
        <el-table-column label="操作" width="64" align="center">
          <template #default="{ $index }">
            <el-button type="danger" link @click="remove($index)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>
    </div>

    <el-button style="margin-top: 8px" @click="add">+ 添加字段</el-button>

    <el-collapse style="margin-top: 8px">
      <el-collapse-item title="查看生成的 JSON（自动同步）">
        <pre class="json-preview">{{ preview }}</pre>
      </el-collapse-item>
    </el-collapse>
  </div>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import type { TemplateField } from '@/types/models'

const props = defineProps<{ modelValue: string }>()
const emit = defineEmits<{ 'update:modelValue': [string] }>()

const fields = ref<TemplateField[]>([])

function parse(json: string): TemplateField[] {
  try {
    const arr = JSON.parse(json || '[]')
    return Array.isArray(arr) ? (arr as TemplateField[]) : []
  } catch {
    return []
  }
}
function serialize(list: TemplateField[]): string {
  return JSON.stringify(list)
}

// 外部 JSON → 内部字段；仅在内容与当前序列化不一致时才覆盖，避免回写循环
watch(
  () => props.modelValue,
  (v) => {
    const parsed = parse(v)
    if (serialize(parsed) !== serialize(fields.value)) fields.value = parsed
  },
  { immediate: true }
)

// 内部字段变化 → 写回 JSON
watch(
  fields,
  (list) => emit('update:modelValue', serialize(list)),
  { deep: true }
)

function add() {
  fields.value.push({ key: '', label: '', type: 'text', required: false, enum: [] })
}
function remove(i: number) {
  fields.value.splice(i, 1)
}
function move(i: number, dir: number) {
  const j = i + dir
  if (j < 0 || j >= fields.value.length) return
  const t = fields.value
  ;[t[i], t[j]] = [t[j], t[i]]
}
function onTypeChange(row: TemplateField) {
  if (row.type !== 'select') row.enum = []
}

const preview = ref('[]')
watch(fields, (list) => (preview.value = serialize(list)), { deep: true, immediate: true })
</script>

<style scoped>
.hint { color: #64748b; font-size: 12px; line-height: 1.6; }
.muted { color: #cbd5e1; }
/* 关键：容器横向滚动，表格永不撑破父级（弹窗）宽度 */
.table-wrap {
  width: 100%;
  overflow-x: auto;
}
.json-preview {
  background: #0f172a; color: #e2e8f0; padding: 10px; border-radius: 6px;
  font-size: 12px; max-height: 180px; overflow: auto; margin: 0;
  white-space: pre-wrap;   /* 长 JSON 自动换行，不横向溢出 */
  word-break: break-all;
}
</style>
