<script setup lang="ts">
import { type Component, computed, onMounted, ref } from 'vue'
import { Odometer, Refresh, Tickets, Tools, TrendCharts, Van } from '@element-plus/icons-vue'
import PageContainer from '@/components/common/PageContainer.vue'
import PageHeader from '@/components/common/PageHeader.vue'
import { formatNumber } from '@/utils/format'
import { getErrorMessage } from '@/utils/error'
import { productionService } from '@/services/ProductionService'
import { useAuthStore } from '@/stores/auth'
import { useRouter } from 'vue-router'

interface OverviewStat {
  description: string
  key: string
  permission: string
  route: string
  title: string
  value?: number
}

interface OverviewShortcut {
  description: string
  icon: Component
  permission: string
  route: string
  title: string
}

const auth = useAuthStore()
const router = useRouter()
const loading = ref(false)
const error = ref('')

const pendingOrders = ref<number>()
const inProgressOrders = ref<number>()
const runningLines = ref<number>()

const shortcuts: OverviewShortcut[] = [
  {
    description: '维护生产订单，审核、开工、完工与取消。',
    icon: Tickets,
    permission: 'production:orders',
    route: '/production/orders',
    title: '生产订单',
  },
  {
    description: '配置产品产能、生产线、线型与排产日历。',
    icon: Odometer,
    permission: 'production:capacity',
    route: '/production/capacity',
    title: '产能配置',
  },
  {
    description: '查看产线状态，上报故障并按编号处理。',
    icon: Tools,
    permission: 'production:breakdown',
    route: '/production/breakdown',
    title: '故障反馈',
  },
  {
    description: '处理外部订单、交付评估、产能检测与产线实时状态。',
    icon: Van,
    permission: 'production:view',
    route: '/production/operations',
    title: '生产运营',
  },
]

const statistics = computed<OverviewStat[]>(() => [
  {
    description: '等待审核的生产订单数量。',
    key: 'pending',
    permission: 'production:orders',
    route: '/production/orders',
    title: '待审核订单',
    value: pendingOrders.value,
  },
  {
    description: '正在生产中的订单数量。',
    key: 'in_progress',
    permission: 'production:orders',
    route: '/production/orders',
    title: '生产中订单',
    value: inProgressOrders.value,
  },
  {
    description: '当前处于运行状态的生产线数量。',
    key: 'running_lines',
    permission: 'production:capacity',
    route: '/production/capacity',
    title: '运行中产线',
    value: runningLines.value,
  },
])

const visibleShortcuts = computed(() =>
  shortcuts.filter((shortcut) => auth.hasPermission(shortcut.permission)),
)
const visibleStatistics = computed(() =>
  statistics.value.filter((statistic) => auth.hasPermission(statistic.permission)),
)

function canAccess(permission?: string) {
  return !permission || auth.hasPermission(permission)
}

function navigateTo(route?: string, permission?: string) {
  if (route && canAccess(permission)) {
    void router.push(route)
  }
}

async function loadOverview() {
  loading.value = true
  error.value = ''
  try {
    const tasks: Promise<void>[] = []
    if (auth.hasPermission('production:orders')) {
      tasks.push(
        productionService
          .listOrders({ page: 1, pageSize: 1, status: 'pending_review' })
          .then((result) => void (pendingOrders.value = result.total)),
        productionService
          .listOrders({ page: 1, pageSize: 1, status: 'in_progress' })
          .then((result) => void (inProgressOrders.value = result.total)),
      )
    }
    if (auth.hasPermission('production:capacity')) {
      tasks.push(
        productionService
          .listLines({ page: 1, pageSize: 1, status: 'running' })
          .then((result) => void (runningLines.value = result.total)),
      )
    }
    await Promise.all(tasks)
  } catch (requestError) {
    error.value = getErrorMessage(requestError, '生产管理概览加载失败')
  } finally {
    loading.value = false
  }
}

onMounted(() => void loadOverview())
</script>

<template>
  <PageContainer>
    <PageHeader title="生产管理" description="掌握生产订单、产能与产线运行状态，快速进入各项作业。">
      <template #actions>
        <el-button :icon="Refresh" :loading="loading" @click="loadOverview">刷新数据</el-button>
      </template>
    </PageHeader>

    <el-alert
      v-if="error"
      class="request-error"
      :closable="false"
      show-icon
      :title="error"
      type="error"
    >
      <template #default>
        <el-button link type="primary" @click="loadOverview">重新加载</el-button>
      </template>
    </el-alert>

    <section v-if="visibleStatistics.length" class="statistics-grid" aria-label="生产管理统计">
      <el-card
        v-for="statistic in visibleStatistics"
        :key="statistic.key"
        v-loading="loading"
        class="statistic-card statistic-card--clickable"
        shadow="never"
        @click="navigateTo(statistic.route, statistic.permission)"
      >
        <p>{{ statistic.title }}</p>
        <strong>{{ formatNumber(statistic.value) }}</strong>
        <small>{{ statistic.description }}</small>
      </el-card>
    </section>

    <el-card class="overview-card" shadow="never">
      <template #header>
        <div class="card-header">
          <el-icon><TrendCharts /></el-icon>
          <span>生产作业入口</span>
        </div>
      </template>
      <el-empty
        v-if="!visibleShortcuts.length"
        :image-size="70"
        description="当前账号暂无生产作业入口"
      />
      <div v-else class="shortcut-grid">
        <button
          v-for="shortcut in visibleShortcuts"
          :key="shortcut.route"
          class="shortcut-entry"
          type="button"
          @click="navigateTo(shortcut.route, shortcut.permission)"
        >
          <el-icon :size="25"><component :is="shortcut.icon" /></el-icon>
          <span>
            <strong>{{ shortcut.title }}</strong>
            <small>{{ shortcut.description }}</small>
          </span>
        </button>
      </div>
    </el-card>
  </PageContainer>
</template>

<style scoped>
.request-error {
  margin-bottom: 16px;
}
.statistics-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 16px;
  margin-bottom: 16px;
}
.overview-card {
  margin-bottom: 16px;
  min-width: 0;
}
.card-header {
  display: flex;
  align-items: center;
  gap: 8px;
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
@media (max-width: 960px) {
  .statistics-grid,
  .shortcut-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}
@media (max-width: 640px) {
  .statistics-grid,
  .shortcut-grid {
    grid-template-columns: 1fr;
  }
}
</style>
