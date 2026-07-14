<script setup lang="ts">
import { computed } from 'vue'

type StatusTone = 'danger' | 'info' | 'neutral' | 'success' | 'warning'

const { labels = {}, value } = defineProps<{
  labels?: Record<string, string>
  value?: string | null
}>()

const defaults: Record<string, { label: string; tone: StatusTone }> = {
  cancelled: { label: '已取消', tone: 'neutral' },
  completed: { label: '已完成', tone: 'success' },
  disabled: { label: '停用', tone: 'neutral' },
  draft: { label: '草稿', tone: 'neutral' },
  fault: { label: '故障', tone: 'danger' },
  in_progress: { label: '进行中', tone: 'info' },
  normal: { label: '正常', tone: 'success' },
  pending: { label: '待审核', tone: 'warning' },
  valid: { label: '启用', tone: 'success' },
}

const status = computed(() => {
  const key = value ?? ''
  const configured = defaults[key]
  return {
    label: labels[key] ?? configured?.label ?? (key || '-'),
    tone: configured?.tone ?? 'neutral',
  }
})
</script>

<template>
  <span class="status-tag" :class="`status-tag--${status.tone}`">{{ status.label }}</span>
</template>
