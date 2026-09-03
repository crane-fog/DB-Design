<script setup lang="ts">
const { description, title } = defineProps<{ description?: string; title: string }>()
</script>

<template>
  <header class="page-header" :aria-label="title">
    <el-tooltip
      v-if="description"
      :content="description"
      placement="bottom-start"
      popper-class="page-header-description-tooltip"
    >
      <span class="page-header-info" :aria-label="description" role="img" tabindex="0"> i </span>
    </el-tooltip>
    <div v-if="$slots.actions" class="page-header-actions">
      <slot name="actions" />
    </div>
  </header>
</template>
