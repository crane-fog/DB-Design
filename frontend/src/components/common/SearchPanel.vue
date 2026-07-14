<script setup lang="ts">
const { title = '查询条件' } = defineProps<{ title?: string }>()
</script>

<template>
  <section class="search-panel" :aria-label="title">
    <div class="search-panel-title">{{ title }}</div>
    <div class="search-panel-body"><slot /></div>
    <div v-if="$slots.actions" class="search-panel-actions"><slot name="actions" /></div>
  </section>
</template>
