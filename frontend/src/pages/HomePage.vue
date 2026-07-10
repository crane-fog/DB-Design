<script setup lang="ts">
import { Api, type UserData } from '@/api/client'
import { onMounted, ref } from 'vue'

const loading = ref(false)
const error = ref('')
const rows = ref<UserData[]>([])

async function loadRows() {
  loading.value = true
  error.value = ''
  try {
    const response = await Api.getUserTest()
    rows.value = response.data
  } catch (err: any) {
    error.value = err.response?.data?.message || err.message || '请求失败'
  } finally {
    loading.value = false
  }
}

onMounted(loadRows)
</script>

<template>
  <section class="page-placeholder">
    <h1>管理系统首页</h1>
    <p>
      整个管理系统的首页 <br /><br />
      这是一个调用后端 api 的示例，开发时确认自己电脑上 VS C# 后端在运行状态 <br /><br />
    </p>
  </section>
  <button type="button" @click="loadRows" :disabled="loading">
    {{ loading ? '加载中......' : '刷新数据' }}
  </button>

  <p v-if="error">{{ error }}</p>

  <table v-if="rows.length">
    <thead>
      <tr>
        <th>ID</th>
        <th>NAME</th>
        <th>CREATED_AT</th>
      </tr>
    </thead>
    <tbody>
      <tr v-for="row in rows" :key="row.id">
        <td>{{ row.id }}</td>
        <td>{{ row.name || '-' }}</td>
        <td>{{ row.createdAt ? new Date(row.createdAt).toLocaleString() : '-' }}</td>
      </tr>
    </tbody>
  </table>

  <p v-else-if="!loading && !error">暂无数据</p>
</template>
