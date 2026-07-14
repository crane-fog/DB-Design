<script setup lang="ts">
const { description = '调整查询条件后重试，或等待数据接口接入。', title = '暂无数据' } =
  defineProps<{ description?: string; title?: string }>()
</script>

<template>
  <div class="empty-state">
    <strong>{{ title }}</strong>
    <p>{{ description }}</p>
    <slot />
  </div>
</template>
