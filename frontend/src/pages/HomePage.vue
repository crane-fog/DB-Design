<script setup lang="ts">
import {
  Bell,
  CollectionTag,
  DocumentChecked,
  List,
  Operation,
  User,
  UserFilled,
} from '@element-plus/icons-vue'
import { type Component, computed, onUnmounted, ref, watch } from 'vue'
import type { DashboardShortcutIcon, HomeDashboardData } from '@/types/dashboard'
import {
  dashboardService,
  hasDashboardPermission,
  homeDashboardShortcuts,
} from '@/services/DashboardService'
import { formatDateTime, formatNumber } from '@/utils/format'
import { PermissionCode } from '@/constants/permissions'
import type { PermissionCode as PermissionCodeValue } from '@/api'
import PageContainer from '@/components/common/PageContainer.vue'
import PageHeader from '@/components/common/PageHeader.vue'
import { getErrorMessage } from '@/utils/error'
import { useAuthStore } from '@/stores/auth'
import { useRouter } from 'vue-router'

const auth = useAuthStore()
const router = useRouter()
const loading = ref(false)
const error = ref('')
const dashboard = ref<HomeDashboardData>()
let isUnmounted = false
let requestId = 0

const shortcutIcons: Record<DashboardShortcutIcon, Component> = {
  audit: DocumentChecked,
  materials: CollectionTag,
  roles: User,
  users: UserFilled,
}

const statisticIcons: Record<string, Component> = {
  auditLogs: DocumentChecked,
  inventoryAlerts: Bell,
  purchaseReminders: Bell,
  roles: User,
  users: UserFilled,
}

const visibleShortcuts = computed(() =>
  homeDashboardShortcuts.filter((shortcut) => canAccess(shortcut.permission)),
)
const visibleStatistics = computed(() =>
  (dashboard.value?.statistics ?? []).filter((statistic) => canAccess(statistic.permission)),
)
const visibleTodos = computed(() =>
  (dashboard.value?.todos.items ?? []).filter((todo) => canAccess(todo.permission)),
)
const visibleOperations = computed(() => {
  if (!canAccess(PermissionCode.SystemAuditOperationView)) {
    return []
  }
  return dashboard.value?.recentOperations.items ?? []
})
const operationsEmptyText = computed(() => {
  if (!canAccess(PermissionCode.SystemAuditOperationView)) {
    return '当前账号无权查看操作日志'
  }
  if (!dashboard.value || dashboard.value.recentOperations.state === 'error') {
    return '操作日志未能加载，请重试'
  }
  return '暂无最近操作记录'
})
const todosEmptyText = computed(() => {
  if (
    !canAccess(PermissionCode.InventoryAlertView) &&
    !canAccess(PermissionCode.PurchaseOverdueView)
  ) {
    return '当前账号无权查看库存预警或采购提醒'
  }
  if (!dashboard.value || dashboard.value.todos.state === 'error') {
    return '待办未能加载，请重试'
  }
  if (dashboard.value.todos.state === 'partial') {
    return '已加载来源暂无待办，其他来源加载失败'
  }
  return '权限范围内暂无待处理库存预警或待催交采购提醒'
})

function canAccess(permission?: PermissionCodeValue) {
  return !permission || hasDashboardPermission(auth, permission)
}

function navigateTo(route?: string, permission?: PermissionCodeValue) {
  if (route && canAccess(permission)) {
    void router.push(route)
  }
}

async function loadDashboard() {
  const currentRequestId = ++requestId
  loading.value = true
  error.value = ''
  dashboard.value = undefined
  try {
    const result = await dashboardService.getHomeDashboard({
      permissions: [...auth.permissions],
    })
    if (!isUnmounted && currentRequestId === requestId) {
      dashboard.value = result
      error.value = result.errors.join('；')
    }
  } catch (requestError) {
    if (!isUnmounted && currentRequestId === requestId) {
      error.value = getErrorMessage(requestError, '工作台数据加载失败')
    }
  } finally {
    if (!isUnmounted && currentRequestId === requestId) {
      loading.value = false
    }
  }
}

watch(
  () => auth.permissions,
  () => void loadDashboard(),
  { deep: true, immediate: true },
)
onUnmounted(() => {
  isUnmounted = true
})
</script>

