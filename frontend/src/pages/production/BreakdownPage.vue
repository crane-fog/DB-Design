<script setup lang="ts">
import { ElMessage, type FormInstance, type FormRules } from 'element-plus'
import {
  type FaultRecordItem,
  type FaultReportFormData,
  type FaultStatusValue,
  type FaultUpdateFormData,
  type ProductionLineItem,
  productionService,
} from '@/services/ProductionService'
import { Plus, Refresh, View } from '@element-plus/icons-vue'
import { computed, onMounted, reactive, ref } from 'vue'
import PageContainer from '@/components/common/PageContainer.vue'
import PageHeader from '@/components/common/PageHeader.vue'
import type { PageResult } from '@/services/pagination'
import StatusTag from '@/components/common/StatusTag.vue'
import { formatDateTime } from '@/utils/format'
import { getErrorMessage } from '@/utils/error'
import { useAuthStore } from '@/stores/auth'

const pageSize = 10
const faultStatusLabels: Record<FaultStatusValue, string> = {
  pending_repair: '待维修',
  recovered: '已恢复',
  repairing: '维修中',
}
const faultLevelLabels: Record<FaultRecordItem['faultLevel'], string> = {
  critical: '严重',
  major: '主要',
  minor: '一般',
}
const auth = useAuthStore()
const canManage = computed(() => auth.hasPermission('production:breakdown'))
const lines = ref<ProductionLineItem[]>([])
const linesLoading = ref(false)
const filters = reactive({
  faultType: '',
  lineId: undefined as number | undefined,
  occurRange: [] as string[],
  status: '',
})
const page = ref(1)
const loading = ref(false)
const error = ref('')
const result = ref<PageResult<FaultRecordItem>>({ items: [], page: 1, pageSize, total: 0 })
const reportVisible = ref(false)
const reportFormRef = ref<FormInstance>()
const reporting = ref(false)
const reportForm = reactive<FaultReportFormData>({
  description: '',
  faultLevel: 'major',
  faultType: '',
  lineId: 0,
  occurTime: '',
})
const updateVisible = ref(false)
const updateFormRef = ref<FormInstance>()
const updating = ref(false)
const updateTarget = ref<FaultRecordItem>()
const updateForm = reactive<FaultUpdateFormData>({
  faultId: 0,
  processingNote: '',
  recoverTime: '',
  repairerId: undefined,
  status: 'repairing',
})
const detailVisible = ref(false)
const detailLoading = ref(false)
const detailError = ref('')
const detail = ref<FaultRecordItem>()

const reportRules: FormRules<FaultReportFormData> = {
  description: [{ message: '请输入故障描述', required: true, trigger: 'blur' }],
  faultLevel: [{ message: '请选择故障等级', required: true, trigger: 'change' }],
  faultType: [{ message: '请输入故障类型', required: true, trigger: 'blur' }],
  lineId: [{ message: '请选择生产线', required: true, trigger: 'change', type: 'number' }],
  occurTime: [{ message: '请选择发生时间', required: true, trigger: 'change' }],
}
const updateRules: FormRules<FaultUpdateFormData> = {
  recoverTime: [
    {
      trigger: 'change',
      validator: (_rule, value, callback) => {
        if (updateForm.status === 'recovered' && !value) {
          callback(new Error('故障恢复时必须填写恢复时间'))
          return
        }
        callback()
      },
    },
  ],
  status: [{ message: '请选择处理状态', required: true, trigger: 'change' }],
}
const availableLines = computed(() => lines.value.filter((line) => line.status !== 'fault'))
const requireRecoverTime = computed(() => updateForm.status === 'recovered')

function getFaultLevelLabel(level: FaultRecordItem['faultLevel']) {
  return faultLevelLabels[level]
}

function selectedStatus(): FaultStatusValue | undefined {
  const { status } = filters
  if (status === 'pending_repair' || status === 'repairing' || status === 'recovered') {
    return status
  }
  return undefined
}

async function loadLines() {
  linesLoading.value = true
  try {
    const lineResult = await productionService.listLines({ page: 1, pageSize: 100 })
    lines.value = lineResult.items
  } catch (requestError) {
    ElMessage.error(getErrorMessage(requestError, '生产线选项加载失败'))
  } finally {
    linesLoading.value = false
  }
}

