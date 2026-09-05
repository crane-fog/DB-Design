<script setup lang="ts">
import {
  type BatchConsumptionCreateFormData,
  type BatchConsumptionItem,
  type BatchConsumptionUpdateFormData,
  type MaterialBatchTraceItem,
  type TraceConsumptionReferenceData,
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
import type { ProductionOrderStatus } from '@/api'
import { getErrorMessage } from '@/utils/error'
import { parsePositiveInt } from '@/utils/parse'
import { useAuthStore } from '@/stores/auth'
import { useRoute } from 'vue-router'

type TraceTab = 'consumption' | 'material' | 'product'

const { section } = defineProps<{ section: TraceTab }>()
const pageSize = 10
const auth = useAuthStore()
const route = useRoute()
const canManage = computed(() => auth.hasPermission(PERMISSIONS.trace.manage))
const references = ref<TraceConsumptionReferenceData>({})
const suppliers = computed(() => references.value.suppliers ?? [])
const referenceError = ref('')
const referenceLoading = ref(false)
const sectionMetadata: Record<TraceTab, { description: string; title: string }> = {
  consumption: {
    description: '维护生产订单与采购明细之间的真实批次消耗关系。',
    title: '批次消耗',
  },
  material: {
    description: '从原材料采购批次反向查询受影响的产品批次与生产订单。',
    title: '反向追溯',
  },
  product: {
    description: '从成品批次或生产订单追溯所使用的原材料采购批次。',
    title: '正向追溯',
  },
}
const currentSection = computed(() => sectionMetadata[section])

async function loadReferences() {
  referenceLoading.value = true
  try {
    referenceError.value = ''
    references.value = await traceService.listConsumptionReferences()
  } catch (error) {
    references.value = {}
    referenceError.value = getErrorMessage(error, '追溯参考数据加载失败')
  } finally {
    referenceLoading.value = false
  }
}

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
    {
      message: '请输入有效采购明细 ID',
      min: 1,
      required: true,
      trigger: 'change',
      type: 'integer',
    },
  ],
  orderId: [
    {
      message: '请输入有效生产订单 ID',
      min: 1,
      required: true,
      trigger: 'change',
      type: 'integer',
    },
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
  } catch (error) {
    consumptionError.value = getErrorMessage(error, '批次消耗列表加载失败')
  } finally {
    consumptionLoading.value = false
  }
}

function resetConsumptionFilters() {
  Object.assign(consumptionFilters, { itemId: '', materialId: '', orderId: '' })
  void loadConsumption(1)
}

function resetConsumptionForm() {
  Object.assign(consumptionForm, { consumeQty: 1, itemId: 0, orderId: 0 })
}

function openConsumptionCreate() {
  consumptionDialogMode.value = 'create'
  editingConsumptionId.value = undefined
  resetConsumptionForm()
  consumptionFormRef.value?.clearValidate()
  consumptionDialogVisible.value = true
}

function openConsumptionEdit(record: BatchConsumptionItem) {
  consumptionDialogMode.value = 'edit'
  editingConsumptionId.value = record.consumptionId
  Object.assign(consumptionForm, {
    consumeQty: record.consumeQty,
    itemId: record.itemId,
    orderId: record.orderId,
  })
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
    } else {
      throw new Error('消耗记录 ID 无效，请重新打开编辑窗口')
    }
    consumptionDialogVisible.value = false
    await loadConsumption(consumptionPage.value)
  } catch (error) {
    ElMessage.error(getErrorMessage(error, '批次消耗提交失败'))
  } finally {
    consumptionSubmitting.value = false
  }
}

async function removeConsumption(record: BatchConsumptionItem) {
  if (consumptionDeleting.value) {
    return
  }
  try {
    await ElMessageBox.confirm(`确定要删除消耗记录 #${record.consumptionId} 吗？`, '删除批次消耗', {
      confirmButtonText: '继续',
      type: 'warning',
    })
    await ElMessageBox.confirm('删除后无法恢复，是否再次确认删除？', '二次确认', {
      confirmButtonText: '确定删除',
      type: 'error',
    })
    consumptionDeleting.value = true
    await traceService.deleteBatchConsumption(record.consumptionId)
    let targetPage = consumptionPage.value
    if (consumptionResult.value.items.length === 1 && consumptionPage.value > 1) {
      targetPage -= 1
    }
    ElMessage.success('批次消耗已删除')
    await loadConsumption(targetPage)
  } catch (error) {
    if (error !== 'cancel' && error !== 'close') {
      ElMessage.error(getErrorMessage(error, '批次消耗删除失败'))
    }
  } finally {
    consumptionDeleting.value = false
  }
}