<template>
  <PageContainer class="dashboard-page">
    <PageHeader title="工作台" description="集中查看系统概况、待办提醒和最近操作。">
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
      <template #default>
        <el-button link type="primary" @click="loadDashboard">重新加载</el-button>
      </template>
    </el-alert>

    <section class="statistics-grid" aria-label="统计概览">
      <el-card
        v-for="statistic in visibleStatistics"
        :key="statistic.key"
        class="statistic-card"
        :class="[
          `statistic-card--${statistic.key}`,
          {
            'statistic-card--clickable': statistic.route && canAccess(statistic.permission),
            'statistic-card--disabled': statistic.route && !canAccess(statistic.permission),
          },
        ]"
        shadow="never"
        @click="navigateTo(statistic.route, statistic.permission)"
      >
        <div class="statistic-card__content">
          <div>
            <p class="statistic-card__title">{{ statistic.title }}</p>
            <strong>{{ formatNumber(statistic.value) }}</strong>
            <small v-if="statistic.value === undefined">加载失败</small>
            <p class="statistic-card__description">{{ statistic.description }}</p>
          </div>
          <el-icon class="statistic-card__icon" :size="34">
            <component :is="statisticIcons[statistic.key] ?? List" />
          </el-icon>
        </div>
      </el-card>
      <el-skeleton
        v-if="loading && !dashboard"
        v-for="index in 4"
        :key="`skeleton-${index}`"
        animated
      >
        <template #template><el-skeleton-item variant="rect" style="height: 140px" /></template>
      </el-skeleton>
    </section>

    <el-card class="dashboard-section dashboard-section--shortcuts" shadow="never">
      <template #header><span>快捷入口</span></template>
      <el-empty
        v-if="!visibleShortcuts.length"
        :image-size="70"
        description="当前账号暂无可用快捷入口"
      />
      <div v-else class="shortcut-grid">
        <button
          v-for="shortcut in visibleShortcuts"
          :key="shortcut.route"
          class="shortcut-entry"
          type="button"
          @click="navigateTo(shortcut.route, shortcut.permission)"
        >
          <el-icon :size="26"><component :is="shortcutIcons[shortcut.icon]" /></el-icon>
          <span
            ><strong>{{ shortcut.title }}</strong
            ><small>{{ shortcut.description }}</small></span
          >
        </button>
      </div>
    </el-card>

    <div class="dashboard-content-grid">
      <el-card class="dashboard-section dashboard-section--todos" shadow="never">
        <template #header><span>待办与提醒（各来源最多 5 条）</span></template>
        <el-skeleton v-if="loading && !dashboard" :rows="4" animated />
        <el-empty v-else-if="!visibleTodos.length" :image-size="70" :description="todosEmptyText" />
        <el-timeline v-else>
          <el-timeline-item
            v-for="todo in visibleTodos"
            :key="todo.id"
            :timestamp="formatDateTime(todo.createdAt)"
            :type="todo.type === 'warning' ? 'danger' : 'warning'"
          >
            <button
              v-if="todo.route && canAccess(todo.permission)"
              class="timeline-link"
              type="button"
              @click="navigateTo(todo.route, todo.permission)"
            >
              {{ todo.title }}
            </button>
            <span v-else>{{ todo.title }}</span>
            <el-tag class="todo-status" size="small" type="warning">
              {{ todo.statusLabel }}
            </el-tag>
          </el-timeline-item>
        </el-timeline>
      </el-card>

      <el-card class="dashboard-section dashboard-section--operations" shadow="never">
        <template #header><span>最近操作记录</span></template>
        <el-skeleton v-if="loading && !dashboard" :rows="4" animated />
        <el-empty
          v-else-if="!visibleOperations.length"
          :image-size="70"
          :description="operationsEmptyText"
        />
        <el-table v-else :data="visibleOperations" size="small" stripe>
          <el-table-column label="操作人编号" min-width="100">
            <template #default="{ row }">{{ row.operatorId ?? '-' }}</template>
          </el-table-column>
          <el-table-column label="模块" min-width="100" prop="module" />
          <el-table-column label="操作内容" min-width="130" prop="action" />
          <el-table-column label="操作时间" min-width="165">
            <template #default="{ row }">{{ formatDateTime(row.operateTime) }}</template>
          </el-table-column>
        </el-table>
      </el-card>
    </div>
  </PageContainer>
