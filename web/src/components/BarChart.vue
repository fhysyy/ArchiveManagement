<template>
  <div class="bar-chart">
    <div v-if="!items || items.length === 0" class="empty">暂无数据</div>
    <div v-else class="rows">
      <div v-for="it in items" :key="it.key" class="row">
        <div class="label" :title="it.key">{{ it.key || '(空)' }}</div>
        <div class="track">
          <div class="fill" :style="{ width: pct(it.count) + '%', background: color }" />
        </div>
        <div class="value">{{ it.count }}</div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'

const props = withDefaults(
  defineProps<{ items: { key: string; count: number }[]; color?: string }>(),
  { color: '#409eff' }
)

const max = computed(() => Math.max(1, ...(props.items ?? []).map(i => i.count)))
function pct(v: number) { return Math.round((v / max.value) * 100) }
</script>

<style scoped>
.bar-chart { width: 100%; }
.empty { color: #94a3b8; font-size: 12px; padding: 8px 0; }
.rows { display: flex; flex-direction: column; gap: 6px; }
.row { display: flex; align-items: center; gap: 8px; font-size: 12px; }
.label { width: 92px; flex: none; color: #475569; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.track { flex: 1; background: #f1f5f9; border-radius: 3px; height: 14px; overflow: hidden; }
.fill { height: 100%; border-radius: 3px; transition: width .3s ease; }
.value { width: 44px; flex: none; text-align: right; color: #1e293b; font-variant-numeric: tabular-nums; }
</style>
