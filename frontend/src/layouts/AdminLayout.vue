<script setup lang="ts">
import AdminHeader from '@/components/layout/AdminHeader.vue'
import AdminSidebar from '@/components/layout/AdminSidebar.vue'
</script>

<template>
  <div class="admin-layout">
    <AdminHeader />
    <AdminSidebar />
    <main class="admin-content">
      <RouterView />
    </main>
  </div>
</template>
