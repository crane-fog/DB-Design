<script setup lang="ts">
const { confirmText, message, open, title } = defineProps<{
  confirmText?: string
  message: string
  open: boolean
  title?: string
}>()

const emit = defineEmits<{ cancel: []; confirm: [] }>()
</script>

<template>
  <div v-if="open" class="dialog-backdrop" role="presentation" @click.self="emit('cancel')">
    <section
      class="confirm-dialog"
      aria-modal="true"
      role="dialog"
      :aria-label="title ?? '确认操作'"
    >
      <h2>{{ title ?? '确认操作' }}</h2>
      <p>{{ message }}</p>
      <footer>
        <button type="button" class="button button--secondary" @click="emit('cancel')">取消</button>
        <button type="button" class="button button--danger" @click="emit('confirm')">
          {{ confirmText ?? '确认' }}
        </button>
      </footer>
    </section>
  </div>
</template>
