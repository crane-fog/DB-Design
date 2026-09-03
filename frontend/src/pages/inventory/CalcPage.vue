<script setup lang="ts">
import { Delete, Plus, ShoppingCart } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import type {
  InventoryReferenceData,
  MaterialShortageRequestItem,
  MaterialShortageResult,
} from '@/types/inventory'
import { computed, onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import { formatDateTime, formatNumber } from '@/utils/format'
import EmptyState from '@/components/common/EmptyState.vue'
import PageContainer from '@/components/common/PageContainer.vue'
import PageHeader from '@/components/common/PageHeader.vue'
import { getErrorMessage } from '@/utils/error'
import { inventoryService } from '@/services/InventoryService'
import { purchaseService } from '@/services/PurchaseService'
import { useAuthStore } from '@/stores/auth'
import { useRouter } from 'vue-router'

interface CalculationRow extends MaterialShortageRequestItem {
  key: number
}

interface BuyerOption {
  buyerId: number
  buyerName: string
}

const auth = useAuthStore()
const router = useRouter()
const calculating = ref(false)
const creatingDrafts = ref(false)
const error = ref('')
const result = ref<MaterialShortageResult>()
const rows = reactive<CalculationRow[]>([{ key: 1, materialId: 0, productionQty: 1, versionId: 0 }])
const purchaseQuantities = reactive<Record<number, number>>({})
const draftExpectedDate = ref(new Date(Date.now() + 14 * 86_400_000).toISOString().slice(0, 10))
const selectedBuyerId = ref<number>()
const buyerOptions = ref<BuyerOption[]>([])
const buyerLoading = ref(false)
const buyerError = ref('')
let nextKey = 2
let requestId = 0
const referenceLoading = ref(false)
const referenceError = ref('')
const referenceData = ref<InventoryReferenceData>({
  bomVersions: [],
  materials: [],
  productionOrders: [],
})
let referenceRequestId = 0

const shortageItems = computed(
  () => result.value?.items.filter((item) => item.netShortageQty > 0) ?? [],
)
const totalShortage = computed(() =>
  shortageItems.value.reduce((sum, item) => sum + item.netShortageQty, 0),
)
const productOptions = computed(() =>
  referenceData.value.materials.filter((item) => item.materialType === 'finished'),
)

function getVersionOptions(materialId: number) {
  return referenceData.value.bomVersions.filter((item) => item.materialId === materialId)
}

function handleProductChange(row: CalculationRow) {
  row.versionId = getVersionOptions(row.materialId)[0]?.versionId ?? 0
}

async function loadReferenceData() {
  const currentRequestId = ++referenceRequestId
  referenceLoading.value = true
  referenceError.value = ''
  try {
    const data = await inventoryService.getReferenceData(false)
    if (currentRequestId === referenceRequestId) {
      referenceData.value = data
    }
  } catch (requestError) {
    if (currentRequestId === referenceRequestId) {
      referenceError.value = getErrorMessage(requestError, '产品与 BOM 版本加载失败')
    }
  } finally {
    if (currentRequestId === referenceRequestId) {
      referenceLoading.value = false
    }
  }
}

function addRow() {
  rows.push({ key: nextKey++, materialId: 0, productionQty: 1, versionId: 0 })
}

function removeRow(key: number) {
  if (rows.length > 1) {
    const index = rows.findIndex((row) => row.key === key)
    if (index !== -1) {
      rows.splice(index, 1)
    }
  }
}

function validateRows() {
  if (rows.some((row) => row.materialId <= 0 || row.productionQty <= 0 || row.versionId <= 0)) {
    ElMessage.warning('请完整选择成品、BOM 版本并填写生产数量')
    return false
  }
  return true
}

async function calculate() {
  if (!validateRows() || calculating.value) {
    return
  }
  const currentRequestId = ++requestId
  calculating.value = true
  error.value = ''
  try {
    const calculation = await inventoryService.calculateShortage(
      rows.map(({ materialId, productionQty, versionId }) => ({
        materialId,
        productionQty,
        versionId,
      })),
    )
    if (currentRequestId !== requestId) {
      return
    }
    result.value = calculation
    for (const item of calculation.items) {
      purchaseQuantities[item.materialId] = item.suggestedPurchaseQty
    }
  } catch (requestError) {
    if (currentRequestId === requestId) {
      error.value = getErrorMessage(requestError, '物料缺口计算失败')
    }
  } finally {
    if (currentRequestId === requestId) {
      calculating.value = false
    }
  }
}

async function createPurchaseDrafts() {
  if (creatingDrafts.value) {
    return
  }
  if (!selectedBuyerId.value) {
    ElMessage.warning('请选择采购员')
    return
  }
  const buyerId = selectedBuyerId.value
  const items = shortageItems.value
    .map((item) => ({
      materialId: item.materialId,
      purchaseQty: purchaseQuantities[item.materialId] ?? 0,
    }))
    .filter((item) => item.purchaseQty > 0)
  if (!items.length) {
    ElMessage.warning('没有可生成采购草稿的缺口记录')
    return
  }
  if (!draftExpectedDate.value) {
    ElMessage.warning('请选择采购预计交期')
    return
  }
  creatingDrafts.value = true
  try {
    const draftResult = await purchaseService.createDrafts(items, buyerId, draftExpectedDate.value)
    const skipped = draftResult.unassignedItems.length
    let message = `已生成 ${draftResult.createdCount} 张采购草稿`
    if (skipped > 0) {
      message = `已生成 ${draftResult.createdCount} 张草稿，${skipped} 项缺少供应商`
    }
    ElMessage.success(message)
    if (draftResult.createdCount > 0) {
      try {
        await ElMessageBox.confirm('采购草稿已生成，是否立即进入采购管理查看？', '生成成功', {
          cancelButtonText: '继续计算',
          confirmButtonText: '查看采购草稿',
          type: 'success',
        })
        await router.push('/purchase')
      } catch (action) {
        if (action !== 'cancel' && action !== 'close') {
          throw action
        }
      }
    }
  } catch (requestError) {
    ElMessage.error(getErrorMessage(requestError, '生成采购草稿失败'))
  } finally {
    creatingDrafts.value = false
  }
}

async function loadBuyerOptions() {
  if (!auth.roles.some((role) => ['系统管理员', '采购员', '采购主管'].includes(role))) {
    return
  }
  buyerLoading.value = true
  buyerError.value = ''
  try {
    const purchaseReferences = await purchaseService.getReferenceData()
    buyerOptions.value = purchaseReferences.buyers
  } catch (requestError) {
    buyerError.value = getErrorMessage(requestError, '采购员列表加载失败')
  } finally {
    buyerLoading.value = false
  }
}

onMounted(() => {
  void loadReferenceData()
  void loadBuyerOptions()
})
onBeforeUnmount(() => {
  requestId += 1
  referenceRequestId += 1
})
</script>

<template>
  <PageContainer>
    <PageHeader
      title="物料缺口计算"
      description="按产品、产量与 BOM 版本展开需求，并生成可执行的采购建议。"
    />

    <el-alert
      v-if="referenceError"
      class="request-error"
      :closable="false"
      :title="referenceError"
      type="error"
      show-icon
    >
      <template #default
        ><el-button link type="primary" @click="loadReferenceData"
          >重新加载选项</el-button
        ></template
      >
    </el-alert>

    <el-card class="calculation-card" shadow="never">
      <template #header>
        <div class="section-heading">
          <span>计算条件</span>
          <el-button :icon="Plus" plain type="primary" @click="addRow">添加产品</el-button>
        </div>
      </template>
      <div class="calculation-rows">
        <div v-for="(row, index) in rows" :key="row.key" class="calculation-row">
          <span class="row-index">{{ index + 1 }}</span>
          <label
            ><span>成品物料</span
            ><el-input-number
              v-if="!productOptions.length"
              v-model="row.materialId"
              :min="1"
              :precision="0"
              placeholder="输入成品编号" /><el-select
              v-else
              v-model="row.materialId"
              filterable
              :loading="referenceLoading"
              placeholder="选择成品"
              @change="handleProductChange(row)"
              ><el-option
                v-for="product in productOptions"
                :key="product.materialId"
                :label="product.materialName"
                :value="product.materialId" /></el-select
          ></label>
          <label
            ><span>生产数量</span
            ><el-input-number v-model="row.productionQty" :min="0.01" :precision="2"
          /></label>
          <label
            ><span>BOM 版本</span
            ><el-input-number
              v-if="!getVersionOptions(row.materialId).length"
              v-model="row.versionId"
              :min="1"
              :precision="0"
              placeholder="输入版本编号" /><el-select
              v-else
              v-model="row.versionId"
              placeholder="选择版本"
              ><el-option
                v-for="version in getVersionOptions(row.materialId)"
                :key="version.versionId"
                :label="version.versionNo"
                :value="version.versionId" /></el-select
          ></label>
          <el-tooltip content="移除此产品" placement="top">
            <el-button
              :disabled="rows.length === 1"
              :icon="Delete"
              aria-label="移除此产品"
              circle
              @click="removeRow(row.key)"
            />
          </el-tooltip>
        </div>
      </div>
      <div class="calculation-actions">
        <el-button :loading="calculating" type="primary" @click="calculate">开始计算</el-button>
        <span>缺口口径：毛需求 − 可用库存 − 在途数量 + 安全库存</span>
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
        ><el-button link type="primary" @click="calculate">重新计算</el-button></template
      >
    </el-alert>

    <el-card class="result-card table-card table-card--accent" shadow="never">
      <template #header>
        <div class="section-heading result-heading table-card__header">
          <span>
            计算结果
            <small v-if="result">{{ formatDateTime(result.calculatedAt) }}</small>
          </span>
          <div v-if="result" class="result-summary table-card__header-actions">
            <span
              >缺口物料 <strong>{{ shortageItems.length }}</strong> 项</span
            >
            <span
              >总缺口 <strong>{{ formatNumber(totalShortage) }}</strong></span
            >
            <el-date-picker
              v-model="draftExpectedDate"
              placeholder="采购预计交期"
              type="date"
              value-format="YYYY-MM-DD"
            />
            <el-input-number
              v-if="!buyerOptions.length"
              v-model="selectedBuyerId"
              :min="1"
              :precision="0"
              placeholder="输入采购员编号"
            />
            <el-select
              v-else
              v-model="selectedBuyerId"
              :disabled="buyerLoading"
              :loading="buyerLoading"
              placeholder="请选择采购员"
              style="width: 180px"
            >
              <el-option
                v-for="buyer in buyerOptions"
                :key="buyer.buyerId"
                :label="buyer.buyerName"
                :value="buyer.buyerId"
              />
            </el-select>
            <el-button
              :icon="ShoppingCart"
              :loading="creatingDrafts"
              :disabled="buyerLoading"
              type="primary"
              @click="createPurchaseDrafts"
            >
              生成采购草稿
            </el-button>
          </div>
        </div>
      </template>

      <el-alert v-if="buyerError" :closable="false" :title="buyerError" type="error" />

      <div v-loading="calculating" class="result-table-wrap">
        <EmptyState
          v-if="!result && !calculating"
          description="填写上方条件并开始计算后，将在此展示各层级物料需求。"
          title="尚未计算"
        />
        <EmptyState
          v-else-if="result && !result.items.length"
          description="当前条件未展开出物料需求。"
          title="无计算结果"
        />
        <el-table v-else-if="result" :data="result.items" stripe>
          <el-table-column label="层级" min-width="72" prop="level" />
          <el-table-column label="物料" min-width="190">
            <template #default="{ row }">
              <div class="material-cell">
                <strong>{{ row.materialName || `物料 #${row.materialId}` }}</strong
                ><small>ID {{ row.materialId }}</small>
              </div>
            </template>
          </el-table-column>
          <el-table-column label="毛需求" min-width="105"
            ><template #default="{ row }">{{
              formatNumber(row.grossRequirement)
            }}</template></el-table-column
          >
          <el-table-column label="可用库存" min-width="105"
            ><template #default="{ row }">{{
              formatNumber(row.availableQty)
            }}</template></el-table-column
          >
          <el-table-column label="在途" min-width="90"
            ><template #default="{ row }">{{
              formatNumber(row.inTransitQty)
            }}</template></el-table-column
          >
          <el-table-column label="安全库存" min-width="105"
            ><template #default="{ row }">{{
              formatNumber(row.safetyStock)
            }}</template></el-table-column
          >
          <el-table-column label="净缺口" min-width="105">
            <template #default="{ row }"
              ><strong :class="{ shortage: row.netShortageQty > 0 }">{{
                formatNumber(row.netShortageQty)
              }}</strong></template
            >
          </el-table-column>
          <el-table-column label="建议采购" min-width="150">
            <template #default="{ row }">
              <el-input-number
                v-if="row.netShortageQty > 0"
                v-model="purchaseQuantities[row.materialId]"
                :min="0"
                :precision="2"
                size="small"
              />
              <span v-else>-</span>
            </template>
          </el-table-column>
        </el-table>
      </div>
    </el-card>
  </PageContainer>