// ---------- 正向追溯（成品 → 原材料） ----------
const productFilters = reactive({ batchNo: '', orderId: '' })
const productLoading = ref(false)
const productError = ref('')
const productSearched = ref(false)
const productResult = ref<Awaited<ReturnType<typeof traceService.traceProductBatch>>>()

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
      orderId,
    })
  } catch (error) {
    productResult.value = undefined
    productError.value = getErrorMessage(error, '正向追溯失败')
  } finally {
    productLoading.value = false
  }
}

function resetProductFilters() {
  Object.assign(productFilters, { batchNo: '', orderId: '' })
  productResult.value = undefined
  productSearched.value = false
  productError.value = ''
}

// ---------- 反向追溯（原材料 → 成品） ----------
const materialFilters = reactive({
  itemId: '',
  materialId: '',
  receiveRange: [] as string[],
  supplierId: undefined as number | undefined,
})
const materialLoading = ref(false)
const materialError = ref('')
const materialSearched = ref(false)
const materialResult = ref<MaterialBatchTraceItem[]>([])

async function traceMaterial() {
  const itemId = parsePositiveInt(materialFilters.itemId)
  const materialId = parsePositiveInt(materialFilters.materialId)
  const [receiveDateStart, receiveDateEnd] = materialFilters.receiveRange
  if (
    !itemId &&
    !materialId &&
    !materialFilters.supplierId &&
    !(receiveDateStart && receiveDateEnd)
  ) {
    ElMessage.warning('请至少提供采购明细、原材料、供应商或完整到货日期范围')
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
      supplierId: materialFilters.supplierId,
    })
  } catch (error) {
    materialResult.value = []
    materialError.value = getErrorMessage(error, '反向追溯失败')
  } finally {
    materialLoading.value = false
  }
}

function resetMaterialFilters() {
  Object.assign(materialFilters, {
    itemId: '',
    materialId: '',
    receiveRange: [],
    supplierId: undefined,
  })
  materialResult.value = []
  materialSearched.value = false
  materialError.value = ''
}

const productionStatusLabels: Record<ProductionOrderStatus, string> = {
  cancelled: '已取消',
  completed: '已完工',
  in_progress: '生产中',
  pending_review: '待审核',
  pending_schedule: '待排产',
}

function getProductionStatusLabel(status?: ProductionOrderStatus) {
  if (!status) {
    return '-'
  }
  return productionStatusLabels[status]
}

onMounted(async () => {
  const initialRequests: Promise<void>[] = []
  if (section === 'consumption') {
    initialRequests.push(loadConsumption())
  }
  if (section === 'consumption' || section === 'material') {
    initialRequests.push(loadReferences())
  }
  await Promise.all(initialRequests)

  let batchNo = ''
  let orderId = ''
  if (typeof route.query.batchNo === 'string') {
    ;({ batchNo } = route.query)
  }
  if (typeof route.query.orderId === 'string') {
    ;({ orderId } = route.query)
  }
  if (section === 'product' && (batchNo || parsePositiveInt(orderId))) {
    Object.assign(productFilters, { batchNo, orderId })
    await traceProduct()
  }
})
</script>

