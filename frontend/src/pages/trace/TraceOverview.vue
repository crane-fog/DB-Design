<script setup lang="ts">
import {
  type BatchConsumptionCreateFormData,
  type BatchConsumptionItem,
  type BatchConsumptionUpdateFormData,
  type MaterialBatchTraceItem,
  type QualityDisposition,
  type QualityDispositionStatus,
  type QualityDispositionType,
  type QualityImpactResult,
  type TraceConsumptionReferenceData,
  type TraceSupplierOption,
  traceService,
} from '@/services/TraceService'
import { Delete, EditPen, Plus, Refresh, Search, View } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import { computed, onMounted, reactive, ref } from 'vue'
import { formatDateTime, formatNumber } from '@/utils/format'
import { PERMISSIONS } from '@/constants/permissions'
import PageContainer from '@/components/common/PageContainer.vue'
import PageHeader from '@/components/common/PageHeader.vue'
import type { PageResult } from '@/services/pagination'
import { getErrorMessage } from '@/utils/error'
import { parsePositiveInt } from '@/utils/parse'
import { qualityDispositionStatuses } from '@/constants/status'
import { useAuthStore } from '@/stores/auth'
import { useRoute } from 'vue-router'

type TraceTab = 'consumption' | 'impact' | 'material' | 'product'

const pageSize = 10
const auth = useAuthStore()
const route = useRoute()
const canManage = computed(() => auth.hasPermission(PERMISSIONS.trace.manage))
const activeTab = ref<TraceTab>('consumption')
const references = ref<TraceConsumptionReferenceData>({ productBatches: [], purchaseItems: [] })
const suppliers = ref<TraceSupplierOption[]>([])
const referenceLoading = ref(false)

