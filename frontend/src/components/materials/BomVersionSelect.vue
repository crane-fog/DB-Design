<script setup lang="ts">
import type { MaterialBomListItem } from '@/types/material'

const { modelValue, options } = defineProps<{
  modelValue: string
  options: MaterialBomListItem[]
}>()

const emit = defineEmits<{
  change: [value: string]
  'update:modelValue': [value: string]
}>()

function updateModelValue(value: string) {
  emit('update:modelValue', value)
}

function handleChange(value: string) {
  emit('change', value)
}
</script>

<template>
  <el-form-item label="产品">
    <el-select
      filterable
      :model-value="modelValue"
      placeholder="选择产品 BOM 版本"
      style="width: 360px"
      @change="handleChange"
      @update:model-value="updateModelValue"
    >
      <el-option
        v-for="bom in options"
        :key="bom.bomId"
        :label="`${bom.materialName} ${bom.version} #${bom.materialCode}`"
        :value="bom.bomId"
      />
    </el-select>
  </el-form-item>
</template>
