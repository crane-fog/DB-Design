<script setup lang="ts">
function goHome() {
  globalThis.location.assign('/')
}
</script>

<template>
  <main class="standalone-error-page">
    <p class="error-page-code">404</p>
    <h1>页面不存在</h1>
    <p>请检查访问地址，或返回工作台继续操作。</p>
    <button type="button" class="button" @click="goHome">返回工作台</button>
  </main>
</template>
