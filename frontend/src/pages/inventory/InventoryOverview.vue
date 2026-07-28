<script setup lang="ts">
import { Bell, Box, DataAnalysis, Refresh, TrendCharts, Van } from '@element-plus/icons-vue'
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import type { InventoryOverviewSummary } from '@/types/inventory'
import PageContainer from '@/components/common/PageContainer.vue'
import PageHeader from '@/components/common/PageHeader.vue'
import { formatNumber } from '@/utils/format'
import { getErrorMessage } from '@/utils/error'
import { inventoryService } from '@/services/InventoryService'
import { useAuthStore } from '@/stores/auth'
import { useRouter } from 'vue-router'

const auth = useAuthStore()
const router = useRouter()
const loading = ref(false)
const error = ref('')
const summary = ref<InventoryOverviewSummary>()
let requestId = 0

const shortcuts = [
  {
    description: '按 BOM 展开需求并核对可用、在途与安全库存。',
    icon: DataAnalysis,
    permission: 'inventory:calc',
    route: '/inventory/calc',
    title: '物料缺口计算',
  },
  {
    description: '处理低库存预警、库存锁定与呆滞物料。',
    icon: Bell,
    permission: 'inventory:monitor',
    route: '/inventory/monitor',
    title: '库存监控',
  },
  {
    description: '登记生产订单完工批次并查询入库记录。',
    icon: Van,
    permission: 'inventory:register',
    route: '/inventory/register',
    title: '完工入库',
  },
]

const visibleShortcuts = computed(() =>
  shortcuts.filter(({ permission }) => auth.hasPermission(permission)),
)

const statistics = computed(() => [
  {
    label: '待处理预警',
    tone: 'blue',
    value: summary.value?.pendingAlertCount,
  },
  {
    label: '有效锁定记录',
    tone: 'pink',
    value: summary.value?.lockedCount,
  },
  {
    label: '待处理呆滞物料',
    tone: 'blue',
    value: summary.value?.obsoletePendingCount,
  },
  {
    label: '累计完工入库',
    tone: 'pink',
    value: summary.value?.inboundCount,
  },
])

async function loadOverview() {
  const currentRequestId = ++requestId
  loading.value = true
  error.value = ''
  try {
    const result = await inventoryService.getOverview()
    if (currentRequestId === requestId) {
      summary.value = result
    }
  } catch (requestError) {
    if (currentRequestId === requestId) {
      error.value = getErrorMessage(requestError, '库存概览加载失败')
    }
  } finally {
    if (currentRequestId === requestId) {
      loading.value = false
    }
  }
}

function navigateTo(path: string) {
  void router.push(path)
}

onMounted(() => void loadOverview())
onBeforeUnmount(() => void requestId++)
</script>

<template>
  <PageContainer>
    <PageHeader
      title="库存管理"
      description="汇总库存风险与作业状态，快速进入缺口、监控和入库流程。"
    >
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

    <section class="inventory-statistics" aria-label="库存统计">
      <el-card
        v-for="statistic in statistics"
        :key="statistic.label"
        v-loading="loading"
        class="inventory-statistic"
        :class="`inventory-statistic--${statistic.tone}`"
        shadow="never"
      >
        <span>{{ statistic.label }}</span>
        <strong>{{ formatNumber(statistic.value) }}</strong>
      </el-card>
    </section>

    <el-card class="operation-card" shadow="never">
      <template #header>
        <div class="card-title">
          <el-icon><TrendCharts /></el-icon><span>库存作业入口</span>
        </div>
      </template>
      <el-empty
        v-if="!visibleShortcuts.length"
        :image-size="72"
        description="当前账号暂无库存作业权限"
      />
      <div v-else class="operation-grid">
        <button
          v-for="(shortcut, index) in visibleShortcuts"
          :key="shortcut.route"
          class="operation-entry"
          :class="index % 2 ? 'operation-entry--pink' : 'operation-entry--blue'"
          type="button"
          @click="navigateTo(shortcut.route)"
        >
          <span class="operation-icon"
            ><el-icon :size="24"><component :is="shortcut.icon" /></el-icon
          ></span>
          <span>
            <strong>{{ shortcut.title }}</strong>
            <small>{{ shortcut.description }}</small>
          </span>
        </button>
      </div>
    </el-card>

    <el-card class="flow-card" shadow="never">
      <div class="flow-note">
        <el-icon :size="22"><Box /></el-icon>
        <span
          ><strong>推荐作业顺序</strong
          ><small>计算缺口 → 生成采购草稿 → 跟踪到货 → 锁定生产库存 → 完工入库</small></span
        >
      </div>
    </el-card>
  </PageContainer>
</template>

<style scoped>
.request-error,
.inventory-statistics,
.operation-card {
  margin-bottom: 16px;
}
.inventory-statistics {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 14px;
}
.inventory-statistic {
  min-width: 0;
  border-top-width: 3px;
}
.inventory-statistic--blue {
  border-top-color: var(--primary-color);
  background: var(--app-background);
}
.inventory-statistic--pink {
  border-top-color: var(--border-color);
  background: var(--card-background);
}
.inventory-statistic span {
  color: var(--el-text-color-secondary);
}
.inventory-statistic strong {
  display: block;
  margin-top: 10px;
  font-size: 28px;
}
.card-title,
.flow-note {
  display: flex;
  align-items: center;
  gap: 9px;
}
.operation-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 12px;
}
.operation-entry {
  display: flex;
  align-items: center;
  gap: 12px;
  min-width: 0;
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 6px;
  color: var(--el-text-color-primary);
  cursor: pointer;
  padding: 16px;
  text-align: left;
}
.operation-entry--blue {
  background: var(--app-background);
}
.operation-entry--pink {
  background: var(--card-background);
}
.operation-entry:hover {
  border-color: var(--el-color-primary-light-3);
}
.operation-icon {
  display: grid;
  flex: 0 0 42px;
  width: 42px;
  height: 42px;
  place-items: center;
  border-radius: 6px;
  background: rgb(255 255 255 / 78%);
  color: var(--el-color-primary);
}
.operation-entry > span:last-child,
.flow-note > span {
  display: grid;
  min-width: 0;
  gap: 4px;
}
.operation-entry small,
.flow-note small {
  color: var(--el-text-color-secondary);
  font-size: 12px;
}
.flow-card {
  background: var(--app-background);
}
@media (max-width: 960px) {
  .inventory-statistics,
  .operation-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}
@media (max-width: 640px) {
  .inventory-statistics,
  .operation-grid {
    grid-template-columns: 1fr;
  }
}
</style>
