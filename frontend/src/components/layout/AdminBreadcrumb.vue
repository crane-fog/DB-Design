<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'

const route = useRoute()
const currentTitle = computed(() => route.meta.title ?? '工作台')
const isHomeRoute = computed(() => route.name === 'home')
</script>

<template>
  <el-breadcrumb class="admin-breadcrumb" separator="/" aria-label="面包屑">
    <el-breadcrumb-item v-if="!isHomeRoute" :to="{ path: '/' }">工作台</el-breadcrumb-item>
    <el-breadcrumb-item>{{ currentTitle }}</el-breadcrumb-item>
  </el-breadcrumb>
</template>
