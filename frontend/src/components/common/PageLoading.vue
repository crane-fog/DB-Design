<script setup lang="ts">
const { text = '加载中...' } = defineProps<{ text?: string }>()
</script>

<template>
  <div class="page-loading" role="status">
    <span class="page-loading-spinner" aria-hidden="true" />
    {{ text }}
  </div>
</template>
