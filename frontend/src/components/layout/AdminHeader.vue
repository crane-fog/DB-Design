<script setup lang="ts">
import { SwitchButton, UserFilled } from '@element-plus/icons-vue'
import { computed } from 'vue'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const userDisplayName = computed(
  () => auth.currentUser?.name || auth.currentUser?.employeeNo || '已登录用户',
)
const userInitial = computed(() => userDisplayName.value.slice(0, 1))

function handleUserCommand(command: string) {
  if (command === 'logout') {
    auth.logout()
  }
}
</script>

<template>
  <header class="admin-header">
    <div class="admin-header-brand">
      <span class="admin-brand-mark" aria-hidden="true">IM</span>
      <span class="admin-brand-title">工业制造物料管理系统</span>
    </div>

    <el-dropdown trigger="click" @command="handleUserCommand">
      <button class="user-summary" type="button" aria-label="用户菜单">
        <el-avatar class="user-avatar" :size="32">{{ userInitial }}</el-avatar>
        <span class="user-name">{{ userDisplayName }}</span>
        <el-icon class="user-menu-icon"><UserFilled /></el-icon>
      </button>
      <template #dropdown>
        <el-dropdown-menu>
          <el-dropdown-item command="logout" divided>
            <el-icon><SwitchButton /></el-icon>
            退出登录
          </el-dropdown-item>
        </el-dropdown-menu>
      </template>
    </el-dropdown>
  </header>
</template>
