<script setup lang="ts">
import PageContainer from '@/components/common/PageContainer.vue'
import { useRouter } from 'vue-router'

const router = useRouter()
</script>

<template>
  <PageContainer>
    <section class="error-page">
      <p class="error-page-code">403</p>
      <h1>暂无访问权限</h1>
      <p>当前账号没有访问此资源的权限。如需开通，请联系系统管理员。</p>
      <button type="button" class="button" @click="router.push('/')">返回工作台</button>
    </section>
  </PageContainer>
</template>
