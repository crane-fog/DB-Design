<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { Api } from '@/api/client'
import type { UserData } from '@/api'

const loading = ref(false)
const error = ref('')
const rows = ref<UserData[]>([])

async function loadRows() {
  loading.value = true
  error.value = ''
  const startTime = Date.now()
  const end_time = Date.now()
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
  <main class="page">
    <h1>数据库查询结果</h1>

    <button type="button" @click="loadRows" :disabled="loading">
      {{ loading ? '加载中......' : '刷新数据' }}
    </button>

    <p v-if="error" class="error">{{ error }}</p>

    <table v-if="rows.length" class="table">
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
  </main>
</template>

<style scoped>
.page {
  max-width: 960px;
  margin: 32px auto;
  padding: 0 16px;
}

h1 {
  margin: 0 0 12px;
}

button {
  margin-bottom: 12px;
  padding: 8px 14px;
  border: 1px solid #aaa;
  border-radius: 6px;
  background: #fff;
  cursor: pointer;
}

button:disabled {
  cursor: not-allowed;
  opacity: 0.6;
}

.error {
  color: #c62828;
}

.table {
  width: 100%;
  border-collapse: collapse;
}

.table th,
.table td {
  padding: 8px;
  border: 1px solid #ddd;
  text-align: left;
}
</style>
