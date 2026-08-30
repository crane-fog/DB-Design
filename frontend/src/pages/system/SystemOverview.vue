<script setup lang="ts">
import { type Component, computed, onMounted, onUnmounted, ref } from 'vue'
import type { DashboardShortcutIcon, SystemDashboardData } from '@/types/dashboard'
import { DocumentChecked, Key, Operation, User, UserFilled } from '@element-plus/icons-vue'
import { formatDateTime, formatNumber } from '@/utils/format'
import PageContainer from '@/components/common/PageContainer.vue'
import PageHeader from '@/components/common/PageHeader.vue'
import { dashboardService } from '@/services/DashboardService'
import { getErrorMessage } from '@/utils/error'
import { useAuthStore } from '@/stores/auth'
import { useRouter } from 'vue-router'

const auth = useAuthStore()
const router = useRouter()
const loading = ref(false)
const error = ref('')
const dashboard = ref<SystemDashboardData>()
let isUnmounted = false
let requestId = 0

const shortcutIcons: Record<DashboardShortcutIcon, Component> = {
  audit: DocumentChecked,
  materials: Key,
  roles: User,
  users: UserFilled,
}

const visibleShortcuts = computed(() =>
  (dashboard.value?.shortcuts ?? []).filter((shortcut) => auth.hasPermission(shortcut.permission)),
)
const visibleStatistics = computed(() =>
  (dashboard.value?.statistics ?? []).filter((statistic) => canAccess(statistic.permission)),
)
const visibleOperations = computed(() =>
  (dashboard.value?.recentOperations.items ?? []).filter((operation) =>
    canAccess(operation.permission),
  ),
)

function canAccess(permission?: string) {
  return !permission || auth.hasPermission(permission)
}

function navigateTo(route?: string, permission?: string) {
  if (route && canAccess(permission)) {
    void router.push(route)
  }
}

async function loadDashboard() {
  const currentRequestId = ++requestId
  loading.value = true
  error.value = ''
  try {
    const result = await dashboardService.getSystemDashboard()
    if (!isUnmounted && currentRequestId === requestId) {
      dashboard.value = result
    }
  } catch (requestError) {
    if (!isUnmounted && currentRequestId === requestId) {
      error.value = getErrorMessage(requestError, '系统管理概览加载失败')
    }
  } finally {
    if (!isUnmounted && currentRequestId === requestId) {
      loading.value = false
    }
  }
}

onMounted(() => void loadDashboard())
onUnmounted(() => {
  isUnmounted = true
})
</script>

<template>
  <PageContainer>
    <PageHeader title="系统管理" description="维护账号、角色和审计信息，快速掌握系统治理状态。">
      <template #actions>
        <el-button :icon="Operation" :loading="loading" @click="loadDashboard">刷新数据</el-button>
      </template>
    </PageHeader>

    <el-alert
      v-if="error"
      :closable="false"
      show-icon
      :title="error"
      type="error"
      class="request-error"
    >
      <template #default
        ><el-button link type="primary" @click="loadDashboard">重新加载</el-button></template
      >
    </el-alert>

    <section class="statistics-grid" aria-label="系统管理统计">
      <el-card
        v-for="statistic in visibleStatistics"
        :key="statistic.title"
        :class="{ 'statistic-card--clickable': statistic.route && canAccess(statistic.permission) }"
        class="statistic-card"
        shadow="never"
        @click="navigateTo(statistic.route, statistic.permission)"
      >
        <p>{{ statistic.title }}</p>
        <strong>{{ formatNumber(statistic.value) }}</strong>
        <small>{{ statistic.description }}</small>
      </el-card>
      <el-skeleton
        v-if="loading && !dashboard"
        v-for="index in 4"
        :key="`skeleton-${index}`"
        animated
      >
        <template #template><el-skeleton-item variant="rect" style="height: 126px" /></template>
      </el-skeleton>
    </section>

    <el-card class="overview-card" shadow="never">
      <template #header><span>管理快捷入口</span></template>
      <div v-if="loading && !dashboard" class="shortcut-grid">
        <el-skeleton v-for="index in 3" :key="index" animated
          ><template #template><el-skeleton-item variant="rect" style="height: 90px" /></template
        ></el-skeleton>
      </div>
      <el-empty
        v-else-if="!visibleShortcuts.length"
        :image-size="70"
        description="当前账号暂无系统管理入口"
      />
      <div v-else class="shortcut-grid">
        <button
          v-for="shortcut in visibleShortcuts"
          :key="shortcut.route"
          class="shortcut-entry"
          type="button"
          @click="navigateTo(shortcut.route, shortcut.permission)"
        >
          <el-icon :size="25"><component :is="shortcutIcons[shortcut.icon]" /></el-icon>
          <span
            ><strong>{{ shortcut.title }}</strong
            ><small>{{ shortcut.description }}</small></span
          >
        </button>
      </div>
    </el-card>

    <el-card class="overview-card" shadow="never">
      <template #header>
        <div class="card-header">
          <span>最近系统操作</span
          ><el-button
            v-if="canAccess('system:audit:view')"
            link
            type="primary"
            @click="navigateTo('/system/audit-logs', 'system:audit:view')"
            >查看更多</el-button
          >
        </div>
      </template>
      <el-skeleton v-if="loading && !dashboard" :rows="4" animated />
      <el-empty
        v-else-if="!visibleOperations.length"
        :image-size="70"
        description="暂无系统操作记录"
      />
      <el-table v-else :data="visibleOperations" stripe>
        <el-table-column label="操作人" min-width="120" prop="operatorName" />
        <el-table-column label="业务模块" min-width="130" prop="module" />
        <el-table-column label="操作类型" min-width="140" prop="action" />
        <el-table-column label="操作时间" min-width="180"
          ><template #default="{ row }">{{
            formatDateTime(row.operateTime)
          }}</template></el-table-column
        >
        <el-table-column label="IP 地址" min-width="130" prop="ipAddress" />
      </el-table>
    </el-card>
  </PageContainer>
</template>

<style scoped>
.request-error {
  margin-bottom: 16px;
}
.statistics-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 16px;
  margin-bottom: 16px;
}
.statistic-card {
  min-width: 0;
}
.statistic-card p,
.statistic-card small {
  display: block;
  margin: 0;
  color: var(--el-text-color-secondary);
}
.statistic-card strong {
  display: block;
  margin: 12px 0 8px;
  font-size: 28px;
}
.statistic-card--clickable {
  cursor: pointer;
  transition:
    border-color 0.2s,
    transform 0.2s;
}
.statistic-card--clickable:hover {
  border-color: var(--el-color-primary);
  transform: translateY(-2px);
}
.overview-card {
  margin-bottom: 16px;
  min-width: 0;
}
.shortcut-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 12px;
}
.shortcut-entry {
  display: flex;
  align-items: center;
  gap: 12px;
  min-width: 0;
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 6px;
  background: var(--el-fill-color-lighter);
  color: var(--el-text-color-primary);
  cursor: pointer;
  padding: 14px;
  text-align: left;
}
.shortcut-entry:hover {
  border-color: var(--el-color-primary-light-5);
  color: var(--el-color-primary);
}
.shortcut-entry span {
  display: grid;
  gap: 5px;
  min-width: 0;
}
.shortcut-entry small {
  color: var(--el-text-color-secondary);
  font-size: 12px;
}
.card-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}
@media (max-width: 960px) {
  .statistics-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
  .shortcut-grid {
    grid-template-columns: 1fr;
  }
}
@media (max-width: 640px) {
  .statistics-grid {
    grid-template-columns: 1fr;
  }
}
</style>