async function loadFaults(targetPage = page.value) {
  loading.value = true
  error.value = ''
  try {
    result.value = await productionService.listFaults({
      faultType: filters.faultType.trim() || undefined,
      lineId: filters.lineId,
      occurEnd: filters.occurRange[1],
      occurStart: filters.occurRange[0],
      page: targetPage,
      pageSize,
      status: selectedStatus(),
    })
    page.value = result.value.page
  } catch (requestError) {
    error.value = getErrorMessage(requestError, '故障历史加载失败')
  } finally {
    loading.value = false
  }
}

function resetFilters() {
  Object.assign(filters, { faultType: '', lineId: undefined, occurRange: [], status: '' })
  void loadFaults(1)
}

function openReport() {
  Object.assign(reportForm, {
    description: '',
    faultLevel: 'major',
    faultType: '',
    lineId: 0,
    occurTime: new Date().toISOString().slice(0, 19).replace('T', ' '),
  })
  reportFormRef.value?.clearValidate()
  reportVisible.value = true
}

async function submitReport() {
  const valid = await reportFormRef.value?.validate().catch(() => false)
  if (!valid || reporting.value) {
    return
  }
  reporting.value = true
  try {
    await productionService.reportFault({ ...reportForm })
    ElMessage.success('故障已上报并写入历史记录')
    reportVisible.value = false
    await Promise.all([loadFaults(1), loadLines()])
  } catch (requestError) {
    ElMessage.error(getErrorMessage(requestError, '故障上报失败'))
  } finally {
    reporting.value = false
  }
}

function openUpdate(record: FaultRecordItem) {
  updateTarget.value = record
  let { status } = record
  if (status === 'pending_repair') {
    status = 'repairing'
  }
  Object.assign(updateForm, {
    faultId: record.faultId,
    processingNote: record.processingNote ?? '',
    recoverTime: record.recoverTime ?? '',
    repairerId: record.repairerId,
    status,
  })
  updateFormRef.value?.clearValidate()
  updateVisible.value = true
}

async function submitUpdate() {
  const valid = await updateFormRef.value?.validate().catch(() => false)
  if (!valid || updating.value) {
    return
  }
  updating.value = true
  try {
    await productionService.updateFault({ ...updateForm })
    ElMessage.success('故障处理状态已更新')
    updateVisible.value = false
    await Promise.all([loadFaults(page.value), loadLines()])
    if (detail.value?.faultId === updateForm.faultId) {
      await openDetail(detail.value)
    }
  } catch (requestError) {
    ElMessage.error(getErrorMessage(requestError, '故障处理更新失败'))
  } finally {
    updating.value = false
  }
}

async function openDetail(record: FaultRecordItem) {
  detailVisible.value = true
  detailLoading.value = true
  detailError.value = ''
  detail.value = undefined
  try {
    detail.value = await productionService.getFault(record.faultId)
  } catch (requestError) {
    detailError.value = getErrorMessage(requestError, '故障详情加载失败')
  } finally {
    detailLoading.value = false
  }
}

onMounted(() => {
  void Promise.all([loadFaults(), loadLines()])
})
</script>

