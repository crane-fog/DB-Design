<script setup lang="ts">
import {
  Bell,
  CollectionTag,
  DocumentChecked,
  List,
  Operation,
  TrendCharts,
  User,
  UserFilled,
} from '@element-plus/icons-vue'
import { type Component, computed, onMounted, onUnmounted, ref } from 'vue'
import type { DashboardShortcutIcon, HomeDashboardData } from '@/types/dashboard'
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
  pendingItems: Bell,
  roles: User,
  todayOperations: TrendCharts,
  users: UserFilled,
}

const visibleShortcuts = computed(() =>
  (dashboard.value?.shortcuts ?? []).filter((shortcut) => auth.hasPermission(shortcut.permission)),
)
const visibleStatistics = computed(() =>
  (dashboard.value?.statistics ?? []).filter((statistic) => canAccess(statistic.permission)),
)
const visibleTodos = computed(() =>
  (dashboard.value?.todos.items ?? []).filter((todo) => canAccess(todo.permission)),
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
    const result = await dashboardService.getHomeDashboard()
    if (!isUnmounted && currentRequestId === requestId) {
      dashboard.value = result
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

onMounted(() => void loadDashboard())
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
        :class="{
          'statistic-card--clickable': statistic.route && canAccess(statistic.permission),
          'statistic-card--disabled': statistic.route && !canAccess(statistic.permission),
        }"
        shadow="never"
        @click="navigateTo(statistic.route, statistic.permission)"
      >
        <div class="statistic-card__content">
          <div>
            <p class="statistic-card__title">{{ statistic.title }}</p>
            <strong>{{ formatNumber(statistic.value) }}</strong>
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
      <div v-if="loading && !dashboard" class="shortcut-grid">
        <el-skeleton v-for="index in 4" :key="index" animated>
          <template #template><el-skeleton-item variant="rect" style="height: 92px" /></template>
        </el-skeleton>
      </div>
      <el-empty
        v-else-if="!visibleShortcuts.length"
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
        <template #header><span>待办与提醒</span></template>
        <el-skeleton v-if="loading && !dashboard" :rows="4" animated />
        <el-empty
          v-else-if="!visibleTodos.length"
          :image-size="70"
          description="暂无待办或系统提醒"
        />
        <el-timeline v-else>
          <el-timeline-item
            v-for="todo in visibleTodos"
            :key="todo.id"
            :timestamp="formatDateTime(todo.createdAt)"
            :type="
              todo.type === 'warning' ? 'danger' : todo.type === 'reminder' ? 'warning' : 'primary'
            "
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
            <el-tag
              class="todo-status"
              size="small"
              :type="todo.status === 'resolved' ? 'success' : 'warning'"
            >
              {{
                todo.status === 'resolved'
                  ? '已处理'
                  : todo.status === 'processing'
                    ? '处理中'
                    : '待处理'
              }}
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
          description="暂无最近操作记录"
        />
        <el-table v-else :data="visibleOperations" size="small" stripe>
          <el-table-column label="操作人" min-width="100" prop="operatorName" />
          <el-table-column label="模块" min-width="100" prop="module" />
          <el-table-column label="操作内容" min-width="130" prop="action" />
          <el-table-column label="操作时间" min-width="165">
            <template #default="{ row }">{{ formatDateTime(row.operateTime) }}</template>
          </el-table-column>
          <el-table-column label="结果" min-width="72">
            <template #default="{ row }">
              <el-tag size="small" :type="row.result === 'success' ? 'success' : 'danger'">
                {{ row.result === 'success' ? '成功' : '失败' }}
              </el-tag>
            </template>
          </el-table-column>
        </el-table>
      </el-card>
    </div>
  </PageContainer>
</template>

<style scoped>
.dashboard-page {
  --mtf-blue: #5bcefa;
  --mtf-pink: #f5a9b8;
  --mtf-white: #ffffff;
  --mtf-pink-soft: #f9dce4;
  --mtf-pink-light: #fde7ed;
  --mtf-pink-panel: #fff3f6;
  --mtf-blue-soft: #d8f0fb;
  --mtf-blue-light: #e4f7fe;
  --mtf-blue-panel: #f1fafe;
  --mtf-pink-border: #f2bec9;
  --mtf-blue-border: #a9e1f7;
  --text-primary: #34324a;
  --text-secondary: #7a7890;
  --page-bg: #fcfbfd;
  --panel-bg: #ffffff;
  position: relative;
  min-height: calc(100vh - 40px);
  max-width: none;
  margin: -20px;
  padding: 30px clamp(20px, 3vw, 48px) 44px;
  overflow: hidden;
  color: var(--text-primary);
  background: var(--page-bg);
}

.dashboard-page > :deep(*) {
  position: relative;
  z-index: 1;
}
.dashboard-page :deep(.page-header) {
  align-items: center;
  margin-bottom: 26px;
}
.dashboard-page :deep(.page-header h1) {
  margin: 0;
  color: var(--text-primary);
  font-size: clamp(28px, 3vw, 36px);
  letter-spacing: -0.03em;
}
.dashboard-page :deep(.page-header h1)::after {
  display: inline-block;
  width: 26px;
  height: 8px;
  margin-left: 12px;
  border-radius: 999px;
  background: var(--mtf-pink);
  border-right: 26px solid var(--mtf-blue);
  content: '';
  vertical-align: middle;
}
.dashboard-page :deep(.page-header p) {
  margin: 10px 0 0;
  color: var(--text-secondary);
}
.dashboard-page :deep(.page-header-actions .el-button) {
  border-color: var(--mtf-blue);
  background: var(--mtf-blue);
  color: var(--mtf-white);
  box-shadow: none;
}
.dashboard-page :deep(.page-header-actions .el-button:hover) {
  border-color: #38b7e5;
  background: #38b7e5;
  color: var(--mtf-white);
}

.request-error {
  margin-bottom: 16px;
}
.dashboard-page :deep(.el-alert) {
  border: 1px solid rgb(248 113 113 / 46%);
  background: rgb(127 29 29 / 48%);
  color: #fee2e2;
}
.statistics-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 16px;
  margin-bottom: 16px;
}
.statistic-card {
  min-width: 0;
  border: 1px solid var(--mtf-pink-border);
  border-radius: 18px;
  background: var(--mtf-pink-soft);
  box-shadow: 0 6px 20px rgb(52 50 74 / 7%);
  overflow: hidden;
  position: relative;
}
.statistic-card::before {
  position: absolute;
  top: 0;
  right: 0;
  left: 0;
  height: 5px;
  background: var(--mtf-pink);
  content: '';
}
.statistic-card:nth-child(n + 3)::before {
  background: var(--mtf-blue);
}
.statistic-card:nth-child(4n + 1) {
  border-color: var(--mtf-pink-border);
  background: var(--mtf-pink-soft);
}
.statistic-card:nth-child(4n + 2) {
  border-color: var(--mtf-pink-border);
  background: var(--mtf-pink-soft);
}
.statistic-card:nth-child(4n + 3) {
  border-color: var(--mtf-blue-border);
  background: var(--mtf-blue-soft);
}
.statistic-card:nth-child(4n) {
  border-color: var(--mtf-blue-border);
  background: var(--mtf-blue-soft);
}
.statistic-card:nth-child(n + 3) .statistic-card__icon {
  background: var(--mtf-blue);
}
.dashboard-page :deep(.statistic-card .el-card__body) {
  padding: 22px;
}
.statistic-card--clickable {
  cursor: pointer;
  transition:
    border-color 0.2s,
    transform 0.2s;
}
.statistic-card--clickable:hover {
  border-color: var(--mtf-blue);
  box-shadow: 0 6px 20px rgb(52 50 74 / 10%);
  transform: translateY(-2px);
}
.statistic-card--disabled {
  opacity: 0.64;
}
.statistic-card__content {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}
.statistic-card__title,
.statistic-card__description {
  margin: 0;
  color: var(--text-secondary);
}
.statistic-card__content strong {
  display: block;
  margin: 8px 0;
  color: var(--text-primary);
  font-size: 28px;
  line-height: 1;
}
.statistic-card__description {
  font-size: 13px;
}
.statistic-card__icon {
  display: grid;
  width: 58px;
  height: 58px;
  place-items: center;
  flex: 0 0 auto;
  border-radius: 16px;
  background: var(--mtf-pink);
  color: var(--mtf-white);
  box-shadow: none;
}
.dashboard-section {
  min-width: 0;
  margin-bottom: 16px;
  border: 1px solid #ece7ef;
  border-radius: 18px;
  background: var(--panel-bg);
  box-shadow: 0 4px 16px rgb(52 50 74 / 6%);
  overflow: hidden;
}
.dashboard-section--shortcuts {
  background: var(--panel-bg);
}
.dashboard-section--todos {
  background: var(--mtf-pink-panel);
}
.dashboard-section--operations {
  background: var(--mtf-blue-panel);
}
.dashboard-page :deep(.dashboard-section .el-card__header) {
  border-bottom-color: #ece7ef;
  color: var(--text-primary);
  font-weight: 700;
  border-top: 3px solid var(--mtf-pink);
  background: var(--mtf-pink-soft);
}
.dashboard-page :deep(.dashboard-section--todos .el-card__header) {
  border-top-color: var(--mtf-pink);
  background: var(--mtf-pink-soft);
}
.dashboard-page :deep(.dashboard-section--operations .el-card__header) {
  border-top-color: var(--mtf-blue);
  background: var(--mtf-blue-soft);
}
.dashboard-page :deep(.dashboard-section--todos .el-card__body) {
  background: var(--mtf-pink-panel);
}
.dashboard-page :deep(.dashboard-section--operations .el-card__body) {
  background: var(--mtf-blue-panel);
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
  border: 1px solid #ece7ef;
  border-radius: 6px;
  background: var(--panel-bg);
  color: var(--text-primary);
  cursor: pointer;
  padding: 14px;
  text-align: left;
  border-left: 4px solid var(--mtf-pink);
}
.shortcut-entry:nth-child(odd) {
  background: var(--mtf-pink-light);
}
.shortcut-entry:hover {
  border-color: var(--mtf-pink);
  background: var(--mtf-pink-soft);
  color: var(--text-primary);
  transform: translateY(-2px);
}
.shortcut-entry {
  transition:
    border-color 0.2s,
    background 0.2s,
    transform 0.2s;
}
.shortcut-entry .el-icon {
  color: var(--mtf-pink);
}
.shortcut-entry:nth-child(even) .el-icon {
  color: var(--mtf-blue);
}
.shortcut-entry:nth-child(even) {
  border-left-color: var(--mtf-blue);
  background: var(--mtf-blue-light);
}
.shortcut-entry:nth-child(even):hover {
  border-color: var(--mtf-blue);
  background: var(--mtf-blue-soft);
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
  color: var(--mtf-pink);
  cursor: pointer;
  font: inherit;
  padding: 0;
  text-align: left;
}
.todo-status {
  margin-left: 8px;
}
.dashboard-page :deep(.el-timeline-item__timestamp) {
  color: var(--text-secondary);
}
.dashboard-page :deep(.el-timeline-item__content) {
  color: var(--text-primary);
}
.dashboard-page :deep(.el-timeline-item__tail) {
  border-left-color: var(--mtf-blue-soft);
}
.dashboard-page :deep(.el-table) {
  --el-table-border-color: #ece7ef;
  --el-table-header-bg-color: rgb(255 255 255 / 0%);
  --el-table-row-hover-bg-color: rgb(255 255 255 / 0%);
  --el-table-bg-color: rgb(255 255 255 / 0%);
  --el-table-tr-bg-color: var(--panel-bg);
  --el-table-text-color: var(--text-primary);
  --el-table-header-text-color: var(--text-primary);
  background: transparent;
}
.dashboard-page :deep(.el-table__inner-wrapper::before) {
  background-color: #ece7ef;
}
.dashboard-page :deep(.el-table th.el-table__cell) {
  border-top: 3px solid var(--mtf-blue);
  background: var(--mtf-blue-soft);
  color: var(--text-primary);
}
.dashboard-page :deep(.el-table tr > td.el-table__cell) {
  background: var(--panel-bg);
  color: var(--text-primary);
}
.dashboard-page
  :deep(.el-table--striped .el-table__body tr.el-table__row--striped > td.el-table__cell) {
  background: var(--mtf-blue-light);
}
.dashboard-page :deep(.el-table__body tr:hover > td.el-table__cell),
.dashboard-page :deep(.el-table__fixed-body-wrapper tr:hover > td.el-table__cell),
.dashboard-page :deep(.el-table__fixed-right tr:hover > td.el-table__cell) {
  background: var(--mtf-pink-light) !important;
  color: var(--text-primary) !important;
}
.dashboard-page :deep(.el-table td.el-table__cell),
.dashboard-page :deep(.el-table th.el-table__cell) {
  border-bottom-color: #ece7ef;
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
  .dashboard-page :deep(.page-header h1)::after {
    width: 28px;
    height: 6px;
  }
}
</style>
