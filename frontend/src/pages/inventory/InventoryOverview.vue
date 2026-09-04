<script setup lang="ts">
import { Bell, Box, DataAnalysis, Refresh, Search, TrendCharts, Van } from '@element-plus/icons-vue'
import type {
  InventoryOverviewSummary,
  InventoryStockQuery,
  InventoryStockStatus,
} from '@/types/inventory'
import { type InventoryStockData, inventoryService } from '@/services/InventoryService'
import { computed, onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import EmptyState from '@/components/common/EmptyState.vue'
import PageContainer from '@/components/common/PageContainer.vue'
import PageHeader from '@/components/common/PageHeader.vue'
import { formatNumber } from '@/utils/format'
import { getErrorMessage } from '@/utils/error'
import { useRouter } from 'vue-router'

const router = useRouter()
const loading = ref(false)
const error = ref('')
const summary = ref<Partial<InventoryOverviewSummary>>()
const canBrowseCatalog = computed(() => inventoryService.canReadMaterialReferences())
let requestId = 0
const stockLoading = ref(false)
const stockError = ref('')
const stockItems = ref<InventoryStockData[]>([])
const stockTotal = ref(0)
const stockQuery = reactive<InventoryStockQuery>({ page: 1, pageSize: 10 })
let stockRequestId = 0
const stockDetail = ref<InventoryStockData>()
const stockDetailError = ref('')
const stockDetailLoading = ref(false)

const materialTypeLabels = {
  auxiliary: '辅料',
  finished: '成品',
  raw_material: '原材料',
  semi_finished: '半成品',
}
type VisibleInventoryStockStatus = Exclude<InventoryStockStatus, 'locked'>

const stockStatusLabels: Record<VisibleInventoryStockStatus, string> = {
  low: '低库存',
  normal: '正常',
  zero: '零库存',
}

function formatStockDateTime(value?: string | null) {
  return value?.replace('T', ' ') || '-'
}

const shortcuts = [
  {
    description: '按 BOM 展开需求并核对可用、在途与安全库存。',
    icon: DataAnalysis,
    route: '/inventory/calc',
    title: '物料缺口计算',
  },
  {
    description: '处理低库存预警、库存锁定与呆滞物料。',
    icon: Bell,
    route: '/inventory/monitor',
    title: '库存监控',
  },
  {
    description: '登记生产订单完工批次并查询入库记录。',
    icon: Van,
    route: '/inventory/register',
    title: '完工入库',
  },
]

const statistics = computed(() => [
  {
    label: '物料总数',
    value: summary.value?.materialCount,
  },
  {
    label: '有库存物料',
    value: summary.value?.availableMaterialCount,
  },
  {
    label: '已锁定物料',
    value: summary.value?.lockedMaterialCount,
  },
  {
    label: '低库存物料',
    value: summary.value?.lowStockCount,
  },
  {
    label: '零库存物料',
    value: summary.value?.zeroStockCount,
  },
  {
    label: '待处理预警',
    value: summary.value?.pendingAlertCount,
  },
  {
    label: '待处理呆滞物料',
    value: summary.value?.obsoletePendingCount,
  },
  {
    label: '累计完工入库',
    value: summary.value?.inboundCount,
  },
])
const refreshing = computed(() => loading.value || stockLoading.value)

function getStockTagType(status?: InventoryStockStatus) {
  if (status === 'locked') {
    return 'success'
  }
  if (status === 'normal') {
    return 'success'
  }
  if (status === 'low') {
    return 'warning'
  }
  if (status === 'zero') {
    return 'danger'
  }
  return 'info'
}

function getMaterialTypeLabel(item: InventoryStockData) {
  if (item.materialType) {
    return materialTypeLabels[item.materialType]
  }
  return '-'
}

function getStockStatusLabel(item: InventoryStockData) {
  if (item.status === 'locked') {
    return stockStatusLabels.normal
  }
  if (item.status) {
    return stockStatusLabels[item.status]
  }
  return '-'
}

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

async function loadStocks() {
  const currentRequestId = ++stockRequestId
  stockLoading.value = true
  stockError.value = ''
  try {
    const result = await inventoryService.listStocks({
      ...stockQuery,
      materialName: stockQuery.materialName?.trim() || undefined,
    })
    if (currentRequestId !== stockRequestId) {
      return
    }
    stockItems.value = result.items
    stockTotal.value = result.total
  } catch (requestError) {
    if (currentRequestId === stockRequestId) {
      stockError.value = getErrorMessage(requestError, '库存台账加载失败')
    }
  } finally {
    if (currentRequestId === stockRequestId) {
      stockLoading.value = false
    }
  }
}

function refreshAll() {
  void Promise.all([loadOverview(), loadStocks()])
}

function searchStocks() {
  stockQuery.page = 1
  void loadStocks()
}

function resetStockQuery() {
  Object.assign(stockQuery, {
    materialId: undefined,
    materialName: undefined,
    materialType: undefined,
    page: 1,
    status: undefined,
  })
  void loadStocks()
}

async function viewStockDetail(materialId: number) {
  stockDetail.value = undefined
  stockDetailError.value = ''
  stockDetailLoading.value = true
  try {
    stockDetail.value = await inventoryService.getStockDetail(materialId)
  } catch (requestError) {
    stockDetailError.value = getErrorMessage(requestError, '库存详情加载失败')
  } finally {
    stockDetailLoading.value = false
  }
}

function closeStockDetail() {
  stockDetail.value = undefined
  stockDetailError.value = ''
}

function navigateTo(path: string) {
  void router.push(path)
}

onMounted(refreshAll)
onBeforeUnmount(() => {
  requestId += 1
  stockRequestId += 1
})
</script>

<template>
  <PageContainer>
    <PageHeader
      title="库存管理"
      description="汇总库存风险与作业状态，快速进入缺口、监控和入库流程。"
    >
      <template #actions>
        <el-button :icon="Refresh" :loading="refreshing" @click="refreshAll">刷新数据</el-button>
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
        class="inventory-statistic statistic-card"
        shadow="never"
      >
        <span class="statistic-card__title">{{ statistic.label }}</span>
        <strong>{{ formatNumber(statistic.value) }}</strong>
      </el-card>
    </section>

    <el-alert
      v-if="stockError"
      class="request-error"
      :closable="false"
      show-icon
      :title="stockError"
      type="error"
    >
      <template #default>
        <el-button link type="primary" @click="loadStocks">重新加载台账</el-button>
      </template>
    </el-alert>

    <el-card class="stock-card table-card table-card--accent" shadow="never">
      <template #header>
        <div class="card-title">
          <el-icon><Box /></el-icon><span>物料库存台账</span>
        </div>
      </template>
      <el-form class="stock-filter-form" inline @submit.prevent="searchStocks">
        <el-form-item label="物料编号">
          <el-input-number
            :controls="false"
            v-model="stockQuery.materialId"
            :min="1"
            placeholder="请输入编号"
          />
        </el-form-item>
        <el-form-item v-if="canBrowseCatalog" label="物料名称">
          <el-input
            v-model.trim="stockQuery.materialName"
            clearable
            placeholder="请输入名称"
            @keyup.enter="searchStocks"
          />
        </el-form-item>
        <el-form-item v-if="canBrowseCatalog" label="物料类型">
          <el-select v-model="stockQuery.materialType" clearable placeholder="全部">
            <el-option
              v-for="(label, value) in materialTypeLabels"
              :key="value"
              :label="label"
              :value="value"
            />
          </el-select>
        </el-form-item>
        <el-form-item v-if="canBrowseCatalog" label="库存状态">
          <el-select v-model="stockQuery.status" clearable placeholder="全部">
            <el-option
              v-for="(label, value) in stockStatusLabels"
              :key="value"
              :label="label"
              :value="value"
            />
          </el-select>
        </el-form-item>
        <el-form-item>
          <el-button :icon="Search" :loading="stockLoading" type="primary" @click="searchStocks"
            >查询</el-button
          >
          <el-button @click="resetStockQuery">重置</el-button>
        </el-form-item>
      </el-form>
      <div v-loading="stockLoading" class="stock-table-area">
        <EmptyState
          v-if="!stockLoading && !stockError && !stockItems.length"
          :description="
            !canBrowseCatalog && !stockQuery.materialId
              ? '输入物料编号查询库存。'
              : '当前查询条件下没有库存记录。'
          "
        />
        <el-table v-else :data="stockItems" stripe>
          <el-table-column label="物料" min-width="210">
            <template #default="{ row }">
              <div class="material-cell">
                <strong>{{ row.materialName }}</strong>
                <small>#{{ row.materialId }} · {{ getMaterialTypeLabel(row) }}</small>
              </div>
            </template>
          </el-table-column>
          <el-table-column label="总库存" width="70">
            <template #default="{ row }"
              ><strong>{{ formatNumber(row.availableQty + row.lockedQty) }}</strong>
              {{ row.unit || '' }}</template
            >
          </el-table-column>
          <el-table-column label="=" width="20" />
          <el-table-column label="可用数量" width="80">
            <template #default="{ row }"
              ><strong>{{ formatNumber(row.availableQty) }}</strong> {{ row.unit || '' }}</template
            >
          </el-table-column>
          <el-table-column label="+" width="20" />
          <el-table-column label="锁定数量" min-width="80">
            <template #default="{ row }"
              >{{ formatNumber(row.lockedQty) }} {{ row.unit || '' }}</template
            >
          </el-table-column>
          <el-table-column label="安全库存" min-width="80">
            <template #default="{ row }"
              >{{ formatNumber(row.safetyStock) }} {{ row.unit || '' }}</template
            >
          </el-table-column>
          <el-table-column label="库存状态" min-width="100">
            <template #default="{ row }">
              <el-tag :type="getStockTagType(row.status)">{{ getStockStatusLabel(row) }}</el-tag>
            </template>
          </el-table-column>
          <el-table-column label="最后入库" min-width="160">
            <template #default="{ row }">{{ formatStockDateTime(row.lastInDate) }}</template>
          </el-table-column>
          <el-table-column label="最后出库" min-width="160">
            <template #default="{ row }">{{ formatStockDateTime(row.lastOutDate) }}</template>
          </el-table-column>
          <el-table-column fixed="right" label="操作" min-width="80">
            <template #default="{ row }">
              <el-button link type="primary" @click="viewStockDetail(row.materialId)"
                >详情</el-button
              >
            </template>
          </el-table-column>
        </el-table>
      </div>
      <el-pagination
        v-if="stockTotal"
        v-model:current-page="stockQuery.page"
        v-model:page-size="stockQuery.pageSize"
        :page-sizes="[10, 20, 50]"
        background
        layout="total, sizes, prev, pager, next"
        :total="stockTotal"
        @change="loadStocks"
      />
    </el-card>

    <el-drawer
      :model-value="stockDetailLoading || Boolean(stockDetail) || Boolean(stockDetailError)"
      size="min(92vw, 520px)"
      title="库存详情"
      @close="closeStockDetail"
    >
      <div v-loading="stockDetailLoading" class="stock-detail-area">
        <el-alert
          v-if="stockDetailError"
          :closable="false"
          show-icon
          :title="stockDetailError"
          type="error"
        />
        <div v-else-if="stockDetail" class="stock-detail-grid">
          <div>
            <span>物料</span><strong>{{ stockDetail.materialName }}</strong>
          </div>
          <div>
            <span>物料编号</span><strong>#{{ stockDetail.materialId }}</strong>
          </div>
          <div>
            <span>类型</span><strong>{{ getMaterialTypeLabel(stockDetail) }}</strong>
          </div>
          <div>
            <span>单位</span><strong>{{ stockDetail.unit || '-' }}</strong>
          </div>
          <div>
            <span>可用数量</span
            ><strong
              >{{ formatNumber(stockDetail.availableQty) }} {{ stockDetail.unit || '' }}</strong
            >
          </div>
          <div>
            <span>锁定数量</span
            ><strong>{{ formatNumber(stockDetail.lockedQty) }} {{ stockDetail.unit || '' }}</strong>
          </div>
          <div>
            <span>安全库存</span
            ><strong
              >{{ formatNumber(stockDetail.safetyStock) }} {{ stockDetail.unit || '' }}</strong
            >
          </div>
          <div>
            <span>库存状态</span
            ><el-tag :type="getStockTagType(stockDetail.status)">{{
              getStockStatusLabel(stockDetail)
            }}</el-tag>
          </div>
          <div>
            <span>最后入库</span><strong>{{ formatStockDateTime(stockDetail.lastInDate) }}</strong>
          </div>
          <div>
            <span>最后出库</span><strong>{{ formatStockDateTime(stockDetail.lastOutDate) }}</strong>
          </div>
        </div>
      </div>
    </el-drawer>
  </PageContainer>
</template>

<style scoped>
.request-error,
.inventory-statistics,
.stock-card,
.operation-card {
  margin-bottom: 16px;
}
.inventory-statistics {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 14px;
}
.stock-filter-form {
  display: flex;
  flex-wrap: nowrap;
  align-items: center;
  gap: 8px;
  margin-bottom: 16px;
}
.stock-filter-form :deep(.el-form-item) {
  min-width: 0;
  margin-right: 0;
  margin-bottom: 0;
}
.stock-filter-form :deep(.el-form-item__content) {
  min-width: 0;
  flex: 1 1 auto;
}
.stock-filter-form :deep(.el-form-item__label) {
  flex-shrink: 0;
  padding-right: 8px;
  white-space: nowrap;
}
.stock-filter-form :deep(.el-form-item:not(:last-child)) {
  flex: 1 1 150px;
  max-width: 240px;
}
.stock-filter-form :deep(.el-form-item:first-child) {
  min-width: 180px;
}
.stock-filter-form :deep(.el-input),
.stock-filter-form :deep(.el-input-number),
.stock-filter-form :deep(.el-select) {
  width: 100%;
}
.stock-filter-form :deep(.el-input-number .el-input__inner) {
  text-align: left;
}
.stock-filter-form :deep(.el-input-number.is-without-controls .el-input__wrapper) {
  padding-right: 11px;
  padding-left: 11px;
}
.stock-filter-form :deep(.el-form-item:last-child) {
  flex: 0 0 auto;
}
.stock-filter-form :deep(.el-form-item:last-child .el-form-item__content) {
  flex-wrap: nowrap;
  gap: 8px;
}
.stock-filter-form :deep(.el-form-item:last-child .el-button + .el-button) {
  margin-left: 0;
}
.stock-table-area {
  min-height: 260px;
}
.stock-detail-area {
  min-height: 180px;
}
.stock-detail-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 12px;
}
.stock-detail-grid > div {
  display: grid;
  gap: 4px;
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 6px;
  padding: 12px;
}
.stock-detail-grid span {
  color: var(--el-text-color-secondary);
  font-size: 12px;
}
.material-cell {
  display: grid;
  gap: 2px;
}
.material-cell small {
  color: var(--el-text-color-secondary);
  font-size: 12px;
}
:deep(.el-pagination) {
  justify-content: flex-end;
  margin-top: 16px;
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
  .stock-filter-form {
    flex-wrap: wrap;
  }
  .stock-filter-form :deep(.el-form-item) {
    flex: 1 1 100%;
    width: auto;
    min-width: 0;
    max-width: none;
  }
  .stock-detail-grid {
    grid-template-columns: 1fr;
  }
}
</style>