<template>
  <PageContainer>
    <PageHeader title="故障反馈" description="查询故障历史，并完成上报、维修和恢复处理。">
      <template #actions>
        <el-button v-if="canManage" :icon="Plus" type="primary" @click="openReport"
          >上报故障</el-button
        >
      </template>
    </PageHeader>
    <el-card class="section-card" shadow="never">
      <el-form :model="filters" inline @submit.prevent="loadFaults(1)">
        <el-form-item label="生产线"
          ><el-select
            v-model="filters.lineId"
            clearable
            filterable
            :loading="linesLoading"
            placeholder="全部生产线"
            style="width: 190px"
            ><el-option
              v-for="line in lines"
              :key="line.lineId"
              :label="`生产线 ${line.lineId} · ${line.status === 'fault' ? '故障' : line.status === 'running' ? '运行中' : '空闲'}`"
              :value="line.lineId" /></el-select
        ></el-form-item>
        <el-form-item label="故障类型"
          ><el-input v-model.trim="filters.faultType" clearable placeholder="支持模糊查询"
        /></el-form-item>
        <el-form-item label="处理状态"
          ><el-select v-model="filters.status" clearable placeholder="全部" style="width: 130px"
            ><el-option label="待维修" value="pending_repair" /><el-option
              label="维修中"
              value="repairing" /><el-option label="已恢复" value="recovered" /></el-select
        ></el-form-item>
        <el-form-item
          ><el-button :loading="loading" type="primary" @click="loadFaults(1)">查询</el-button
          ><el-button :disabled="loading" :icon="Refresh" @click="resetFilters"
            >重置</el-button
          ></el-form-item
        >
      </el-form>
    </el-card>
    <el-card class="section-card" shadow="never">
      <el-alert v-if="error" :closable="false" show-icon :title="error" type="error"
        ><template #default
          ><el-button link type="primary" @click="loadFaults(page)">重新加载</el-button></template
        ></el-alert
      >
      <el-table v-else v-loading="loading" :data="result.items" min-height="320" stripe>
        <el-table-column label="故障编号" min-width="100" prop="faultId" />
        <el-table-column label="产线" min-width="130"
          ><template #default="{ row }">{{
            row.lineName || `生产线 ${row.lineId}`
          }}</template></el-table-column
        >
        <el-table-column label="类型 / 等级" min-width="150"
          ><template #default="{ row }"
            >{{ row.faultType
            }}<small class="cell-sub">{{ getFaultLevelLabel(row.faultLevel) }}</small></template
          ></el-table-column
        >
        <el-table-column label="发生时间" min-width="170"
          ><template #default="{ row }">{{
            formatDateTime(row.occurTime)
          }}</template></el-table-column
        >
        <el-table-column label="处理状态" min-width="110"
          ><template #default="{ row }"
            ><StatusTag :labels="faultStatusLabels" :value="row.status" /></template
        ></el-table-column>
        <el-table-column label="维修人员" min-width="110"
          ><template #default="{ row }">{{
            row.repairerName || row.repairerId || '-'
          }}</template></el-table-column
        >
        <el-table-column label="恢复时间" min-width="170"
          ><template #default="{ row }">{{
            formatDateTime(row.recoverTime)
          }}</template></el-table-column
        >
        <el-table-column fixed="right" label="操作" min-width="150"
          ><template #default="{ row }"
            ><el-button :icon="View" link type="primary" @click="openDetail(row)">详情</el-button
            ><el-button
              v-if="canManage && row.status !== 'recovered'"
              link
              type="primary"
              @click="openUpdate(row)"
              >处理</el-button
            ></template
          ></el-table-column
        >
      </el-table>
      <el-empty
        v-if="!loading && !error && !result.items.length"
        description="暂无符合条件的故障历史记录"
      />
      <div v-if="!error && result.total > 0" class="pagination">
        <el-pagination
          v-model:current-page="page"
          background
          layout="total, prev, pager, next"
          :page-size="pageSize"
          :total="result.total"
          @current-change="loadFaults"
        />
      </div>
    </el-card>
    <el-dialog
      v-model="reportVisible"
      :close-on-click-modal="false"
      title="上报生产故障"
      width="560px"
      ><el-form ref="reportFormRef" :model="reportForm" :rules="reportRules" label-width="100px"
        ><el-form-item label="发生产线" prop="lineId"
          ><el-select
            v-model="reportForm.lineId"
            filterable
            :loading="linesLoading"
            placeholder="选择可用生产线"
            style="width: 100%"
            ><el-option
              v-for="line in availableLines"
              :key="line.lineId"
              :label="`生产线 ${line.lineId} · ${line.typeName || '-'} · ${line.status === 'running' ? '运行中' : '空闲'}`"
              :value="line.lineId" /></el-select></el-form-item
        ><el-form-item label="故障类型" prop="faultType"
          ><el-input v-model.trim="reportForm.faultType" /></el-form-item
        ><el-form-item label="故障等级" prop="faultLevel"
          ><el-select v-model="reportForm.faultLevel" style="width: 100%"
            ><el-option label="一般" value="minor" /><el-option
              label="主要"
              value="major" /><el-option label="严重" value="critical" /></el-select></el-form-item
        ><el-form-item label="发生时间" prop="occurTime"
          ><el-date-picker
            v-model="reportForm.occurTime"
            type="datetime"
            value-format="YYYY-MM-DD HH:mm:ss"
            style="width: 100%" /></el-form-item
        ><el-form-item label="故障描述" prop="description"
          ><el-input
            v-model.trim="reportForm.description"
            :rows="4"
            type="textarea" /></el-form-item></el-form
      ><template #footer
        ><el-button :disabled="reporting" @click="reportVisible = false">取消</el-button
        ><el-button :loading="reporting" type="primary" @click="submitReport"
          >提交</el-button
        ></template
      ></el-dialog
    >
    <el-dialog
      v-model="updateVisible"
      :close-on-click-modal="false"
      :title="`处理故障 #${updateTarget?.faultId || ''}`"
      width="520px"
      ><el-form ref="updateFormRef" :model="updateForm" :rules="updateRules" label-width="100px"
        ><el-form-item label="处理状态" prop="status"
          ><el-select v-model="updateForm.status" style="width: 100%"
            ><el-option label="维修中" value="repairing" /><el-option
              label="已恢复"
              value="recovered" /></el-select></el-form-item
        ><el-form-item label="维修人员"
          ><el-input-number
            v-model="updateForm.repairerId"
            :min="1"
            style="width: 100%" /></el-form-item
        ><el-form-item v-if="requireRecoverTime" label="恢复时间" prop="recoverTime"
          ><el-date-picker
            v-model="updateForm.recoverTime"
            type="datetime"
            value-format="YYYY-MM-DD HH:mm:ss"
            style="width: 100%" /></el-form-item
        ><el-form-item label="处理备注"
          ><el-input
            v-model.trim="updateForm.processingNote"
            :rows="3"
            type="textarea" /></el-form-item></el-form
      ><template #footer
        ><el-button :disabled="updating" @click="updateVisible = false">取消</el-button
        ><el-button :loading="updating" type="primary" @click="submitUpdate"
          >保存处理</el-button
        ></template
      ></el-dialog
    >
    <el-drawer v-model="detailVisible" size="460px" title="故障详情"
      ><el-alert
        v-if="detailError"
        :closable="false"
        show-icon
        :title="detailError"
        type="error" /><el-skeleton v-else-if="detailLoading" animated :rows="8" /><el-descriptions
        v-else-if="detail"
        border
        :column="1"
        ><el-descriptions-item label="故障编号">#{{ detail.faultId }}</el-descriptions-item
        ><el-descriptions-item label="关联产线">{{
          detail.lineName || `生产线 ${detail.lineId}`
        }}</el-descriptions-item
        ><el-descriptions-item label="故障类型 / 等级"
          >{{ detail.faultType }} /
          {{ getFaultLevelLabel(detail.faultLevel) }}</el-descriptions-item
        ><el-descriptions-item label="故障描述">{{ detail.description }}</el-descriptions-item
        ><el-descriptions-item label="发生时间">{{
          formatDateTime(detail.occurTime)
        }}</el-descriptions-item
        ><el-descriptions-item label="上报人员">{{
          detail.reporterName || detail.reporterId
        }}</el-descriptions-item
        ><el-descriptions-item label="当前状态"
          ><StatusTag :labels="faultStatusLabels" :value="detail.status" /></el-descriptions-item
        ><el-descriptions-item label="处理人员">{{
          detail.repairerName || detail.repairerId || '-'
        }}</el-descriptions-item
        ><el-descriptions-item label="恢复时间">{{
          formatDateTime(detail.recoverTime)
        }}</el-descriptions-item
        ><el-descriptions-item label="处理备注">{{
          detail.processingNote || '-'
        }}</el-descriptions-item></el-descriptions
      ><el-empty
        v-if="!detail && !detailLoading && !detailError"
        description="故障记录不存在或已被删除"
    /></el-drawer>
  </PageContainer>
</template>

<style scoped>
.section-card {
  margin-bottom: 16px;
  min-width: 0;
}
.pagination {
  display: flex;
  justify-content: flex-end;
  margin-top: 16px;
}
.cell-sub {
  color: var(--el-text-color-secondary);
  display: block;
  font-size: 12px;
  margin-top: 3px;
}
</style>