</template>

<style scoped>
.dashboard-page {
  --mtf-blue: #5bcefa;
  --target-blue: #3478f6;
  --target-purple: #6758c9;
  --target-orange: #e9a23b;
  --mtf-pink: #f5a9b8;
  --mtf-blue-strong: #269fd3;
  --mtf-pink-strong: #d96f87;
  --page-bg: #f7f8fc;
  --panel-bg: #ffffff;
  --text-primary: #25283d;
  --text-normal: #4f566b;
  --text-secondary: #6f778c;
  --text-muted: #9197a8;
  --border-color: rgb(160 175 200 / 32%);
  --divider-color: rgb(185 195 212 / 28%);
  --card-shadow: 0 6px 22px rgb(38 50 80 / 6.5%);
  position: relative;
  min-height: calc(100vh - 40px);
  max-width: none;
  margin: -20px;
  padding: 30px clamp(20px, 3vw, 48px) 44px;
  overflow: hidden;
  color: var(--text-primary);
  background: var(--dashboard-background);
}

.dashboard-page > :deep(*) {
  position: relative;
  z-index: 1;
}
.dashboard-page :deep(.page-header) {
  align-items: center;
  margin-bottom: 6px;
}
.dashboard-page :deep(.page-header h1) {
  margin: 0;
  color: var(--text-primary);
  font-size: clamp(28px, 3vw, 36px);
  letter-spacing: -0.03em;
}
.dashboard-page :deep(.page-header p) {
  margin: 10px 0 0;
  color: var(--text-secondary);
}
.dashboard-page :deep(.page-header-actions .el-button) {
  border-color: var(--target-blue);
  background: var(--target-blue);
  color: #ffffff;
  box-shadow: 0 3px 10px rgb(38 159 211 / 14%);
}
.dashboard-page :deep(.page-header-actions .el-button:hover) {
  border-color: #285fce;
  background: #285fce;
  color: #ffffff;
}

