<script setup lang="ts">
import {
  type BatchConsumptionCreateFormData,
  type BatchConsumptionItem,
  type BatchConsumptionUpdateFormData,
  type MaterialBatchTraceItem,
  type ProductBatchTraceItem,
  type QualityImpactResult,
  type SuggestedActionValue,
  traceService,
} from '@/services/TraceService'
import { Delete, EditPen, Plus, Refresh, Search } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import { computed, onMounted, reactive, ref } from 'vue'
import { formatDateTime, formatNumber } from '@/utils/format'
import { PERMISSIONS } from '@/constants/permissions'
import PageContainer from '@/components/common/PageContainer.vue'
import PageHeader from '@/components/common/PageHeader.vue'
import type { PageResult } from '@/services/pagination'
import StatusTag from '@/components/common/StatusTag.vue'
import { getErrorMessage } from '@/utils/error'
import { productionOrderStatusLabels as orderStatusLabels } from '@/constants/status'
import { parsePositiveInt } from '@/utils/parse'
import { useAuthStore } from '@/stores/auth'

type TraceTab = 'consumption' | 'impact' | 'material' | 'product'

const suggestedActionLabels: Record<SuggestedActionValue, string> = {
  freeze: '冻结批次',
  observe: '持续观察',
  recall: '启动召回',
}

const suggestedActionTone: Record<SuggestedActionValue, 'danger' | 'info' | 'warning'> = {
  freeze: 'warning',
  observe: 'info',
  recall: 'danger',
}

const pageSize = 10
const auth = useAuthStore()
const canManage = computed(() => auth.hasPermission(PERMISSIONS.trace.manage))
const activeTab = ref<TraceTab>('consumption')

// ---------- 批次消耗关系 ----------
const consumptionFilters = reactive({ itemId: '', materialId: '', orderId: '' })
const consumptionPage = ref(1)
const consumptionLoading = ref(false)
const consumptionError = ref('')
const consumptionResult = ref<PageResult<BatchConsumptionItem>>({
  items: [],
  page: 1,
  pageSize,
  total: 0,
})
const consumptionDialogVisible = ref(false)
const consumptionDialogMode = ref<'create' | 'edit'>('create')
const consumptionFormRef = ref<FormInstance>()
const consumptionSubmitting = ref(false)
const consumptionDeleting = ref(false)
const editingConsumptionId = ref<number>()
const consumptionForm = reactive<BatchConsumptionCreateFormData>({
  consumeQty: 1,
  itemId: 0,
  orderId: 0,
})
const consumptionDialogTitle = computed(() => {
  if (consumptionDialogMode.value === 'create') {
    return '新增批次消耗'
  }
  return '修改批次消耗'
})
const consumptionRules: FormRules<BatchConsumptionCreateFormData> = {
  consumeQty: [
    { message: '请输入消耗数量', required: true, trigger: 'blur', type: 'number' },
    { message: '消耗数量必须大于 0', min: 0.01, trigger: 'blur', type: 'number' },
  ],
  itemId: [
    { message: '请输入采购明细 ID', required: true, trigger: 'blur', type: 'number' },
    { message: '采购明细 ID 必须大于 0', min: 1, trigger: 'blur', type: 'number' },
  ],
  orderId: [
    { message: '请输入生产订单 ID', required: true, trigger: 'blur', type: 'number' },
    { message: '生产订单 ID 必须大于 0', min: 1, trigger: 'blur', type: 'number' },
  ],
}

async function loadConsumption(targetPage = consumptionPage.value) {
  consumptionLoading.value = true
  consumptionError.value = ''
  try {
    consumptionResult.value = await traceService.listBatchConsumption({
      itemId: parsePositiveInt(consumptionFilters.itemId),
      materialId: parsePositiveInt(consumptionFilters.materialId),
      orderId: parsePositiveInt(consumptionFilters.orderId),
      page: targetPage,
      pageSize,
    })
    consumptionPage.value = consumptionResult.value.page
  } catch (requestError) {
    consumptionError.value = getErrorMessage(requestError, '批次消耗列表加载失败')
  } finally {
    consumptionLoading.value = false
  }
}

