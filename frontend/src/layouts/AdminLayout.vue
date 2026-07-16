<script setup lang="ts">
import AdminHeader from '@/components/layout/AdminHeader.vue'
import AdminSidebar from '@/components/layout/AdminSidebar.vue'
import { ref } from 'vue'

const collapsed = ref(false)
</script>

<template>
  <div class="admin-layout" :class="{ 'admin-layout--collapsed': collapsed }">
    <AdminHeader :collapsed="collapsed" @toggle-sidebar="collapsed = !collapsed" />
    <AdminSidebar :collapsed="collapsed" />
    <main class="admin-content">
      <RouterView />
    </main>
  </div>
</template>
