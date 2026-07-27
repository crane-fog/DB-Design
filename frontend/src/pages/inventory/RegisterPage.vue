<script setup lang="ts">
import type {
  CompletionInboundFormData,
  CompletionInboundItem,
  CompletionInboundQuery,
} from '@/types/inventory'
import { ElMessage, type FormInstance, type FormRules } from 'element-plus'
import { Plus, Refresh, Search } from '@element-plus/icons-vue'
import { computed, onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import { formatDateTime, formatNumber } from '@/utils/format'
import EmptyState from '@/components/common/EmptyState.vue'
import PageContainer from '@/components/common/PageContainer.vue'
import PageHeader from '@/components/common/PageHeader.vue'
import { getErrorMessage } from '@/utils/error'
import { inventoryService } from '@/services/InventoryService'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const loading = ref(false)
const submitting = ref(false)
const error = ref('')
const items = ref<CompletionInboundItem[]>([])
const total = ref(0)
const dateRange = ref<[string, string]>()
const query = reactive<CompletionInboundQuery>({ page: 1, pageSize: 10 })
const dialogOpen = ref(false)
const formRef = ref<FormInstance>()
const form = reactive<CompletionInboundFormData>({
  batchNo: '',
  finishQty: 1,
  materialId: 0,
  operatorId: 0,
  orderId: 0,
  qualifiedQty: 1,
  versionId: 0,
})
let alive = true
let requestId = 0

const qualifiedRate = computed(() => calculateQualifiedRate(form.finishQty, form.qualifiedQty))

function calculateQualifiedRate(finishQty: number, qualifiedQty: number) {
  if (finishQty <= 0) {
    return 0
  }
  return (qualifiedQty / finishQty) * 100
}

const formRules: FormRules<CompletionInboundFormData> = {
  batchNo: [
    { message: '请输入生产批次号', required: true, trigger: 'blur' },
    { max: 80, message: '批次号不能超过 80 个字符', trigger: 'blur' },
  ],
  finishQty: [
    { message: '请输入完工数量', required: true, trigger: 'blur', type: 'number' },
    { message: '完工数量必须大于 0', min: 0.01, trigger: 'blur', type: 'number' },
  ],
  materialId: [
    { message: '请输入成品物料 ID', required: true, trigger: 'blur', type: 'number' },
    { message: '物料 ID 必须大于 0', min: 1, trigger: 'blur', type: 'number' },
  ],
  orderId: [
    { message: '请输入生产订单 ID', required: true, trigger: 'blur', type: 'number' },
    { message: '生产订单 ID 必须大于 0', min: 1, trigger: 'blur', type: 'number' },
  ],
  qualifiedQty: [
    {
      trigger: 'change',
      validator: (_rule, value, callback) => {
        if (typeof value !== 'number' || value < 0) {
          callback(new Error('合格数量不能小于 0'))
        } else if (value > form.finishQty) {
          callback(new Error('合格数量不能大于完工数量'))
        } else {
          callback()
        }
      },
    },
  ],
  versionId: [
    { message: '请输入 BOM 版本 ID', required: true, trigger: 'blur', type: 'number' },
    { message: 'BOM 版本 ID 必须大于 0', min: 1, trigger: 'blur', type: 'number' },
  ],
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

function openDialog() {
  Object.assign(form, {
    batchNo: '',
    finishQty: 1,
    materialId: 0,
    operatorId: auth.currentUser?.id ?? 0,
    orderId: 0,
    qualifiedQty: 1,
    versionId: 0,
  })
  dialogOpen.value = true
}

async function submitInbound() {
  const operatorId = auth.currentUser?.id
  if (!operatorId) {
    ElMessage.error('当前会话缺少操作人信息，请重新登录')
    return
  }
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid || submitting.value) {
    return
  }
  submitting.value = true
  try {
    const created = await inventoryService.addCompletionInbound({
      ...form,
      batchNo: form.batchNo.trim(),
      operatorId,
    })
    const consumedCount = created?.consumedLockRecords?.length ?? 0
    let message = '完工入库登记成功'
    if (consumedCount > 0) {
      message = `完工入库登记成功，已消耗 ${consumedCount} 条库存锁定记录`
    }
    ElMessage.success(message)
    dialogOpen.value = false
    query.page = 1
    await loadItems()
  } catch (requestError) {
    ElMessage.error(getErrorMessage(requestError, '完工入库登记失败'))
  } finally {
    submitting.value = false
  }
}

onMounted(() => void loadItems())
onBeforeUnmount(() => {
  alive = false
  requestId += 1
})
</script>

<template>
  <PageContainer>
    <PageHeader
      title="完工入库登记"
      description="登记生产完工批次，核对合格数量并追踪历史入库记录。"
    >
      <template #actions>
        <el-button :icon="Refresh" :loading="loading" @click="loadItems">刷新</el-button>
        <el-button :icon="Plus" type="primary" @click="openDialog">登记入库</el-button>
      </template>
    </PageHeader>

    <el-card class="query-card" shadow="never">
      <div class="query-bar">
        <el-input-number v-model="query.orderId" :min="1" placeholder="生产订单 ID" />
        <el-input-number v-model="query.materialId" :min="1" placeholder="成品物料 ID" />
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

    <el-card class="records-card" shadow="never">
      <template #header><span>入库记录</span></template>
      <div v-loading="loading" class="records-area">
        <EmptyState
          v-if="!loading && !error && !items.length"
          description="当前查询条件下没有完工入库记录。"
        />
        <el-table v-else :data="items" stripe>
          <el-table-column label="入库 ID" min-width="90" prop="inboundId" />
          <el-table-column label="批次号" min-width="170"
            ><template #default="{ row }"
              ><strong>{{ row.batchNo }}</strong></template
            ></el-table-column
          >
          <el-table-column label="生产订单" min-width="110"
            ><template #default="{ row }">#{{ row.orderId }}</template></el-table-column
          >
          <el-table-column label="成品物料" min-width="190"
            ><template #default="{ row }"
              ><div class="product-cell">
                <strong>{{ row.productName || `物料 #${row.materialId}` }}</strong
                ><small>ID {{ row.materialId }} · BOM #{{ row.versionId }}</small>
              </div></template
            ></el-table-column
          >
          <el-table-column label="完工 / 合格" min-width="140"
            ><template #default="{ row }"
              >{{ formatNumber(row.finishQty) }} /
              <strong class="qualified">{{ formatNumber(row.qualifiedQty) }}</strong></template
            ></el-table-column
          >
          <el-table-column label="合格率" min-width="100"
            ><template #default="{ row }">{{
              row.finishQty ? `${formatNumber((row.qualifiedQty / row.finishQty) * 100)}%` : '-'
            }}</template></el-table-column
          >
          <el-table-column label="消耗锁定" min-width="100"
            ><template #default="{ row }">{{
              row.consumedLockRecords?.length ?? 0
            }}</template></el-table-column
          >
          <el-table-column label="入库时间" min-width="175"
            ><template #default="{ row }">{{
              formatDateTime(row.inboundTime)
            }}</template></el-table-column
          >
          <el-table-column label="操作人" min-width="90"
            ><template #default="{ row }">#{{ row.operatorId }}</template></el-table-column
          >
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

    <el-dialog
      v-model="dialogOpen"
      title="登记完工入库"
      width="min(94vw, 620px)"
      @closed="formRef?.resetFields()"
    >
      <el-form ref="formRef" :model="form" :rules="formRules" label-width="115px">
        <div class="form-grid">
          <el-form-item label="生产订单 ID" prop="orderId"
            ><el-input-number v-model="form.orderId" :min="1"
          /></el-form-item>
          <el-form-item label="成品物料 ID" prop="materialId"
            ><el-input-number v-model="form.materialId" :min="1"
          /></el-form-item>
          <el-form-item label="BOM 版本 ID" prop="versionId"
            ><el-input-number v-model="form.versionId" :min="1"
          /></el-form-item>
          <el-form-item label="生产批次号" prop="batchNo"
            ><el-input v-model.trim="form.batchNo" maxlength="80" placeholder="如 AX100-20260727-A"
          /></el-form-item>
          <el-form-item label="完工数量" prop="finishQty"
            ><el-input-number v-model="form.finishQty" :min="0.01" :precision="2"
          /></el-form-item>
          <el-form-item label="合格数量" prop="qualifiedQty"
            ><el-input-number
              v-model="form.qualifiedQty"
              :max="form.finishQty"
              :min="0"
              :precision="2"
          /></el-form-item>
        </div>
        <el-alert
          :closable="false"
          :title="`本批次合格率 ${formatNumber(qualifiedRate)}%`"
          type="info"
        />
      </el-form>
      <template #footer
        ><el-button @click="dialogOpen = false">取消</el-button
        ><el-button :loading="submitting" type="primary" @click="submitInbound"
          >确认登记</el-button
        ></template
      >
    </el-dialog>
  </PageContainer>
</template>

<style scoped>
.query-card,
.request-error {
  margin-bottom: 16px;
}
.query-card {
  border-top: 3px solid var(--primary-color);
}
.records-card {
  border-top: 3px solid var(--border-color);
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
.form-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  column-gap: 12px;
}
.form-grid :deep(.el-input-number),
.form-grid :deep(.el-input) {
  width: 100%;
}
@media (max-width: 680px) {
  .query-bar > * {
    flex: 1 1 160px;
  }
  .form-grid {
    grid-template-columns: 1fr;
  }
}
</style>