</template>

<style scoped>
.calculation-card,
.request-error {
  margin-bottom: 16px;
}
.section-heading,
.result-summary,
.calculation-actions {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}
.calculation-rows {
  display: grid;
  gap: 10px;
}
.calculation-row {
  display: grid;
  grid-template-columns: 30px repeat(3, minmax(150px, 1fr)) 34px;
  gap: 12px;
  align-items: end;
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 6px;
  background: var(--app-background);
  padding: 12px;
}
.row-index {
  align-self: center;
  color: var(--el-text-color-secondary);
  font-weight: 700;
  text-align: center;
}
.calculation-row label {
  display: grid;
  gap: 5px;
  color: var(--el-text-color-secondary);
  font-size: 12px;
}
.calculation-row :deep(.el-input-number),
.calculation-row :deep(.el-select) {
  width: 100%;
}
.calculation-actions {
  justify-content: flex-start;
  margin-top: 16px;
}
.calculation-actions span,
.result-heading small,
.material-cell small {
  color: var(--el-text-color-secondary);
  font-size: 12px;
}
.result-heading > span,
.material-cell {
  display: grid;
  gap: 3px;
}
.result-summary {
  justify-content: flex-end;
  flex-wrap: wrap;
  color: var(--el-text-color-secondary);
  font-size: 13px;
}
.result-summary strong,
.shortage {
  color: var(--primary-color);
}
.result-table-wrap {
  min-height: 210px;
}
@media (max-width: 960px) {
  .calculation-row {
    grid-template-columns: 30px repeat(2, minmax(140px, 1fr)) 34px;
  }
  .calculation-row label:nth-of-type(3) {
    grid-column: 2 / 4;
  }
}
@media (max-width: 640px) {
  .calculation-row {
    grid-template-columns: 28px minmax(0, 1fr) 34px;
  }
  .calculation-row label,
  .calculation-row label:nth-of-type(3) {
    grid-column: 2;
  }
  .calculation-row > button {
    grid-column: 3;
    grid-row: 1;
  }
  .result-heading,
  .calculation-actions {
    align-items: flex-start;
    flex-direction: column;
  }
}
</style>
