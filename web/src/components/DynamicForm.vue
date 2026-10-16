<template>
  <el-form label-width="110px" style="margin-top: 8px">
    <el-form-item v-for="f in fields" :key="f.key" :label="f.label">
      <el-input
        v-if="f.type !== 'select'"
        v-model="model[f.key]"
        :placeholder="f.label"
      />
      <el-select v-else v-model="model[f.key]" placeholder="请选择" style="width: 100%">
        <el-option v-for="o in f.enum" :key="o" :label="o" :value="o" />
      </el-select>
    </el-form-item>
  </el-form>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import type { TemplateField } from '@/types/models'

const props = defineProps<{ template: string; modelValue: Record<string, unknown> }>()
const emit = defineEmits<{ 'update:modelValue': [Record<string, unknown>] }>()

const fields = ref<TemplateField[]>([])
const model = ref<Record<string, unknown>>({ ...props.modelValue })

watch(
  () => props.template,
  (t) => {
    try { fields.value = JSON.parse(t || '[]') } catch { fields.value = [] }
  },
  { immediate: true }
)

watch(
  model,
  (v) => emit('update:modelValue', v),
  { deep: true }
)
</script>