.request-error {
  margin-bottom: 16px;
}
.dashboard-page :deep(.el-alert) {
  border: 1px solid #fecdca;
  background: #fef3f2;
}
.statistics-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 16px;
  margin-bottom: 16px;
}
.dashboard-section {
  min-width: 0;
  margin-bottom: 16px;
  border: 1px solid var(--border-color);
  border-radius: 12px;
  background: rgb(255 255 255 / 82%);
  -webkit-backdrop-filter: blur(14px);
  backdrop-filter: blur(14px);
  box-shadow: var(--card-shadow);
  overflow: hidden;
}
.dashboard-page :deep(.dashboard-section .el-card__header) {
  border-bottom-color: var(--divider-color);
  color: var(--text-primary);
  font-weight: 700;
  border-top: 3px solid var(--mtf-blue);
  background: rgb(255 255 255 / 68%);
  -webkit-backdrop-filter: blur(10px);
  backdrop-filter: blur(10px);
}
.dashboard-page :deep(.dashboard-section--todos .el-card__header) {
  border-top-color: var(--mtf-pink);
}
.dashboard-page :deep(.dashboard-section .el-card__body) {
  color: var(--text-primary);
}
.shortcut-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 12px;
}
.shortcut-entry {
  display: flex;
  align-items: center;
  gap: 12px;
  min-width: 0;
  border: 1px solid rgb(165 178 200 / 32%);
  border-radius: 10px;
  background: rgb(255 255 255 / 72%);
  color: var(--text-primary);
  cursor: pointer;
  padding: 14px;
  text-align: left;
  border-left: 2px solid transparent;
}
.shortcut-entry:hover {
  border-color: rgb(91 206 250 / 55%);
  background: rgb(91 206 250 / 5%);
  color: var(--text-primary);
  transform: translateY(-1px);
}
.shortcut-entry {
  transition:
    border-color 0.2s,
    background 0.2s,
    transform 0.2s;
}
.shortcut-entry .el-icon {
  color: var(--target-purple);
}
.shortcut-entry:nth-child(2) .el-icon {
  color: var(--mtf-blue-strong);
}
.shortcut-entry:nth-child(3) .el-icon {
  color: var(--mtf-pink-strong);
}
.shortcut-entry:nth-child(4) .el-icon {
  color: var(--target-orange);
}
.shortcut-entry span {
  display: grid;
  gap: 5px;
  min-width: 0;
}
.shortcut-entry small {
  color: var(--text-secondary);
  font-size: 12px;
}
.shortcut-entry strong {
  color: #34394c;
  font-weight: 600;
}
.dashboard-content-grid {
  display: grid;
  grid-template-columns: minmax(0, 0.9fr) minmax(0, 1.6fr);
  gap: 16px;
}
.dashboard-content-grid .dashboard-section {
  margin-bottom: 0;
}
.timeline-link {
  border: 0;
  background: transparent;
  color: var(--mtf-blue-strong);
  cursor: pointer;
  font: inherit;
  padding: 0;
  text-align: left;
}
.todo-status {
  margin-left: 8px;
}
.dashboard-page :deep(.el-timeline-item__timestamp) {
  color: #646b7e;
}
.dashboard-page :deep(.el-timeline) {
  overflow: visible;
  padding-left: 0;
}
.dashboard-page :deep(.el-timeline-item) {
  padding-bottom: 16px;
}
.dashboard-page :deep(.el-timeline-item:last-child) {
  padding-bottom: 0;
}
.dashboard-page :deep(.el-timeline-item__content) {
  color: var(--text-primary);
}
.dashboard-page :deep(.el-timeline-item__wrapper) {
  top: 0;
  border-radius: 0 8px 8px 0;
  background: rgb(245 169 184 / 16%);
  padding: 8px 12px 8px 28px;
}
.dashboard-page :deep(.el-timeline-item__tail) {
  left: 4px;
  border-left-color: #dce3ec;
}
.dashboard-page :deep(.el-table) {
  --el-table-border-color: var(--divider-color);
  --el-table-header-bg-color: rgb(91 206 250 / 12%);
  --el-table-row-hover-bg-color: #f8fbfd;
  --el-table-bg-color: rgb(255 255 255 / 88%);
  --el-table-tr-bg-color: rgb(255 255 255 / 84%);
  --el-table-text-color: var(--text-normal);
  --el-table-header-text-color: #405166;
  background: rgb(255 255 255 / 84%);
  -webkit-backdrop-filter: blur(10px);
  backdrop-filter: blur(10px);
}
.dashboard-page :deep(.el-table__inner-wrapper::before) {
  background-color: var(--divider-color);
}
.dashboard-page :deep(.el-table th.el-table__cell) {
  border-top: 2px solid var(--mtf-blue);
  background: rgb(70 130 245 / 16%);
  color: #2f65c7;
}
.dashboard-page :deep(.el-table tr > td.el-table__cell) {
  background: rgb(255 255 255 / 76%);
  color: var(--text-normal);
}
.dashboard-page :deep(.el-table__body td:nth-child(4)) {
  color: #646b7e;
}
.dashboard-page
  :deep(.el-table--striped .el-table__body tr.el-table__row--striped > td.el-table__cell) {
  background: rgb(248 250 252 / 78%);
}
.dashboard-page :deep(.el-table__body tr:hover > td.el-table__cell),
.dashboard-page :deep(.el-table__fixed-body-wrapper tr:hover > td.el-table__cell),
.dashboard-page :deep(.el-table__fixed-right tr:hover > td.el-table__cell) {
  background: #f8fbfd !important;
  color: var(--text-primary) !important;
}
.dashboard-page :deep(.el-table td.el-table__cell),
.dashboard-page :deep(.el-table th.el-table__cell) {
  border-bottom-color: rgb(185 195 212 / 30%);
}
.dashboard-page :deep(.el-table__empty-text),
.dashboard-page :deep(.el-empty__description p) {
  color: var(--text-secondary);
}
@media (max-width: 1100px) {
  .statistics-grid,
  .shortcut-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
  .dashboard-content-grid {
    grid-template-columns: 1fr;
  }
}
@media (max-width: 640px) {
  .dashboard-page {
    margin: -16px;
    padding: 24px 16px 32px;
  }
  .statistics-grid,
  .shortcut-grid {
    grid-template-columns: 1fr;
  }
}
</style>