function resetConsumptionFilters() {
  Object.assign(consumptionFilters, { itemId: '', materialId: '', orderId: '' })
  void loadConsumption(1)
}

function openConsumptionCreate() {
  consumptionDialogMode.value = 'create'
  Object.assign(consumptionForm, { consumeQty: 1, itemId: 0, orderId: 0 })
  editingConsumptionId.value = undefined
  consumptionFormRef.value?.clearValidate()
  consumptionDialogVisible.value = true
}

function openConsumptionEdit(record: BatchConsumptionItem) {
  consumptionDialogMode.value = 'edit'
  Object.assign(consumptionForm, {
    consumeQty: record.consumeQty,
    itemId: record.itemId,
    orderId: record.orderId,
  })
  editingConsumptionId.value = record.consumptionId
  consumptionFormRef.value?.clearValidate()
  consumptionDialogVisible.value = true
}

async function submitConsumptionForm() {
  const valid = await consumptionFormRef.value?.validate().catch(() => false)
  if (!valid || consumptionSubmitting.value) {
    return
  }
  consumptionSubmitting.value = true
  try {
    if (consumptionDialogMode.value === 'create') {
      await traceService.createBatchConsumption({ ...consumptionForm })
      ElMessage.success('批次消耗已新增')
    } else if (editingConsumptionId.value !== undefined) {
      await traceService.updateBatchConsumption({
        ...consumptionForm,
        consumptionId: editingConsumptionId.value,
      } satisfies BatchConsumptionUpdateFormData)
      ElMessage.success('批次消耗已更新')
    }
    consumptionDialogVisible.value = false
    await loadConsumption(consumptionPage.value)
  } catch (requestError) {
    ElMessage.error(getErrorMessage(requestError, '批次消耗提交失败'))
  } finally {
    consumptionSubmitting.value = false
  }
}

async function removeConsumption(record: BatchConsumptionItem) {
  if (consumptionDeleting.value) {
    return
  }
  try {
    consumptionDeleting.value = true
    await ElMessageBox.confirm(
      `确定要删除消耗记录 #${record.consumptionId} 吗？删除后无法恢复。`,
      '删除批次消耗',
      { confirmButtonText: '确定删除', type: 'warning' },
    )
    await traceService.deleteBatchConsumption(record.consumptionId)
    ElMessage.success('批次消耗已删除')
    await loadConsumption(consumptionPage.value)
  } catch (requestError) {
    if (requestError !== 'cancel' && requestError !== 'close') {
      ElMessage.error(getErrorMessage(requestError, '批次消耗删除失败'))
    }
  } finally {
    consumptionDeleting.value = false
  }
}

// ---------- 正向追溯（成品 → 原材料） ----------
const productFilters = reactive({ batchNo: '', includeSupplier: true, orderId: '' })
const productLoading = ref(false)
const productError = ref('')
const productSearched = ref(false)
const productResult = ref<ProductBatchTraceItem>()

async function traceProduct() {
  const orderId = parsePositiveInt(productFilters.orderId)
  const batchNo = productFilters.batchNo.trim()
  if (!orderId && !batchNo) {
    ElMessage.warning('请至少输入生产订单 ID 或成品批次号')
    return
  }
  productLoading.value = true
  productError.value = ''
  productSearched.value = true
  try {
    productResult.value = await traceService.traceProductBatch({
      batchNo: batchNo || undefined,
      includeSupplier: productFilters.includeSupplier,
      orderId,
    })
  } catch (requestError) {
    productResult.value = undefined
    productError.value = getErrorMessage(requestError, '正向追溯失败')
  } finally {
    productLoading.value = false
  }
}

function resetProductFilters() {
  Object.assign(productFilters, { batchNo: '', includeSupplier: true, orderId: '' })
  productResult.value = undefined
  productSearched.value = false
  productError.value = ''
}

