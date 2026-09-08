<script setup lang="ts">
import type {
  CompletionInboundDetail,
  CompletionInboundItem,
  CompletionInboundQuery,
  InventoryReferenceData,
} from '@/types/inventory'
import { Refresh, Search } from '@element-plus/icons-vue'
import { onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import { formatDateTime, formatNumber } from '@/utils/format'
import EmptyState from '@/components/common/EmptyState.vue'
import PageContainer from '@/components/common/PageContainer.vue'
import PageHeader from '@/components/common/PageHeader.vue'
import { getErrorMessage } from '@/utils/error'
import { inventoryService } from '@/services/InventoryService'
import { useRouter } from 'vue-router'

const router = useRouter()
const loading = ref(false)
const error = ref('')
const items = ref<CompletionInboundItem[]>([])
const total = ref(0)
const dateRange = ref<[string, string]>()
const query = reactive<CompletionInboundQuery>({ page: 1, pageSize: 10 })
const detailDrawerOpen = ref(false)
const detailLoading = ref(false)
const detailError = ref('')
const selectedInbound = ref<CompletionInboundDetail>()
let alive = true
let requestId = 0
const referenceError = ref('')
const referenceData = ref<InventoryReferenceData>({
  bomVersions: [],
  materials: [],
  productionOrders: [],
})
let referenceRequestId = 0

async function loadReferenceData() {
  const currentRequestId = ++referenceRequestId
  referenceError.value = ''
  try {
    const data = await inventoryService.getReferenceData()
    if (alive && currentRequestId === referenceRequestId) {
      referenceData.value = data
    }
  } catch (requestError) {
    if (alive && currentRequestId === referenceRequestId) {
      referenceError.value = getErrorMessage(requestError, '生产订单与产出物料选项加载失败')
    }
  }
}

async function loadItems() {
  const currentRequestId = ++requestId
  loading.value = true
  error.value = ''
  try {
    const result = await inventoryService.listCompletionInbound({
      ...query,
      endTime: dateRange.value?.[1],
      startTime: dateRange.value?.[0],
    })
    if (!alive || currentRequestId !== requestId) {
      return
    }
    items.value = result.items
    total.value = result.total
  } catch (requestError) {
    if (alive && currentRequestId === requestId) {
      error.value = getErrorMessage(requestError, '完工入库记录加载失败')
    }
  } finally {
    if (alive && currentRequestId === requestId) {
      loading.value = false
    }
  }
}

function resetQuery() {
  Object.assign(query, { materialId: undefined, orderId: undefined, page: 1 })
  dateRange.value = undefined
  void loadItems()
}

function searchItems() {
  query.page = 1
  void loadItems()
}

async function viewInbound(item: CompletionInboundItem) {
  selectedInbound.value = undefined
  detailError.value = ''
  detailDrawerOpen.value = true
  detailLoading.value = true
  try {
    selectedInbound.value = await inventoryService.getCompletionInboundDetail(item.inboundId)
  } catch (requestError) {
    detailError.value = getErrorMessage(requestError, '完工入库详情加载失败')
  } finally {
    detailLoading.value = false
  }
}

function openBatchTrace() {
  const inbound = selectedInbound.value
  if (!inbound) {
    return
  }
  void router.push({
    path: '/trace/product',
    query: { batchNo: inbound.batchNo, orderId: String(inbound.orderId) },
  })
}

function closeInboundDetail() {
  selectedInbound.value = undefined
  detailError.value = ''
}

onMounted(() => {
  void loadItems()
  void loadReferenceData()
})
onBeforeUnmount(() => {
  alive = false
  requestId += 1
  referenceRequestId += 1
})
</script>

<template>
  <PageContainer>
    <PageHeader title="完工入库记录" description="查询生产完工批次、核对合格数量并查看入库详情。">
      <template #actions>
        <el-button :icon="Refresh" :loading="loading" @click="loadItems">刷新</el-button>
      </template>
    </PageHeader>

    <el-alert
      v-if="referenceError"
      class="request-error"
      :closable="false"
      show-icon
      :title="referenceError"
      type="warning"
    >
      <template #default
        ><el-button link type="primary" @click="loadReferenceData"
          >重新加载关联选项</el-button
        ></template
      >
    </el-alert>

    <el-card class="query-card" shadow="never">
      <div class="query-bar">
        <el-select v-model="query.orderId" clearable filterable placeholder="生产订单">
          <el-option
            v-for="order in referenceData.productionOrders"
            :key="order.orderId"
            :label="`#${order.orderId} · ${order.materialName}`"
            :value="order.orderId"
          />
        </el-select>
        <el-select v-model="query.materialId" clearable filterable placeholder="物料">
          <el-option
            v-for="material in referenceData.materials.filter((item) =>
              ['finished', 'semi_finished'].includes(item.materialType),
            )"
            :key="material.materialId"
            :label="material.materialName"
            :value="material.materialId"
          />
        </el-select>
        <el-date-picker
          v-model="dateRange"
          end-placeholder="结束日期"
          range-separator="至"
          start-placeholder="开始日期"
          type="daterange"
          value-format="YYYY-MM-DD"
        />
        <el-button :icon="Search" type="primary" @click="searchItems">查询</el-button>
        <el-button @click="resetQuery">重置</el-button>
      </div>
    </el-card>

    <el-alert
      v-if="error"
      class="request-error"
      :closable="false"
      show-icon
      :title="error"
      type="error"
    >
      <template #default
        ><el-button link type="primary" @click="loadItems">重新加载</el-button></template
      >
    </el-alert>

    <el-card class="records-card table-card table-card--accent" shadow="never">
      <template #header
        ><div class="table-card__header"><span>入库记录</span></div></template
      >
      <div v-loading="loading" class="records-area">
        <EmptyState
          v-if="!loading && !error && !items.length"
          description="当前查询条件下没有完工入库记录。"
        />
        <el-table v-else :data="items" stripe>
          <el-table-column label="入库 ID" min-width="70" prop="inboundId" />
          <el-table-column label="批次号" min-width="170"
            ><template #default="{ row }"
              ><strong>{{ row.batchNo }}</strong></template
            ></el-table-column
          >
          <el-table-column label="生产订单 ID" min-width="100"
            ><template #default="{ row }">#{{ row.orderId }}</template></el-table-column
          >
          <el-table-column label="产出物料" min-width="200"
            ><template #default="{ row }"
              ><div class="product-cell">
                <strong>{{ row.productName || `物料 #${row.materialId}` }}</strong
                ><small>#{{ row.materialId }}</small>
              </div></template
            ></el-table-column
          >
          <el-table-column label="完工 / 合格" min-width="110"
            ><template #default="{ row }"
              >{{ formatNumber(row.finishQty) }} /
              <strong class="qualified">{{ formatNumber(row.qualifiedQty) }}</strong></template
            ></el-table-column
          >
          <el-table-column label="合格率" min-width="70"
            ><template #default="{ row }">{{
              row.finishQty ? `${formatNumber((row.qualifiedQty / row.finishQty) * 100)}%` : '-'
            }}</template></el-table-column
          >
          <el-table-column label="消耗锁定" min-width="80"
            ><template #default="{ row }">{{
              row.consumedLockRecords?.length ?? 0
            }}</template></el-table-column
          >
          <el-table-column label="入库时间" min-width="175"
            ><template #default="{ row }">{{
              formatDateTime(row.inboundTime)
            }}</template></el-table-column
          >
          <el-table-column label="操作人" min-width="80"
            ><template #default="{ row }">{{
              row.operatorName || (row.operatorId ? `用户 #${row.operatorId}` : '-')
            }}</template></el-table-column
          >
          <el-table-column fixed="right" label="操作" min-width="90">
            <template #default="{ row }">
              <el-button link type="primary" @click="viewInbound(row)">详情</el-button>
            </template>
          </el-table-column>
        </el-table>
      </div>
      <el-pagination
        v-if="total"
        v-model:current-page="query.page"
        v-model:page-size="query.pageSize"
        :page-sizes="[10, 20, 50]"
        background
        layout="total, sizes, prev, pager, next"
        :total="total"
        @change="loadItems"
      />
    </el-card>

    <el-drawer
      v-model="detailDrawerOpen"
      size="min(94vw, 680px)"
      title="完工入库详情"
      @closed="closeInboundDetail"
    >
      <div v-loading="detailLoading" class="inbound-detail-area">
        <el-alert
          v-if="detailError"
          :closable="false"
          show-icon
          :title="detailError"
          type="error"
        />
        <template v-else-if="selectedInbound">
          <div class="inbound-detail-grid">
            <div>
              <span>入库单</span><strong>#{{ selectedInbound.inboundId }}</strong>
            </div>
            <div>
              <span>生产订单</span
              ><strong>{{
                selectedInbound.productionOrder
                  ? '#' +
                    selectedInbound.productionOrder.orderId +
                    ' · ' +
                    selectedInbound.productionOrder.materialName
                  : '#' + selectedInbound.orderId
              }}</strong>
            </div>
            <div>
              <span>产出物料</span><strong>{{ selectedInbound.productName }}</strong>
            </div>
            <div>
              <span>批次号</span><strong>{{ selectedInbound.batchNo }}</strong>
            </div>
            <div>
              <span>完工数量</span><strong>{{ formatNumber(selectedInbound.finishQty) }}</strong>
            </div>
            <div>
              <span>合格数量</span><strong>{{ formatNumber(selectedInbound.qualifiedQty) }}</strong>
            </div>
            <div>
              <span>BOM 版本</span
              ><strong>{{
                selectedInbound.bomVersionNo || '#' + selectedInbound.versionId
              }}</strong>
            </div>
            <div>
              <span>操作人</span
              ><strong>{{
                selectedInbound.operatorName ||
                (selectedInbound.operatorId ? `用户 #${selectedInbound.operatorId}` : '-')
              }}</strong>
            </div>
          </div>
          <div class="detail-actions">
            <el-button :icon="Search" type="primary" @click="openBatchTrace">批次追溯</el-button>
          </div>
          <el-divider content-position="left">原料锁定消耗</el-divider>
          <EmptyState
            v-if="!selectedInbound.consumedLockRecords?.length"
            description="该入库记录没有返回原料锁定消耗明细。"
          />
          <el-table v-else :data="selectedInbound.consumedLockRecords" stripe>
            <el-table-column label="锁定 ID" prop="lockId" min-width="90" />
            <el-table-column label="物料" min-width="180">
              <template #default="{ row }">{{
                row.materialName || `物料 #${row.materialId}`
              }}</template>
            </el-table-column>
            <el-table-column label="消耗数量" min-width="110">
              <template #default="{ row }">{{ formatNumber(row.lockQty) }}</template>
            </el-table-column>
          </el-table>
        </template>
      </div>
    </el-drawer>
  </PageContainer>
</template>

<style scoped>
.query-card,
.request-error {
  margin-bottom: 16px;
}

.detail-actions {
  display: flex;
  justify-content: flex-end;
  margin-top: 16px;
}
.inbound-detail-area {
  min-height: 180px;
}
.query-bar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 10px;
}
.query-bar :deep(.el-input-number) {
  width: 155px;
}
.query-bar :deep(.el-select) {
  width: 190px;
}
.records-area {
  min-height: 260px;
}
.product-cell {
  display: grid;
  gap: 2px;
}
.product-cell small {
  color: var(--el-text-color-secondary);
}
.qualified {
  color: var(--el-color-success);
}
:deep(.el-pagination) {
  justify-content: flex-end;
  margin-top: 16px;
}
.inbound-detail-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 12px;
}
.inbound-detail-grid > div {
  display: grid;
  gap: 4px;
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 6px;
  padding: 12px;
}
.inbound-detail-grid span {
  color: var(--el-text-color-secondary);
  font-size: 12px;
}
@media (max-width: 680px) {
  .query-bar > * {
    flex: 1 1 160px;
  }
  .inbound-detail-grid {
    grid-template-columns: 1fr;
  }
}
</style>