<template>
  <PageContainer>
    <PageHeader :title="currentSection.title" :description="currentSection.description" />

    <el-alert
      v-if="referenceError"
      class="trace-request-error"
      :closable="false"
      :title="referenceError"
      type="error"
      show-icon
    >
      <el-button link type="primary" :loading="referenceLoading" @click="loadReferences"
        >重新加载参考数据</el-button
      >
    </el-alert>

    <template v-if="section === 'consumption'">
      <el-card class="trace-search-card" shadow="never">
        <el-form :model="consumptionFilters" inline @submit.prevent="loadConsumption(1)">
          <el-form-item label="生产订单 ID">
            <el-input v-model.trim="consumptionFilters.orderId" clearable placeholder="" />
          </el-form-item>
          <el-form-item label="采购明细 ID">
            <el-input v-model.trim="consumptionFilters.itemId" clearable placeholder="" />
          </el-form-item>
          <el-form-item label="原材料 ID">
            <el-input v-model.trim="consumptionFilters.materialId" clearable placeholder="" />
          </el-form-item>
          <el-form-item>
            <el-button :loading="consumptionLoading" type="primary" @click="loadConsumption(1)"
              >查询</el-button
            >
            <el-button
              :disabled="consumptionLoading"
              :icon="Refresh"
              @click="resetConsumptionFilters"
              >重置</el-button
            >
            <el-button v-if="canManage" :icon="Plus" type="primary" @click="openConsumptionCreate"
              >新增消耗</el-button
            >
          </el-form-item>
        </el-form>
      </el-card>
      <el-card class="trace-table-card table-card" shadow="never">
        <el-alert
          v-if="consumptionError"
          class="trace-request-error"
          :closable="false"
          show-icon
          :title="consumptionError"
          type="error"
        >
          <el-button link type="primary" @click="loadConsumption(consumptionPage)"
            >重新加载</el-button
          >
        </el-alert>
        <el-table v-else v-loading="consumptionLoading" :data="consumptionResult.items" stripe>
          <el-table-column label="消耗 ID" min-width="90" prop="consumptionId" />
          <el-table-column label="生产订单 ID / 产品" min-width="180">
            <template #default="{ row }">
              <p class="cell-sub">#{{ row.orderId }}</p>
              <p>{{ row.productMaterialName || '-' }}</p>
            </template>
          </el-table-column>
          <el-table-column label="采购明细 ID / 原材料" min-width="185">
            <template #default="{ row }">
              <p class="cell-sub">#{{ row.itemId }}</p>
              <p>{{ row.materialName || '-' }}</p>
            </template>
          </el-table-column>
          <el-table-column label="采购订单 ID" min-width="120">
            <template #default="{ row }">{{
              row.purchaseOrderId ? '#' + row.purchaseOrderId : '-'
            }}</template>
          </el-table-column>
          <el-table-column label="生产状态" min-width="110">
            <template #default="{ row }">{{
              getProductionStatusLabel(row.productionStatus)
            }}</template>
          </el-table-column>
          <el-table-column label="消耗数量" min-width="120">
            <template #default="{ row }">{{ formatNumber(row.consumeQty) }}</template>
          </el-table-column>
          <el-table-column v-if="canManage" fixed="right" label="操作" min-width="130">
            <template #default="{ row }">
              <el-button
                v-if="canManage"
                link
                type="primary"
                :icon="EditPen"
                @click="openConsumptionEdit(row)"
                >修改</el-button
              >
              <el-button
                v-if="canManage"
                link
                type="danger"
                :icon="Delete"
                :disabled="consumptionDeleting"
                @click="removeConsumption(row)"
                >删除</el-button
              >
            </template>
          </el-table-column>
        </el-table>
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
    </template>

    <template v-if="section === 'product'">
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
            <el-input v-model.trim="productFilters.batchNo" clearable placeholder="与订单二选一" />
          </el-form-item>
          <el-form-item>
            <el-button :icon="Search" :loading="productLoading" type="primary" @click="traceProduct"
              >追溯</el-button
            >
            <el-button :disabled="productLoading" :icon="Refresh" @click="resetProductFilters"
              >重置</el-button
            >
          </el-form-item>
        </el-form>
      </el-card>
      <el-card v-loading="productLoading" class="trace-table-card table-card" shadow="never">
        <el-alert
          v-if="productError"
          :closable="false"
          show-icon
          :title="productError"
          type="error"
        />
        <template v-else-if="productResult">
          <el-descriptions border :column="3" class="trace-summary" title="成品批次">
            <el-descriptions-item label="生产订单 ID"
              >#{{ productResult.orderId }}</el-descriptions-item
            >
            <el-descriptions-item label="成品批次">{{
              productResult.batchNo || '-'
            }}</el-descriptions-item>
            <el-descriptions-item label="产品">{{
              productResult.materialName || '#' + productResult.materialId
            }}</el-descriptions-item>
            <el-descriptions-item v-if="productResult.bomVersion" label="BOM 版本">{{
              productResult.bomVersion
            }}</el-descriptions-item>
            <el-descriptions-item v-if="productResult.planQty !== undefined" label="订单计划数量">{{
              formatNumber(productResult.planQty)
            }}</el-descriptions-item>
            <el-descriptions-item
              v-if="productResult.finishedQty !== undefined"
              label="订单完工数量"
              >{{ formatNumber(productResult.finishedQty) }}</el-descriptions-item
            >
            <el-descriptions-item v-if="productResult.producedAt" label="订单完工日期">{{
              formatDateTime(productResult.producedAt)
            }}</el-descriptions-item>
          </el-descriptions>
          <el-table :data="productResult.consumedBatches" stripe>
            <el-table-column label="采购明细 / 原材料" min-width="190">
              <template #default="{ row }">
                <strong>明细 #{{ row.itemId }}</strong>
                <small class="cell-sub">{{ row.materialName || '#' + row.materialId }}</small>
              </template>
            </el-table-column>
            <el-table-column label="采购订单 ID" min-width="120">
              <template #default="{ row }">{{
                row.purchaseOrderId ? '#' + row.purchaseOrderId : '-'
              }}</template>
            </el-table-column>
            <el-table-column label="供应商" min-width="150">
              <template #default="{ row }">{{ row.supplierName || '-' }}</template>
            </el-table-column>
            <el-table-column label="到货日期" min-width="125">
              <template #default="{ row }">{{ formatDateTime(row.receiveDate) }}</template>
            </el-table-column>
            <el-table-column label="消耗数量" min-width="115">
              <template #default="{ row }">{{ formatNumber(row.consumeQty) }}</template>
            </el-table-column>
          </el-table>
          <template v-if="productResult.inboundRecords">
            <el-divider content-position="left">关联完工入库记录</el-divider>
            <el-table :data="productResult.inboundRecords" stripe>
              <el-table-column label="入库 ID" prop="inbound_id" min-width="90" />
              <el-table-column label="成品批次" prop="batch_no" min-width="145" />
              <el-table-column label="完工数量" min-width="110">
                <template #default="{ row }">{{ formatNumber(row.finish_qty) }}</template>
              </el-table-column>
              <el-table-column label="合格数量" min-width="110">
                <template #default="{ row }">{{ formatNumber(row.qualified_qty) }}</template>
              </el-table-column>
              <el-table-column label="不合格数量" min-width="110">
                <template #default="{ row }">{{
                  formatNumber(row.finish_qty - row.qualified_qty)
                }}</template>
              </el-table-column>
              <el-table-column label="入库时间" min-width="170">
                <template #default="{ row }">{{ formatDateTime(row.inbound_time) }}</template>
              </el-table-column>
            </el-table>
          </template>
        </template>
        <el-empty v-else-if="productSearched" description="未查询到匹配的成品批次" />
        <el-empty v-else description="请输入生产订单 ID 或成品批次号后开始正向追溯" />
      </el-card>
    </template>

    <template v-if="section === 'material'">
      <el-card class="trace-search-card" shadow="never">
        <el-form :model="materialFilters" inline @submit.prevent="traceMaterial">
          <el-form-item label="采购明细 ID">
            <el-input v-model.trim="materialFilters.itemId" clearable placeholder="可选" />
          </el-form-item>
          <el-form-item label="原材料 ID">
            <el-input v-model.trim="materialFilters.materialId" clearable placeholder="可选" />
          </el-form-item>
          <el-form-item label="供应商筛选">
            <el-select
              v-if="references.suppliers"
              v-model="materialFilters.supplierId"
              clearable
              filterable
              :loading="referenceLoading"
              placeholder="选择供应商"
              style="width: 200px"
            >
              <el-option
                v-for="supplier in suppliers"
                :key="supplier.supplierId"
                :label="supplier.supplierName + ' · #' + supplier.supplierId"
                :value="supplier.supplierId"
              />
            </el-select>
            <el-input-number
              :controls="false"
              v-else
              v-model="materialFilters.supplierId"
              :min="1"
              :precision="0"
              placeholder="供应商 ID（可选）"
            />
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
              >追溯</el-button
            >
            <el-button :disabled="materialLoading" :icon="Refresh" @click="resetMaterialFilters"
              >重置</el-button
            >
          </el-form-item>
        </el-form>
      </el-card>
      <el-card v-loading="materialLoading" class="trace-table-card table-card" shadow="never">
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
            :key="batch.itemId"
            class="material-batch-card table-card table-card--accent"
            shadow="never"
          >
            <template #header>
              <div class="material-batch-header table-card__header">
                <strong>{{ batch.materialName || '原材料 #' + batch.materialId }}</strong>
                <span>采购明细 #{{ batch.itemId }} · {{ batch.supplierName || '-' }}</span>
              </div>
            </template>
            <el-table :data="batch.affectedProducts" stripe>
              <el-table-column label="生产订单 ID" min-width="110">
                <template #default="{ row }">#{{ row.orderId }}</template>
              </el-table-column>
              <el-table-column label="产品批次" min-width="155">
                <template #default="{ row }">{{ row.batchNo || '-' }}</template>
              </el-table-column>
              <el-table-column label="产品" min-width="180">
                <template #default="{ row }">{{
                  row.productMaterialName || '#' + row.productMaterialId
                }}</template>
              </el-table-column>
              <el-table-column label="原料消耗数量" min-width="120">
                <template #default="{ row }">{{ formatNumber(row.consumeQty) }}</template>
              </el-table-column>
              <el-table-column label="生产状态" min-width="110">
                <template #default="{ row }">{{
                  getProductionStatusLabel(row.productionStatus)
                }}</template>
              </el-table-column>
            </el-table>
          </el-card>
        </template>
        <el-empty v-else-if="materialSearched" description="未查询到匹配的原材料批次" />
        <el-empty v-else description="请输入查询条件后开始反向追溯" />
      </el-card>
    </template>

    <el-dialog
      v-model="consumptionDialogVisible"
      :close-on-click-modal="false"
      :title="consumptionDialogTitle"
      width="650px"
    >
      <el-form
        ref="consumptionFormRef"
        label-width="120px"
        :model="consumptionForm"
        :rules="consumptionRules"
      >
        <el-form-item label="生产订单 ID" prop="orderId">
          <div class="reference-input">
            <el-input-number
              :controls="false"
              v-model="consumptionForm.orderId"
              :min="1"
              :precision="0"
            />
            <el-select
              v-if="references.productionOrders?.length"
              v-model="consumptionForm.orderId"
              filterable
              :loading="referenceLoading"
              placeholder="选择生产订单 ID"
            >
              <el-option
                v-for="order in references.productionOrders"
                :key="order.orderId"
                :label="
                  '#' +
                  order.orderId +
                  ' · ' +
                  (order.materialName || '-') +
                  ' · ' +
                  getProductionStatusLabel(order.status)
                "
                :value="order.orderId"
              />
            </el-select>
          </div>
        </el-form-item>
        <el-form-item label="采购明细 ID" prop="itemId">
          <div class="reference-input">
            <el-input-number
              :controls="false"
              v-model="consumptionForm.itemId"
              :min="1"
              :precision="0"
            />
            <el-select
              v-if="references.purchaseItems?.length"
              v-model="consumptionForm.itemId"
              filterable
              :loading="referenceLoading"
              placeholder="选择采购明细"
            >
              <el-option
                v-for="item in references.purchaseItems"
                :key="item.itemId"
                :label="
                  '#' +
                  item.itemId +
                  ' · ' +
                  (item.materialName || '-') +
                  ' · 采购订单 #' +
                  item.purchaseOrderId
                "
                :value="item.itemId"
              />
            </el-select>
          </div>
        </el-form-item>
        <el-form-item label="消耗数量" prop="consumeQty">
          <el-input-number
            :controls="false"
            v-model="consumptionForm.consumeQty"
            :min="0.01"
            :precision="2"
          />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button :loading="referenceLoading" @click="loadReferences">刷新参考数据</el-button>
        <el-button :disabled="consumptionSubmitting" @click="consumptionDialogVisible = false"
          >取消</el-button
        >
        <el-button :loading="consumptionSubmitting" type="primary" @click="submitConsumptionForm"
          >确定</el-button
        >
      </template>
    </el-dialog>
  </PageContainer>
</template>

<style scoped>
.trace-search-card,
.trace-table-card {
  min-width: 0;
}
.trace-search-card,
.material-batch-card,
.trace-request-error,
.trace-summary {
  margin-bottom: 16px;
}
.trace-pagination {
  display: flex;
  justify-content: flex-end;
  margin-top: 16px;
}
.cell-sub {
  display: block;
  color: var(--el-text-color-secondary);
  font-size: 12px;
  font-weight: 400;
  margin-top: 3px;
}
.material-batch-header {
  display: flex;
  gap: 12px;
  justify-content: space-between;
}
.material-batch-header span {
  color: var(--el-text-color-secondary);
  font-size: 13px;
}
.reference-input {
  display: flex;
  gap: 8px;
  width: 100%;
}
.reference-input .el-select {
  flex: 1;
  min-width: 0;
}
@media (max-width: 768px) {
  .material-batch-header,
  .reference-input {
    flex-direction: column;
    gap: 4px;
  }
}
</style>