// ---------- 反向追溯（原材料 → 成品） ----------
const materialFilters = reactive({ itemId: '', materialId: '', receiveRange: [] as string[] })
const materialLoading = ref(false)
const materialError = ref('')
const materialSearched = ref(false)
const materialResult = ref<MaterialBatchTraceItem[]>([])

async function traceMaterial() {
  const itemId = parsePositiveInt(materialFilters.itemId)
  const materialId = parsePositiveInt(materialFilters.materialId)
  const [receiveDateStart, receiveDateEnd] = materialFilters.receiveRange
  if (!itemId && !materialId && !(receiveDateStart && receiveDateEnd)) {
    ElMessage.warning('请至少提供采购明细 ID、原材料 ID 或完整的到货日期范围')
    return
  }
  materialLoading.value = true
  materialError.value = ''
  materialSearched.value = true
  try {
    materialResult.value = await traceService.traceMaterialBatch({
      itemId,
      materialId,
      receiveDateEnd,
      receiveDateStart,
    })
  } catch (requestError) {
    materialResult.value = []
    materialError.value = getErrorMessage(requestError, '反向追溯失败')
  } finally {
    materialLoading.value = false
  }
}

function resetMaterialFilters() {
  Object.assign(materialFilters, { itemId: '', materialId: '', receiveRange: [] })
  materialResult.value = []
  materialSearched.value = false
  materialError.value = ''
}

// ---------- 质量影响分析 ----------
const impactFilters = reactive({ itemIds: '', materialId: '', receiveRange: [] as string[] })
const impactLoading = ref(false)
const impactError = ref('')
const impactSearched = ref(false)
const impactResult = ref<QualityImpactResult>()

function parseItemIds(value: string) {
  return value
    .split(/[\s,，]+/)
    .map((token) => Number(token.trim()))
    .filter((id) => Number.isInteger(id) && id > 0)
}

async function analyzeImpact() {
  const itemIds = parseItemIds(impactFilters.itemIds)
  const materialId = parsePositiveInt(impactFilters.materialId)
  const [receiveDateStart, receiveDateEnd] = impactFilters.receiveRange
  if (!itemIds.length && !materialId && !(receiveDateStart && receiveDateEnd)) {
    ElMessage.warning('请至少提供问题采购明细、原材料 ID 或完整的到货日期范围')
    return
  }
  impactLoading.value = true
  impactError.value = ''
  impactSearched.value = true
  try {
    impactResult.value = await traceService.analyzeQualityImpact({
      itemIds,
      materialId,
      receiveDateEnd,
      receiveDateStart,
    })
  } catch (requestError) {
    impactResult.value = undefined
    impactError.value = getErrorMessage(requestError, '质量影响分析失败')
  } finally {
    impactLoading.value = false
  }
}

function resetImpactFilters() {
  Object.assign(impactFilters, { itemIds: '', materialId: '', receiveRange: [] })
  impactResult.value = undefined
  impactSearched.value = false
  impactError.value = ''
}

onMounted(() => void loadConsumption())
</script>

