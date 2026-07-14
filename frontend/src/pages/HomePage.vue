<script setup lang="ts">
import EmptyState from '@/components/common/EmptyState.vue'
import PageContainer from '@/components/common/PageContainer.vue'
import PageHeader from '@/components/common/PageHeader.vue'
import PageLoading from '@/components/common/PageLoading.vue'
import { getErrorMessage } from '@/utils/error'
import { ref } from 'vue'
import { systemService } from '@/services/SystemService'

const loadingTestData = ref(false)
const testError = ref('')
const testRowCount = ref<number>()

async function loadTestData() {
  loadingTestData.value = true
  testError.value = ''
  try {
    const response = await systemService.getUserTest()
    testRowCount.value = response.data.length
  } catch (error) {
    testError.value = getErrorMessage(error)
  } finally {
    loadingTestData.value = false
  }
}
</script>

<template>
  <PageContainer>
    <PageHeader
      title="工作台"
      description="统计、待办、预警和业务记录将在对应数据接口接入后展示。"
    />

    <div class="dashboard-grid">
      <section
        v-for="title in ['统计概览', '待办事项', '预警信息', '最近业务记录']"
        :key="title"
        class="dashboard-card"
      >
        <h2>{{ title }}</h2>
        <p>数据接口待接入</p>
      </section>
    </div>

    <section class="content-card dashboard-test-card">
      <div>
        <h2>开发测试接口</h2>
        <p>保留原有 <code>/api/user-test</code> 调用入口，仅用于联调验证，不作为正式业务统计。</p>
      </div>
      <button
        class="button button--secondary"
        type="button"
        :disabled="loadingTestData"
        @click="loadTestData"
      >
        {{ loadingTestData ? '加载中...' : '验证接口' }}
      </button>
      <PageLoading v-if="loadingTestData" text="正在请求测试数据..." />
      <p v-else-if="testError" class="page-error">{{ testError }}</p>
      <p v-else-if="testRowCount !== undefined" class="test-result">
        接口响应 {{ testRowCount }} 条测试记录。
      </p>
      <EmptyState
        v-else
        title="尚未执行接口验证"
        description="测试数据不会展示为工作台业务数据。"
      />
    </section>
  </PageContainer>
</template>