async function loadReferences() {
  referenceLoading.value = true
  try {
    const [referenceData, supplierData] = await Promise.all([
      traceService.listConsumptionReferences(),
      traceService.listTraceSuppliers(),
    ])
    references.value = referenceData
    suppliers.value = supplierData
  } catch (error) {
    ElMessage.error(getErrorMessage(error, '追溯参考数据加载失败'))
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
const consumptionDetail = ref<BatchConsumptionItem>()
const consumptionDetailVisible = ref(false)
const consumptionDetailLoading = ref(false)
const consumptionForm = reactive<BatchConsumptionCreateFormData>({
  consumeQty: 1,
  consumedAt: '',
  itemId: 0,
  materialBatchNo: '',
  operatorName: '当前操作员',
  orderId: 0,
  productBatchNo: '',
  purchaseOrderNo: '',
  remarks: '',
  supplierId: 0,
  unit: '',
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
  consumedAt: [
    { message: '请选择消耗时间', required: true, trigger: 'change' },
    {
      message: '消耗时间格式无效',
      trigger: 'change',
      validator: (_rule, value, callback) => {
        if (Date.parse(String(value).replace(' ', 'T'))) {
          callback()
          return
        }
        callback(new Error('消耗时间格式无效'))
      },
    },
  ],
  itemId: [{ message: '请选择采购明细与原材料批次', required: true, trigger: 'change' }],
  operatorName: [{ message: '请输入操作人', required: true, trigger: 'blur' }],
  productBatchNo: [{ message: '请选择生产订单与产品批次', required: true, trigger: 'change' }],
  supplierId: [{ message: '请选择供应商', required: true, trigger: 'change' }],
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
  Object.assign(consumptionForm, {
    consumeQty: 1,
    consumedAt: '',
    itemId: 0,
    materialBatchNo: '',
    operatorName: '当前操作员',
    orderId: 0,
    productBatchNo: '',
    purchaseOrderNo: '',
    remarks: '',
    supplierId: 0,
    unit: '',
  })
}

function onPurchaseItemChange(itemId: number) {
  const item = references.value.purchaseItems.find((candidate) => candidate.itemId === itemId)
  if (!item) {
    return
  }
  Object.assign(consumptionForm, {
    itemId: item.itemId,
    materialBatchNo: item.materialBatchNo,
    purchaseOrderNo: item.purchaseOrderNo,
    supplierId: item.supplierId,
    unit: item.unit,
  })
}

function onProductBatchChange(batchNo: string) {
  const batch = references.value.productBatches.find((candidate) => candidate.batchNo === batchNo)
  if (!batch) {
    return
  }
  Object.assign(consumptionForm, { orderId: batch.orderId, productBatchNo: batch.batchNo })
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
    consumedAt: record.consumedAt ?? '',
    itemId: record.itemId,
    materialBatchNo: record.materialBatchNo ?? '',
    operatorName: record.operatorName ?? '当前操作员',
    orderId: record.orderId,
    productBatchNo: record.productBatchNo ?? '',
    purchaseOrderNo: record.purchaseOrderNo ?? '',
    remarks: record.remarks ?? '',
    supplierId: record.supplierId ?? 0,
    unit: record.unit ?? '',
  })
  consumptionFormRef.value?.clearValidate()
  consumptionDialogVisible.value = true
}

async function openConsumptionDetail(record: BatchConsumptionItem) {
  consumptionDetailVisible.value = true
  consumptionDetailLoading.value = true
  try {
    consumptionDetail.value = await traceService.getBatchConsumption(record.consumptionId)
  } catch (error) {
    consumptionDetail.value = undefined
    ElMessage.error(getErrorMessage(error, '批次消耗详情加载失败'))
  } finally {
    consumptionDetailLoading.value = false
  }
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
const productFilters = reactive({ batchNo: '', includeSupplier: true, orderId: '' })
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
      includeSupplier: productFilters.includeSupplier,
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
  Object.assign(productFilters, { batchNo: '', includeSupplier: true, orderId: '' })
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

// ---------- 质量影响分析与 Mock 处置 ----------
const impactFilters = reactive({ itemIds: '', materialId: '', receiveRange: [] as string[] })
const impactLoading = ref(false)
const impactError = ref('')
const impactSearched = ref(false)
const impactResult = ref<QualityImpactResult>()
const impactDispositions = ref<QualityDisposition[]>([])
const dispositionDialogVisible = ref(false)
const dispositionSubmitting = ref(false)
const dispositionFormRef = ref<FormInstance>()
const dispositionType = ref<QualityDispositionType>('freeze')
const dispositionTarget = ref<QualityImpactResult['affectedProducts'][number]>()
const dispositionForm = reactive({
  affectedQty: 0,
  handlingInstruction: '',
  note: '',
  operatorName: '当前操作员',
  reason: '',
  recallScope: '',
})
const dispositionTitle = computed(() => {
  if (dispositionType.value === 'freeze') {
    return '冻结受影响批次'
  }
  return '发起质量召回'
})
const dispositionRules: FormRules = {
  operatorName: [{ message: '请输入操作人', required: true, trigger: 'blur' }],
  reason: [{ message: '请输入处置原因', required: true, trigger: 'blur' }],
  recallScope: [{ message: '请输入召回范围', required: true, trigger: 'blur' }],
}

function parseItemIds(value: string) {
  return value
    .split(/[\s,，]+/)
    .map((token) => Number(token.trim()))
    .filter((id) => Number.isInteger(id) && id > 0)
}

async function loadDispositions() {
  impactDispositions.value = await traceService.listQualityDispositions()
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
    const [result] = await Promise.all([
      traceService.analyzeQualityImpact({ itemIds, materialId, receiveDateEnd, receiveDateStart }),
      loadDispositions(),
    ])
    impactResult.value = result
  } catch (error) {
    impactResult.value = undefined
    impactError.value = getErrorMessage(error, '质量影响分析失败')
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

function openDisposition(
  type: QualityDispositionType,
  target: QualityImpactResult['affectedProducts'][number],
) {
  dispositionType.value = type
  dispositionTarget.value = target
  Object.assign(dispositionForm, {
    affectedQty: target.finishedQty ?? 0,
    handlingInstruction: '',
    note: '',
    operatorName: '当前操作员',
    reason: '',
    recallScope: '',
  })
  dispositionFormRef.value?.clearValidate()
  dispositionDialogVisible.value = true
}

async function submitDisposition() {
  const valid = await dispositionFormRef.value?.validate().catch(() => false)
  const target = dispositionTarget.value
  if (!valid || !target?.batchNo || dispositionSubmitting.value) {
    return
  }
  try {
    let actionName = '召回'
    if (dispositionType.value === 'freeze') {
      actionName = '冻结'
    }
    await ElMessageBox.confirm(`确认${actionName}批次 ${target.batchNo} 吗？`, '二次确认', {
      confirmButtonText: '确认提交',
      type: 'warning',
    })
    dispositionSubmitting.value = true
    const form = { batchNo: target.batchNo, ...dispositionForm }
    if (dispositionType.value === 'freeze') {
      await traceService.freezeBatch(form)
      ElMessage.success('批次已冻结')
    } else {
      await traceService.recallBatch(form)
      ElMessage.success('召回记录已创建')
    }
    dispositionDialogVisible.value = false
    await analyzeImpact()
  } catch (error) {
    if (error !== 'cancel' && error !== 'close') {
      ElMessage.error(getErrorMessage(error, '质量处置提交失败'))
    }
  } finally {
    dispositionSubmitting.value = false
  }
}

function getQualityStatusLabel(status?: QualityDispositionStatus) {
  if (!status) {
    return '-'
  }
  return qualityDispositionStatuses[status].label
}

function getQualityStatusTone(status?: QualityDispositionStatus) {
  if (!status) {
    return 'info'
  }
  return qualityDispositionStatuses[status].tone
}

onMounted(async () => {
  await Promise.all([loadConsumption(), loadReferences()])

  let batchNo = ''
  let orderId = ''
  if (typeof route.query.batchNo === 'string') {
    ;({ batchNo } = route.query)
  }
  if (typeof route.query.orderId === 'string') {
    ;({ orderId } = route.query)
  }
  if (route.query.tab === 'product' && (batchNo || parsePositiveInt(orderId))) {
    activeTab.value = 'product'
    Object.assign(productFilters, { batchNo, orderId })
    await traceProduct()
  }
})
</script>

<template>
  <PageContainer>
    <PageHeader
      title="质量追溯"
      description="维护批次消耗关系，并完成正反向追溯、质量影响分析和 Mock 质量处置。"
    />

    <el-tabs v-model="activeTab" class="trace-tabs">
      <el-tab-pane label="批次消耗" name="consumption">
        <el-card class="trace-search-card" shadow="never">
          <el-form :model="consumptionFilters" inline @submit.prevent="loadConsumption(1)">
            <el-form-item label="生产订单 ID"
              ><el-input v-model.trim="consumptionFilters.orderId" clearable placeholder="精确查询"
            /></el-form-item>
            <el-form-item label="采购明细 ID"
              ><el-input v-model.trim="consumptionFilters.itemId" clearable placeholder="精确查询"
            /></el-form-item>
            <el-form-item label="原材料 ID"
              ><el-input
                v-model.trim="consumptionFilters.materialId"
                clearable
                placeholder="精确查询"
            /></el-form-item>
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
        <el-card class="trace-table-card" shadow="never">
          <el-alert
            v-if="consumptionError"
            class="trace-request-error"
            :closable="false"
            show-icon
            :title="consumptionError"
            type="error"
            ><template #default
              ><el-button link type="primary" @click="loadConsumption(consumptionPage)"
                >重新加载</el-button
              ></template
            ></el-alert
          >
          <el-table
            v-else
            v-loading="consumptionLoading"
            :data="consumptionResult.items"
            min-height="320"
            stripe
          >
            <el-table-column label="消耗 ID" min-width="90" prop="consumptionId" />
            <el-table-column label="产品批次 / 订单" min-width="170"
              ><template #default="{ row }"
                ><strong>{{ row.productBatchNo || '-' }}</strong
                ><small class="cell-sub">订单 #{{ row.orderId }}</small></template
              ></el-table-column
            >
            <el-table-column label="原材料批次" min-width="185"
              ><template #default="{ row }"
                ><strong>{{ row.materialBatchNo || '-' }}</strong
                ><small class="cell-sub"
                  >{{ row.materialName || '-' }} · {{ row.purchaseOrderNo || '-' }}</small
                ></template
              ></el-table-column
            >
            <el-table-column label="供应商" min-width="145"
              ><template #default="{ row }">{{
                row.supplierName || '-'
              }}</template></el-table-column
            >
            <el-table-column label="消耗数量" min-width="120"
              ><template #default="{ row }"
                >{{ formatNumber(row.consumeQty) }} {{ row.unit || '' }}</template
              ></el-table-column
            >
            <el-table-column label="消耗时间" min-width="170"
              ><template #default="{ row }">{{
                formatDateTime(row.consumedAt)
              }}</template></el-table-column
            >
            <el-table-column fixed="right" label="操作" min-width="190"
              ><template #default="{ row }"
                ><el-button link type="primary" :icon="View" @click="openConsumptionDetail(row)"
                  >详情</el-button
                ><el-button
                  v-if="canManage"
                  link
                  type="primary"
                  :icon="EditPen"
                  @click="openConsumptionEdit(row)"
                  >修改</el-button
                ><el-button
                  v-if="canManage"
                  link
                  type="danger"
                  :icon="Delete"
                  :disabled="consumptionDeleting"
                  @click="removeConsumption(row)"
                  >删除</el-button
                ></template
              ></el-table-column
            >
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

      <el-tab-pane label="正向追溯" name="product">
        <el-card class="trace-search-card" shadow="never"
          ><el-form :model="productFilters" inline @submit.prevent="traceProduct"
            ><el-form-item label="生产订单 ID"
              ><el-input
                v-model.trim="productFilters.orderId"
                clearable
                placeholder="与批次号二选一" /></el-form-item
            ><el-form-item label="成品批次号"
              ><el-input
                v-model.trim="productFilters.batchNo"
                clearable
                placeholder="与订单二选一" /></el-form-item
            ><el-form-item label="含供应商"
              ><el-switch v-model="productFilters.includeSupplier" /></el-form-item
            ><el-form-item
              ><el-button
                :icon="Search"
                :loading="productLoading"
                type="primary"
                @click="traceProduct"
                >追溯</el-button
              ><el-button :disabled="productLoading" :icon="Refresh" @click="resetProductFilters"
                >重置</el-button
              ></el-form-item
            ></el-form
          ></el-card
        >
        <el-card v-loading="productLoading" class="trace-table-card" shadow="never">
          <el-alert
            v-if="productError"
            :closable="false"
            show-icon
            :title="productError"
            type="error"
          />
          <template v-else-if="productResult">
            <el-descriptions border :column="3" class="trace-summary" title="成品批次"
              ><el-descriptions-item label="生产订单"
                >#{{ productResult.orderId }}</el-descriptions-item
              ><el-descriptions-item label="成品批次">{{
                productResult.batchNo || '-'
              }}</el-descriptions-item
              ><el-descriptions-item label="BOM 版本">{{
                productResult.bomVersion || '-'
              }}</el-descriptions-item
              ><el-descriptions-item label="计划 / 完工"
                >{{ formatNumber(productResult.planQty) }} /
                {{ formatNumber(productResult.finishedQty) }}</el-descriptions-item
              ><el-descriptions-item label="合格 / 不合格"
                >{{ formatNumber(productResult.qualifiedQty) }} /
                {{ formatNumber(productResult.defectiveQty) }}</el-descriptions-item
              ><el-descriptions-item label="入库数量">{{
                formatNumber(productResult.inboundQty)
              }}</el-descriptions-item
              ><el-descriptions-item label="生产时间">{{
                formatDateTime(productResult.producedAt)
              }}</el-descriptions-item
              ><el-descriptions-item label="入库时间">{{
                formatDateTime(productResult.inboundAt)
              }}</el-descriptions-item></el-descriptions
            >
            <el-table :data="productResult.consumedBatches" stripe
              ><el-table-column label="原材料批次" min-width="180"
                ><template #default="{ row }"
                  ><strong>{{ row.materialBatchNo || '-' }}</strong
                  ><small class="cell-sub">{{
                    row.materialName || `#${row.materialId}`
                  }}</small></template
                ></el-table-column
              ><el-table-column label="采购订单" min-width="150"
                ><template #default="{ row }">{{
                  row.purchaseOrderNo || '-'
                }}</template></el-table-column
              ><el-table-column label="供应商" min-width="150"
                ><template #default="{ row }">{{
                  row.supplierName || '-'
                }}</template></el-table-column
              ><el-table-column label="到货日期" min-width="125"
                ><template #default="{ row }">{{
                  formatDateTime(row.receiveDate)
                }}</template></el-table-column
              ><el-table-column label="消耗数量" min-width="115"
                ><template #default="{ row }"
                  >{{ formatNumber(row.consumeQty) }} {{ row.unit || '' }}</template
                ></el-table-column
              ></el-table
            >
            <el-empty
              v-if="!productResult.consumedBatches.length"
              description="该成品批次暂无原材料消耗记录"
            />
          </template>
          <el-empty v-else-if="productSearched" description="未查询到匹配的成品批次" /><el-empty
            v-else
            description="请输入生产订单 ID 或成品批次号后开始正向追溯"
          />
        </el-card>
      </el-tab-pane>

      <el-tab-pane label="反向追溯" name="material">
        <el-card class="trace-search-card" shadow="never"
          ><el-form :model="materialFilters" inline @submit.prevent="traceMaterial"
            ><el-form-item label="采购明细 ID"
              ><el-input
                v-model.trim="materialFilters.itemId"
                clearable
                placeholder="可选" /></el-form-item
            ><el-form-item label="原材料 ID"
              ><el-input
                v-model.trim="materialFilters.materialId"
                clearable
                placeholder="可选" /></el-form-item
            ><el-form-item label="供应商筛选"
              ><el-select
                v-model="materialFilters.supplierId"
                clearable
                filterable
                :loading="referenceLoading"
                placeholder="选择供应商"
                ><el-option
                  v-for="supplier in suppliers"
                  :key="supplier.supplierId"
                  :label="`${supplier.supplierName} · #${supplier.supplierId}`"
                  :value="supplier.supplierId" /></el-select></el-form-item
            ><el-form-item label="到货日期"
              ><el-date-picker
                v-model="materialFilters.receiveRange"
                end-placeholder="结束日期"
                range-separator="至"
                start-placeholder="开始日期"
                type="daterange"
                value-format="YYYY-MM-DD" /></el-form-item
            ><el-form-item
              ><el-button
                :icon="Search"
                :loading="materialLoading"
                type="primary"
                @click="traceMaterial"
                >追溯</el-button
              ><el-button :disabled="materialLoading" :icon="Refresh" @click="resetMaterialFilters"
                >重置</el-button
              ></el-form-item
            ></el-form
          ></el-card
        >
        <el-card v-loading="materialLoading" class="trace-table-card" shadow="never"
          ><el-alert
            v-if="materialError"
            :closable="false"
            show-icon
            :title="materialError"
            type="error" /><template v-else-if="materialResult.length"
            ><el-card
              v-for="batch in materialResult"
              :key="batch.itemId"
              class="material-batch-card"
              shadow="never"
              ><template #header
                ><div class="material-batch-header">
                  <strong>{{ batch.materialName || `原材料 #${batch.materialId}` }}</strong
                  ><span>采购明细 #{{ batch.itemId }} · {{ batch.supplierName || '-' }}</span>
                </div></template
              ><el-table :data="batch.affectedProducts" stripe
                ><el-table-column label="生产订单" min-width="110"
                  ><template #default="{ row }">#{{ row.orderId }}</template></el-table-column
                ><el-table-column label="产品批次" min-width="155"
                  ><template #default="{ row }">{{ row.batchNo || '-' }}</template></el-table-column
                ><el-table-column label="产品 / 完工数量" min-width="180"
                  ><template #default="{ row }"
                    >{{ row.productMaterialName || '-'
                    }}<small class="cell-sub"
                      >完工 {{ formatNumber(row.finishedQty) }}</small
                    ></template
                  ></el-table-column
                ><el-table-column label="消耗数量" min-width="110"
                  ><template #default="{ row }">{{
                    formatNumber(row.consumeQty)
                  }}</template></el-table-column
                ><el-table-column label="处置状态" min-width="110"
                  ><template #default="{ row }"
                    ><el-tag
                      v-if="row.qualityStatus"
                      :type="getQualityStatusTone(row.qualityStatus)"
                      >{{ getQualityStatusLabel(row.qualityStatus) }}</el-tag
                    ></template
                  ></el-table-column
                ></el-table
              ></el-card
            ></template
          ><el-empty v-else-if="materialSearched" description="未查询到匹配的原材料批次" /><el-empty
            v-else
            description="请输入查询条件后开始反向追溯"
        /></el-card>
      </el-tab-pane>

      <el-tab-pane label="质量影响分析" name="impact">
        <el-card class="trace-search-card" shadow="never"
          ><el-form :model="impactFilters" inline @submit.prevent="analyzeImpact"
            ><el-form-item label="问题采购明细"
              ><el-input
                v-model.trim="impactFilters.itemIds"
                clearable
                placeholder="多个 ID 用逗号分隔" /></el-form-item
            ><el-form-item label="原材料 ID"
              ><el-input
                v-model.trim="impactFilters.materialId"
                clearable
                placeholder="可选" /></el-form-item
            ><el-form-item label="到货日期"
              ><el-date-picker
                v-model="impactFilters.receiveRange"
                end-placeholder="结束日期"
                range-separator="至"
                start-placeholder="开始日期"
                type="daterange"
                value-format="YYYY-MM-DD" /></el-form-item
            ><el-form-item
              ><el-button
                :icon="Search"
                :loading="impactLoading"
                type="primary"
                @click="analyzeImpact"
                >分析</el-button
              ><el-button :disabled="impactLoading" :icon="Refresh" @click="resetImpactFilters"
                >重置</el-button
              ></el-form-item
            ></el-form
          ></el-card
        >
        <el-card v-loading="impactLoading" class="trace-table-card" shadow="never"
          ><el-alert
            v-if="impactError"
            :closable="false"
            show-icon
            :title="impactError"
            type="error" /><template v-else-if="impactResult"
            ><el-alert
              class="mock-notice"
              :closable="false"
              show-icon
              title="当前为前端 Mock 处置记录，不代表已同步修改库存状态。"
              type="warning"
            />
            <div class="impact-summary">
              <div
                v-for="metric in [
                  { label: '受影响订单', value: impactResult.summary?.affectedOrderCount },
                  { label: '产品批次', value: impactResult.summary?.affectedBatchCount },
                  { label: '受影响产品', value: impactResult.summary?.affectedProductCount },
                  { label: '合格品', value: impactResult.summary?.qualifiedQty },
                  { label: '不合格品', value: impactResult.summary?.defectiveQty },
                  { label: '已入库', value: impactResult.summary?.inboundQty },
                  { label: '待处理', value: impactResult.summary?.pendingBatchCount },
                  { label: '已冻结', value: impactResult.summary?.frozenBatchCount },
                  { label: '已召回', value: impactResult.summary?.recalledBatchCount },
                ]"
                :key="metric.label"
                class="impact-metric"
              >
                <span>{{ metric.label }}</span
                ><strong>{{ formatNumber(metric.value) }}</strong>
              </div>
            </div>
            <el-table :data="impactResult.affectedProducts" stripe
              ><el-table-column label="生产订单" min-width="105"
                ><template #default="{ row }">#{{ row.orderId }}</template></el-table-column
              ><el-table-column label="产品批次" min-width="145"
                ><template #default="{ row }">{{ row.batchNo || '-' }}</template></el-table-column
              ><el-table-column label="产品 / 数量" min-width="180"
                ><template #default="{ row }"
                  >{{ row.productMaterialName || '-'
                  }}<small class="cell-sub"
                    >计划 {{ formatNumber(row.planQty) }} · 完工
                    {{ formatNumber(row.finishedQty) }}</small
                  ></template
                ></el-table-column
              ><el-table-column label="合格 / 不合格" min-width="145"
                ><template #default="{ row }"
                  >{{ formatNumber(row.qualifiedQty) }} /
                  {{ formatNumber(row.defectiveQty) }}</template
                ></el-table-column
              ><el-table-column label="状态" min-width="105"
                ><template #default="{ row }"
                  ><el-tag
                    v-if="row.qualityStatus"
                    :type="getQualityStatusTone(row.qualityStatus)"
                    >{{ getQualityStatusLabel(row.qualityStatus) }}</el-tag
                  ></template
                ></el-table-column
              ><el-table-column v-if="canManage" fixed="right" label="处置" min-width="160"
                ><template #default="{ row }"
                  ><el-button
                    v-if="row.qualityStatus === 'pending'"
                    link
                    type="warning"
                    @click="openDisposition('freeze', row)"
                    >冻结</el-button
                  ><el-button
                    v-if="row.qualityStatus !== 'recalled'"
                    link
                    type="danger"
                    @click="openDisposition('recall', row)"
                    >召回</el-button
                  ></template
                ></el-table-column
              ></el-table
            ><el-divider content-position="left">质量处置记录</el-divider
            ><el-table :data="impactDispositions" size="small" stripe
              ><el-table-column label="类型" min-width="90"
                ><template #default="{ row }"
                  ><el-tag :type="row.type === 'freeze' ? 'warning' : 'danger'">{{
                    row.type === 'freeze' ? '冻结' : '召回'
                  }}</el-tag></template
                ></el-table-column
              ><el-table-column label="产品批次" min-width="140" prop="batchNo" /><el-table-column
                label="原因"
                min-width="220"
                prop="reason"
              /><el-table-column
                label="操作人"
                min-width="100"
                prop="operatorName"
              /><el-table-column label="受影响数量" min-width="110"
                ><template #default="{ row }">{{
                  formatNumber(row.affectedQty)
                }}</template></el-table-column
              ><el-table-column
                label="召回范围"
                min-width="180"
                prop="recallScope"
              /><el-table-column label="操作时间" min-width="170"
                ><template #default="{ row }">{{
                  formatDateTime(row.operatedAt)
                }}</template></el-table-column
              ></el-table
            ></template
          ><el-empty v-else-if="impactSearched" description="未查询到质量影响分析结果" /><el-empty
            v-else
            description="请输入问题批次条件后开始分析"
        /></el-card>
      </el-tab-pane>
    </el-tabs>

    <el-dialog
      v-model="consumptionDialogVisible"
      :close-on-click-modal="false"
      :title="consumptionDialogTitle"
      width="650px"
      ><el-form
        ref="consumptionFormRef"
        label-width="120px"
        :model="consumptionForm"
        :rules="consumptionRules"
        ><el-form-item label="产品批次 / 订单" prop="productBatchNo"
          ><el-select
            v-model="consumptionForm.productBatchNo"
            filterable
            :loading="referenceLoading"
            placeholder="选择产品批次"
            style="width: 100%"
            @change="onProductBatchChange"
            ><el-option
              v-for="batch in references.productBatches"
              :key="batch.batchNo"
              :label="`${batch.batchNo} · ${batch.materialName} · 订单 #${batch.orderId}`"
              :value="batch.batchNo" /></el-select></el-form-item
        ><el-form-item label="采购明细 / 原材料" prop="itemId"
          ><el-select
            v-model="consumptionForm.itemId"
            filterable
            :loading="referenceLoading"
            placeholder="选择原材料批次"
            style="width: 100%"
            @change="onPurchaseItemChange"
            ><el-option
              v-for="item in references.purchaseItems"
              :key="item.itemId"
              :label="`${item.materialBatchNo} · ${item.materialName} · ${item.purchaseOrderNo}`"
              :value="item.itemId" /></el-select
        ></el-form-item>
        <div class="form-grid">
          <el-form-item label="供应商" prop="supplierId"
            ><el-select v-model="consumptionForm.supplierId" disabled style="width: 100%"
              ><el-option
                v-for="supplier in suppliers"
                :key="supplier.supplierId"
                :label="supplier.supplierName"
                :value="supplier.supplierId" /></el-select></el-form-item
          ><el-form-item label="单位"
            ><el-input v-model="consumptionForm.unit" disabled /></el-form-item
          ><el-form-item label="原材料批次"
            ><el-input v-model="consumptionForm.materialBatchNo" disabled /></el-form-item
          ><el-form-item label="采购订单"
            ><el-input v-model="consumptionForm.purchaseOrderNo" disabled /></el-form-item
          ><el-form-item label="消耗数量" prop="consumeQty"
            ><el-input-number
              v-model="consumptionForm.consumeQty"
              :min="0.01"
              :precision="2"
              style="width: 100%" /></el-form-item
          ><el-form-item label="消耗时间" prop="consumedAt"
            ><el-date-picker
              v-model="consumptionForm.consumedAt"
              type="datetime"
              value-format="YYYY-MM-DD HH:mm:ss"
              style="width: 100%" /></el-form-item
          ><el-form-item label="操作人" prop="operatorName"
            ><el-input v-model.trim="consumptionForm.operatorName"
          /></el-form-item>
        </div>
        <el-form-item label="备注"
          ><el-input
            v-model.trim="consumptionForm.remarks"
            :rows="2"
            type="textarea" /></el-form-item></el-form
      ><template #footer
        ><el-button :disabled="consumptionSubmitting" @click="consumptionDialogVisible = false"
          >取消</el-button
        ><el-button :loading="consumptionSubmitting" type="primary" @click="submitConsumptionForm"
          >确定</el-button
        ></template
      ></el-dialog
    >

    <el-drawer v-model="consumptionDetailVisible" size="460px" title="批次消耗详情"
      ><div v-loading="consumptionDetailLoading">
        <el-descriptions v-if="consumptionDetail" border :column="1"
          ><el-descriptions-item label="消耗记录"
            >#{{ consumptionDetail.consumptionId }}</el-descriptions-item
          ><el-descriptions-item label="生产订单 / 产品批次"
            >#{{ consumptionDetail.orderId }} ·
            {{ consumptionDetail.productBatchNo || '-' }}</el-descriptions-item
          ><el-descriptions-item label="原材料 / 批次"
            >{{ consumptionDetail.materialName || '-' }} ·
            {{ consumptionDetail.materialBatchNo || '-' }}</el-descriptions-item
          ><el-descriptions-item label="采购订单 / 供应商"
            >{{ consumptionDetail.purchaseOrderNo || '-' }} ·
            {{ consumptionDetail.supplierName || '-' }}</el-descriptions-item
          ><el-descriptions-item label="实际消耗数量"
            >{{ formatNumber(consumptionDetail.consumeQty) }}
            {{ consumptionDetail.unit || '' }}</el-descriptions-item
          ><el-descriptions-item label="消耗时间">{{
            formatDateTime(consumptionDetail.consumedAt)
          }}</el-descriptions-item
          ><el-descriptions-item label="操作人">{{
            consumptionDetail.operatorName || '-'
          }}</el-descriptions-item
          ><el-descriptions-item label="备注">{{
            consumptionDetail.remarks || '-'
          }}</el-descriptions-item></el-descriptions
        >
      </div></el-drawer
    >

    <el-dialog
      v-model="dispositionDialogVisible"
      :close-on-click-modal="false"
      :title="dispositionTitle"
      width="520px"
      ><el-alert
        :closable="false"
        show-icon
        :title="`目标批次：${dispositionTarget?.batchNo || '-'}`"
        type="warning"
      /><el-descriptions border :column="1" class="disposition-target"
        ><el-descriptions-item label="产品 / 生产订单"
          >{{ dispositionTarget?.productMaterialName || '-' }} · #{{
            dispositionTarget?.orderId || '-'
          }}</el-descriptions-item
        ><el-descriptions-item label="受影响数量">{{
          formatNumber(dispositionTarget?.finishedQty)
        }}</el-descriptions-item
        ><el-descriptions-item label="操作时间"
          >提交时自动记录</el-descriptions-item
        ></el-descriptions
      ><el-form
        ref="dispositionFormRef"
        class="disposition-form"
        label-width="90px"
        :model="dispositionForm"
        :rules="dispositionRules"
        ><el-form-item label="处置原因" prop="reason"
          ><el-input
            v-model.trim="dispositionForm.reason"
            :rows="3"
            type="textarea" /></el-form-item
        ><template v-if="dispositionType === 'recall'"
          ><el-form-item label="召回范围" prop="recallScope"
            ><el-input
              v-model.trim="dispositionForm.recallScope"
              placeholder="例如：已入库成品及渠道库存"
          /></el-form-item>
          <el-form-item label="受影响数量"
            ><el-input-number
              v-model="dispositionForm.affectedQty"
              :min="0.01"
              disabled
              style="width: 100%"
          /></el-form-item>
          <el-form-item label="处理说明"
            ><el-input
              v-model.trim="dispositionForm.handlingInstruction"
              :rows="2"
              type="textarea" /></el-form-item
        ></template>
        ><el-form-item label="备注"
          ><el-input v-model.trim="dispositionForm.note" :rows="2" type="textarea" /></el-form-item
        ><el-form-item label="操作人" prop="operatorName"
          ><el-input v-model.trim="dispositionForm.operatorName" /></el-form-item></el-form
      ><template #footer
        ><el-button :disabled="dispositionSubmitting" @click="dispositionDialogVisible = false"
          >取消</el-button
        ><el-button :loading="dispositionSubmitting" type="primary" @click="submitDisposition"
          >确认提交</el-button
        ></template
      ></el-dialog
    >
  </PageContainer>
</template>

<style scoped>
.trace-tabs,
.trace-search-card,
.trace-table-card {
  min-width: 0;
}
.trace-search-card,
.material-batch-card {
  margin-bottom: 16px;
}
.trace-request-error,
.mock-notice {
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
.impact-summary {
  display: grid;
  gap: 12px;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  margin-bottom: 16px;
}
.impact-metric {
  background: var(--el-fill-color-lighter);
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 6px;
  display: flex;
  flex-direction: column;
  gap: 6px;
  padding: 12px;
}
.impact-metric span {
  color: var(--el-text-color-secondary);
  font-size: 13px;
}
.impact-metric strong {
  font-size: 22px;
}
.form-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
}
.disposition-form {
  margin-top: 16px;
}
.disposition-target {
  margin-top: 16px;
}
@media (max-width: 768px) {
  .impact-summary,
  .form-grid {
    grid-template-columns: 1fr;
  }
  .material-batch-header {
    align-items: flex-start;
    flex-direction: column;
    gap: 4px;
  }
}
</style>