<template>
  <PageContainer>
    <PageHeader
      title="质量追溯"
      description="维护批次消耗关系，并按成品或原材料进行正向、反向追溯与质量影响分析。"
    />

    <el-tabs v-model="activeTab" class="trace-tabs">
      <!-- 批次消耗关系 -->
      <el-tab-pane label="批次消耗" name="consumption">
        <el-card class="trace-search-card" shadow="never">
          <el-form :model="consumptionFilters" inline @submit.prevent="loadConsumption(1)">
            <el-form-item label="生产订单 ID">
              <el-input
                v-model.trim="consumptionFilters.orderId"
                clearable
                placeholder="精确查询"
              />
            </el-form-item>
            <el-form-item label="采购明细 ID">
              <el-input v-model.trim="consumptionFilters.itemId" clearable placeholder="精确查询" />
            </el-form-item>
            <el-form-item label="原材料 ID">
              <el-input
                v-model.trim="consumptionFilters.materialId"
                clearable
                placeholder="精确查询"
              />
            </el-form-item>
            <el-form-item>
              <el-button :loading="consumptionLoading" type="primary" @click="loadConsumption(1)">
                查询
              </el-button>
              <el-button
                :disabled="consumptionLoading"
                :icon="Refresh"
                @click="resetConsumptionFilters"
              >
                重置
              </el-button>
              <el-button
                v-if="canManage"
                :icon="Plus"
                type="primary"
                @click="openConsumptionCreate"
              >
                新增消耗
              </el-button>
            </el-form-item>
          </el-form>
        </el-card>

        <el-card class="trace-table-card" shadow="never">
          <el-alert
            v-if="consumptionError"
            class="trace-request-error"
            :closable="false"
            show-icon
            :title="consumptionError"
            type="error"
          >
            <template #default>
              <el-button link type="primary" @click="loadConsumption(consumptionPage)">
                重新加载
              </el-button>
            </template>
          </el-alert>

          <el-table
            v-else
            v-loading="consumptionLoading"
            :data="consumptionResult.items"
            min-height="320"
            stripe
          >
            <el-table-column label="消耗 ID" min-width="90" prop="consumptionId" />
            <el-table-column label="生产订单" min-width="110">
              <template #default="{ row }">{{ `#${row.orderId}` }}</template>
            </el-table-column>
            <el-table-column label="成品物料" min-width="150">
              <template #default="{ row }">{{ row.productMaterialName || '-' }}</template>
            </el-table-column>
            <el-table-column label="生产状态" min-width="110">
              <template #default="{ row }">
                <StatusTag
                  v-if="row.productionStatus"
                  :labels="orderStatusLabels"
                  :value="row.productionStatus"
                />
                <span v-else>-</span>
              </template>
            </el-table-column>
            <el-table-column label="采购明细" min-width="110">
              <template #default="{ row }">{{ `#${row.itemId}` }}</template>
            </el-table-column>
            <el-table-column label="原材料" min-width="150">
              <template #default="{ row }">{{ row.materialName || '-' }}</template>
            </el-table-column>
            <el-table-column label="消耗数量" min-width="110">
              <template #default="{ row }">{{ formatNumber(row.consumeQty) }}</template>
            </el-table-column>
            <el-table-column v-if="canManage" fixed="right" label="操作" min-width="140">
              <template #default="{ row }">
                <el-button link type="primary" :icon="EditPen" @click="openConsumptionEdit(row)">
                  修改
                </el-button>
                <el-button
                  link
                  type="danger"
                  :icon="Delete"
                  :disabled="consumptionDeleting"
                  @click="removeConsumption(row)"
                >
                  删除
                </el-button>
              </template>
            </el-table-column>
          </el-table>

          <el-empty
            v-if="!consumptionLoading && !consumptionError && !consumptionResult.items.length"
            description="暂无符合条件的批次消耗记录"
          />

          <div v-if="!consumptionError && consumptionResult.total > 0" class="trace-pagination">
            <el-pagination
              v-model:current-page="consumptionPage"
              background
              layout="total, prev, pager, next"
              :page-size="pageSize"
              :total="consumptionResult.total"
              @current-change="loadConsumption"
            />
          </div>
        </el-card>
      </el-tab-pane>

      <!-- 正向追溯 -->
      <el-tab-pane label="正向追溯" name="product">
        <el-card class="trace-search-card" shadow="never">
          <el-form :model="productFilters" inline @submit.prevent="traceProduct">
            <el-form-item label="生产订单 ID">
              <el-input
                v-model.trim="productFilters.orderId"
                clearable
                placeholder="与批次号二选一"
              />
            </el-form-item>
            <el-form-item label="成品批次号">
              <el-input
                v-model.trim="productFilters.batchNo"
                clearable
                placeholder="与订单二选一"
              />
            </el-form-item>
            <el-form-item label="含供应商">
              <el-switch v-model="productFilters.includeSupplier" />
            </el-form-item>
            <el-form-item>
              <el-button
                :icon="Search"
                :loading="productLoading"
                type="primary"
                @click="traceProduct"
              >
                追溯
              </el-button>
              <el-button :disabled="productLoading" :icon="Refresh" @click="resetProductFilters">
                重置
              </el-button>
            </el-form-item>
          </el-form>
        </el-card>

        <el-card v-loading="productLoading" class="trace-table-card" shadow="never">
          <el-alert
            v-if="productError"
            :closable="false"
            show-icon
            :title="productError"
            type="error"
          />
          <template v-else-if="productResult">
            <el-descriptions border :column="3" class="trace-summary" title="成品批次">
              <el-descriptions-item label="生产订单">{{
                `#${productResult.orderId}`
              }}</el-descriptions-item>
              <el-descriptions-item label="成品物料">
                {{ productResult.materialName || `#${productResult.materialId}` }}
              </el-descriptions-item>
              <el-descriptions-item label="批次号">{{
                productResult.batchNo || '-'
              }}</el-descriptions-item>
            </el-descriptions>
            <el-table :data="productResult.consumedBatches" stripe>
              <el-table-column label="采购明细" min-width="100">
                <template #default="{ row }">{{ `#${row.itemId}` }}</template>
              </el-table-column>
              <el-table-column label="原材料" min-width="150">
                <template #default="{ row }">{{
                  row.materialName || `#${row.materialId}`
                }}</template>
              </el-table-column>
              <el-table-column label="供应商" min-width="150">
                <template #default="{ row }">{{ row.supplierName || '-' }}</template>
              </el-table-column>
              <el-table-column label="采购订单" min-width="110">
                <template #default="{ row }">{{ row.orderId ? `#${row.orderId}` : '-' }}</template>
              </el-table-column>
              <el-table-column label="到货日期" min-width="120">
                <template #default="{ row }">{{ formatDateTime(row.receiveDate) }}</template>
              </el-table-column>
              <el-table-column label="消耗数量" min-width="110">
                <template #default="{ row }">{{ formatNumber(row.consumeQty) }}</template>
              </el-table-column>
            </el-table>
            <el-empty
              v-if="!productResult.consumedBatches.length"
              description="该成品批次暂无原材料消耗记录"
            />
          </template>
          <el-empty v-else-if="productSearched" description="未查询到匹配的成品批次" />
          <el-empty v-else description="请输入生产订单 ID 或成品批次号后开始正向追溯" />
        </el-card>
      </el-tab-pane>

      <!-- 反向追溯 -->
      <el-tab-pane label="反向追溯" name="material">
        <el-card class="trace-search-card" shadow="never">
          <el-form :model="materialFilters" inline @submit.prevent="traceMaterial">
            <el-form-item label="采购明细 ID">
              <el-input v-model.trim="materialFilters.itemId" clearable placeholder="可选" />
            </el-form-item>
            <el-form-item label="原材料 ID">
              <el-input v-model.trim="materialFilters.materialId" clearable placeholder="可选" />
            </el-form-item>
            <el-form-item label="到货日期">
              <el-date-picker
                v-model="materialFilters.receiveRange"
                end-placeholder="结束日期"
                range-separator="至"
                start-placeholder="开始日期"
                type="daterange"
                value-format="YYYY-MM-DD"
              />
            </el-form-item>
            <el-form-item>
              <el-button
                :icon="Search"
                :loading="materialLoading"
                type="primary"
                @click="traceMaterial"
              >
                追溯
              </el-button>
              <el-button :disabled="materialLoading" :icon="Refresh" @click="resetMaterialFilters">
                重置
              </el-button>
            </el-form-item>
          </el-form>
        </el-card>

        <el-card v-loading="materialLoading" class="trace-table-card" shadow="never">
          <el-alert
            v-if="materialError"
            :closable="false"
            show-icon
            :title="materialError"
            type="error"
          />
          <template v-else-if="materialResult.length">
            <el-card
              v-for="batch in materialResult"
              :key="`${batch.itemId}-${batch.materialId}`"
              class="material-batch-card"
              shadow="never"
            >
              <template #header>
                <div class="material-batch-header">
                  <span>{{ batch.materialName || `原材料 #${batch.materialId}` }}</span>
                  <span class="material-batch-meta">
                    采购明细 #{{ batch.itemId }} · 供应商 {{ batch.supplierName || '-' }}
                  </span>
                </div>
              </template>
              <el-table :data="batch.affectedProducts" stripe>
                <el-table-column label="生产订单" min-width="110">
                  <template #default="{ row }">{{ `#${row.orderId}` }}</template>
                </el-table-column>
                <el-table-column label="成品物料" min-width="150">
                  <template #default="{ row }">
                    {{ row.productMaterialName || `#${row.productMaterialId}` }}
                  </template>
                </el-table-column>
                <el-table-column label="批次号" min-width="130">
                  <template #default="{ row }">{{ row.batchNo || '-' }}</template>
                </el-table-column>
                <el-table-column label="生产状态" min-width="110">
                  <template #default="{ row }">
                    <StatusTag
                      v-if="row.productionStatus"
                      :labels="orderStatusLabels"
                      :value="row.productionStatus"
                    />
                    <span v-else>-</span>
                  </template>
                </el-table-column>
                <el-table-column label="消耗数量" min-width="110">
                  <template #default="{ row }">{{ formatNumber(row.consumeQty) }}</template>
                </el-table-column>
              </el-table>
              <el-empty
                v-if="!batch.affectedProducts.length"
                description="该批次暂无流入的成品"
                :image-size="60"
              />
            </el-card>
          </template>
          <el-empty v-else-if="materialSearched" description="未查询到匹配的原材料批次" />
          <el-empty v-else description="请输入查询条件后开始反向追溯" />
        </el-card>
      </el-tab-pane>

      <!-- 质量影响分析 -->
      <el-tab-pane label="质量影响分析" name="impact">
        <el-card class="trace-search-card" shadow="never">
          <el-form :model="impactFilters" inline @submit.prevent="analyzeImpact">
            <el-form-item label="问题采购明细">
              <el-input
                v-model.trim="impactFilters.itemIds"
                clearable
                placeholder="多个 ID 用逗号分隔"
              />
            </el-form-item>
            <el-form-item label="原材料 ID">
              <el-input v-model.trim="impactFilters.materialId" clearable placeholder="可选" />
            </el-form-item>
            <el-form-item label="到货日期">
              <el-date-picker
                v-model="impactFilters.receiveRange"
                end-placeholder="结束日期"
                range-separator="至"
                start-placeholder="开始日期"
                type="daterange"
                value-format="YYYY-MM-DD"
              />
            </el-form-item>
            <el-form-item>
              <el-button
                :icon="Search"
                :loading="impactLoading"
                type="primary"
                @click="analyzeImpact"
              >
                分析
              </el-button>
              <el-button :disabled="impactLoading" :icon="Refresh" @click="resetImpactFilters">
                重置
              </el-button>
            </el-form-item>
          </el-form>
        </el-card>

        <el-card v-loading="impactLoading" class="trace-table-card" shadow="never">
          <el-alert
            v-if="impactError"
            :closable="false"
            show-icon
            :title="impactError"
            type="error"
          />
          <template v-else-if="impactResult">
            <div class="impact-summary">
              <div class="impact-metric">
                <span>受影响订单</span>
                <strong>{{ formatNumber(impactResult.affectedOrderCount) }}</strong>
              </div>
              <div class="impact-metric">
                <span>受影响批次</span>
                <strong>{{ formatNumber(impactResult.affectedBatchCount) }}</strong>
              </div>
              <div class="impact-metric">
                <span>建议动作</span>
                <el-tag
                  v-if="impactResult.suggestedAction"
                  effect="light"
                  :type="suggestedActionTone[impactResult.suggestedAction]"
                >
                  {{ suggestedActionLabels[impactResult.suggestedAction] }}
                </el-tag>
                <strong v-else>-</strong>
              </div>
            </div>
            <el-table :data="impactResult.affectedProducts" stripe>
              <el-table-column label="生产订单" min-width="110">
                <template #default="{ row }">{{ `#${row.orderId}` }}</template>
              </el-table-column>
              <el-table-column label="成品物料" min-width="150">
                <template #default="{ row }">
                  {{ row.productMaterialName || `#${row.productMaterialId}` }}
                </template>
              </el-table-column>
              <el-table-column label="批次号" min-width="130">
                <template #default="{ row }">{{ row.batchNo || '-' }}</template>
              </el-table-column>
              <el-table-column label="生产状态" min-width="110">
                <template #default="{ row }">
                  <StatusTag
                    v-if="row.productionStatus"
                    :labels="orderStatusLabels"
                    :value="row.productionStatus"
                  />
                  <span v-else>-</span>
                </template>
              </el-table-column>
              <el-table-column label="消耗数量" min-width="110">
                <template #default="{ row }">{{ formatNumber(row.consumeQty) }}</template>
              </el-table-column>
            </el-table>
            <el-empty
              v-if="!impactResult.affectedProducts.length"
              description="未发现受影响的成品批次"
            />
          </template>
          <el-empty v-else-if="impactSearched" description="未查询到质量影响分析结果" />
          <el-empty v-else description="请输入问题批次条件后开始分析" />
        </el-card>
      </el-tab-pane>
    </el-tabs>

    <el-dialog
      v-model="consumptionDialogVisible"
      :close-on-click-modal="false"
      :title="consumptionDialogTitle"
      width="480px"
    >
      <el-form
        ref="consumptionFormRef"
        label-width="110px"
        :model="consumptionForm"
        :rules="consumptionRules"
      >
        <el-form-item label="生产订单 ID" prop="orderId">
          <el-input-number v-model="consumptionForm.orderId" :min="1" style="width: 100%" />
        </el-form-item>
        <el-form-item label="采购明细 ID" prop="itemId">
          <el-input-number v-model="consumptionForm.itemId" :min="1" style="width: 100%" />
        </el-form-item>
        <el-form-item label="消耗数量" prop="consumeQty">
          <el-input-number
            v-model="consumptionForm.consumeQty"
            :min="0.01"
            :precision="2"
            :step="1"
            style="width: 100%"
          />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="consumptionDialogVisible = false">取消</el-button>
        <el-button :loading="consumptionSubmitting" type="primary" @click="submitConsumptionForm">
          确定
        </el-button>
      </template>
    </el-dialog>
  </PageContainer>
</template>

<style scoped>
.trace-tabs {
  min-width: 0;
}
.trace-search-card {
  margin-bottom: 16px;
  min-width: 0;
}
.trace-table-card {
  min-width: 0;
}
.trace-request-error {
  margin-bottom: 16px;
}
.trace-pagination {
  display: flex;
  justify-content: flex-end;
  margin-top: 16px;
}
.trace-summary {
  margin-bottom: 16px;
}
.material-batch-card {
  margin-bottom: 16px;
}
.material-batch-card:last-child {
  margin-bottom: 0;
}
.material-batch-header {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: 12px;
}
.material-batch-meta {
  color: var(--el-text-color-secondary);
  font-size: 13px;
}
.impact-summary {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 16px;
  margin-bottom: 16px;
}
.impact-metric {
  display: flex;
  flex-direction: column;
  gap: 8px;
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 6px;
  background: var(--el-fill-color-lighter);
  padding: 14px 16px;
}
.impact-metric span {
  color: var(--el-text-color-secondary);
  font-size: 13px;
}
.impact-metric strong {
  font-size: 24px;
}
@media (max-width: 768px) {
  .impact-summary {
    grid-template-columns: 1fr;
  }
}
</style>
