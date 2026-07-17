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
  <PageContainer>
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

    <el-card class="dashboard-section" shadow="never">
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
      <el-card class="dashboard-section" shadow="never">
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

      <el-card class="dashboard-section" shadow="never">
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
  color: var(--el-text-color-secondary);
}
.statistic-card__content strong {
  display: block;
  margin: 8px 0;
  color: var(--el-text-color-primary);
  font-size: 28px;
  line-height: 1;
}
.statistic-card__description {
  font-size: 13px;
}
.statistic-card__icon {
  color: var(--el-color-primary-light-3);
}
.dashboard-section {
  min-width: 0;
  margin-bottom: 16px;
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
  color: var(--el-color-primary);
  cursor: pointer;
  font: inherit;
  padding: 0;
  text-align: left;
}
.todo-status {
  margin-left: 8px;
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
  .statistics-grid,
  .shortcut-grid {
    grid-template-columns: 1fr;
  }
}
</style>
