<script setup lang="ts">
import { ElMessage, type FormInstance, type FormRules } from 'element-plus'
import {
  type FaultRecordItem,
  type FaultReportFormData,
  type FaultUpdateFormData,
  type ProductionLineItem,
  type ProductionLineRunStatus,
  productionService,
} from '@/services/ProductionService'
import { Plus, Refresh } from '@element-plus/icons-vue'
import { computed, onMounted, reactive, ref } from 'vue'
import PageContainer from '@/components/common/PageContainer.vue'
import PageHeader from '@/components/common/PageHeader.vue'
import type { PageResult } from '@/services/pagination'
import StatusTag from '@/components/common/StatusTag.vue'
import { formatDateTime } from '@/utils/format'
import { getErrorMessage } from '@/utils/error'
import { toBusinessDateTimeInput } from '@/utils/time'
import { useAuthStore } from '@/stores/auth'

const pageSize = 10
const faultStatusLabels = {
  pending_repair: '待维修',
  recovered: '已恢复',
  repairing: '维修中',
}
const lineStatusLabels = { fault: '故障', idle: '空闲', running: '运行中' }
const auth = useAuthStore()
const canManage = computed(() => auth.hasPermission('production:breakdown'))
const canViewLines = computed(() => auth.hasPermission('production:capacity'))
const selectedLineStatus = ref<ProductionLineRunStatus | ''>('fault')
const page = ref(1)
const loading = ref(false)
const error = ref('')
const result = ref<PageResult<ProductionLineItem>>({ items: [], page: 1, pageSize, total: 0 })
const reportVisible = ref(false)
const reportFormRef = ref<FormInstance>()
const reporting = ref(false)
const reportForm = reactive<FaultReportFormData>({ description: '', faultType: '', lineId: 0 })
const updateVisible = ref(false)
const updateFormRef = ref<FormInstance>()
const updating = ref(false)
const updateForm = reactive<FaultUpdateFormData>({
  faultId: 0,
  recoverTime: '',
  repairerId: undefined,
  status: 'repairing',
})
const lastRecord = ref<FaultRecordItem>()

const reportRules: FormRules<FaultReportFormData> = {
  description: [{ message: '请输入故障描述', required: true, trigger: 'blur' }],
  faultType: [{ message: '请输入故障类型', required: true, trigger: 'blur' }],
  lineId: [
    { message: '请输入有效生产线编号', min: 1, required: true, trigger: 'blur', type: 'number' },
  ],
}
const updateRules: FormRules<FaultUpdateFormData> = {
  faultId: [
    { message: '请输入有效故障编号', min: 1, required: true, trigger: 'blur', type: 'number' },
  ],
  repairerId: [{ message: '维修负责人编号必须大于 0', min: 1, trigger: 'blur', type: 'number' }],
  status: [{ message: '请选择处理状态', required: true, trigger: 'change' }],
}

async function loadLines(targetPage = page.value) {
  if (!canViewLines.value) {
    return
  }
  loading.value = true
  error.value = ''
  try {
    result.value = await productionService.listLines({
      page: targetPage,
      pageSize,
      status: selectedLineStatus.value || undefined,
    })
    page.value = result.value.page
  } catch (requestError) {
    error.value = getErrorMessage(requestError, '生产线状态加载失败')
  } finally {
    loading.value = false
  }
}

function openReport(lineId = 0) {
  Object.assign(reportForm, { description: '', faultType: '', lineId })
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
    lastRecord.value = await productionService.reportFault({ ...reportForm })
    ElMessage.success(`故障已上报，编号 #${lastRecord.value.faultId}`)
    reportVisible.value = false
    await loadLines(1)
  } catch (requestError) {
    ElMessage.error(getErrorMessage(requestError, '故障上报失败'))
  } finally {
    reporting.value = false
  }
}

function openUpdate(record?: FaultRecordItem) {
  let status = 'repairing'
  if (record?.status === 'recovered') {
    status = 'recovered'
  }
  Object.assign(updateForm, {
    faultId: record?.faultId ?? 0,
    recoverTime: toBusinessDateTimeInput(record?.recoverTime),
    repairerId: record?.repairerId ?? auth.currentUser?.id,
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
    let recoverTime: string | undefined = undefined
    if (updateForm.status === 'recovered') {
      ;({ recoverTime } = updateForm)
    }
    lastRecord.value = await productionService.updateFault({
      ...updateForm,
      recoverTime,
    })
    ElMessage.success(`故障 #${lastRecord.value.faultId} 处理状态已更新`)
    updateVisible.value = false
    await loadLines(page.value)
  } catch (requestError) {
    ElMessage.error(getErrorMessage(requestError, '故障处理更新失败'))
  } finally {
    updating.value = false
  }
}

onMounted(() => void loadLines())
</script>

<template>
  <PageContainer>
    <PageHeader
      title="故障反馈"
      description="查看产线运行状态，上报故障，并按故障编号进行维修和恢复处理。"
    >
      <template #actions>
        <el-button v-if="canManage" @click="openUpdate()">按编号处理</el-button>
        <el-button v-if="canManage" :icon="Plus" type="primary" @click="openReport()">
          上报故障
        </el-button>
      </template>
    </PageHeader>

    <el-card v-if="canViewLines" class="section-card table-card" shadow="never">
      <el-form inline @submit.prevent="loadLines(1)">
        <el-form-item label="产线状态">
          <el-select
            v-model="selectedLineStatus"
            clearable
            placeholder="全部状态"
            style="width: 150px"
          >
            <el-option label="故障" value="fault" />
            <el-option label="运行中" value="running" />
            <el-option label="空闲" value="idle" />
          </el-select>
        </el-form-item>
        <el-form-item>
          <el-button :loading="loading" type="primary" @click="loadLines(1)">查询</el-button>
          <el-button :disabled="loading" :icon="Refresh" @click="loadLines(page)">刷新</el-button>
        </el-form-item>
      </el-form>
      <el-alert v-if="error" :closable="false" show-icon :title="error" type="error" />
      <el-table v-else v-loading="loading" :data="result.items" stripe>
        <el-table-column label="生产线编号" prop="lineId" min-width="120" />
        <el-table-column label="线型" min-width="150">
          <template #default="{ row }">{{ row.typeName || `#${row.typeId}` }}</template>
        </el-table-column>
        <el-table-column label="负责人" min-width="150">
          <template #default="{ row }">{{ row.managerName || `#${row.managerId}` }}</template>
        </el-table-column>
        <el-table-column label="运行状态" min-width="120">
          <template #default="{ row }">
            <StatusTag v-if="row.status" :labels="lineStatusLabels" :value="row.status" />
            <span v-else>-</span>
          </template>
        </el-table-column>
        <el-table-column v-if="canManage" label="操作" min-width="130">
          <template #default="{ row }">
            <el-button link type="primary" @click="openReport(row.lineId)">上报故障</el-button>
          </template>
        </el-table-column>
      </el-table>
      <div v-if="!error && result.total > 0" class="pagination">
        <el-pagination
          v-model:current-page="page"
          background
          layout="total, prev, pager, next"
          :page-size="pageSize"
          :total="result.total"
          @current-change="loadLines"
        />
      </div>
    </el-card>

    <el-card class="section-card" shadow="never">
      <template #header>最近一次操作结果</template>
      <p class="result-note">
        显示本页最近一次上报或处理返回的记录。请保存故障编号，后续可按编号处理。
      </p>
      <el-descriptions v-if="lastRecord" border :column="2">
        <el-descriptions-item label="故障编号">#{{ lastRecord.faultId }}</el-descriptions-item>
        <el-descriptions-item label="生产线">#{{ lastRecord.lineId }}</el-descriptions-item>
        <el-descriptions-item label="故障类型">{{ lastRecord.faultType }}</el-descriptions-item>
        <el-descriptions-item label="处理状态">
          <StatusTag :labels="faultStatusLabels" :value="lastRecord.status" />
        </el-descriptions-item>
        <el-descriptions-item label="故障描述" :span="2">{{
          lastRecord.description
        }}</el-descriptions-item>
        <el-descriptions-item label="发生时间">{{
          formatDateTime(lastRecord.occurTime)
        }}</el-descriptions-item>
        <el-descriptions-item label="恢复时间">{{
          formatDateTime(lastRecord.recoverTime)
        }}</el-descriptions-item>
        <el-descriptions-item label="上报人">#{{ lastRecord.reporterId }}</el-descriptions-item>
        <el-descriptions-item label="维修负责人">{{
          lastRecord.repairerId ? `#${lastRecord.repairerId}` : '-'
        }}</el-descriptions-item>
      </el-descriptions>
      <el-empty v-else description="上报或处理后查看返回记录" />
      <el-button
        v-if="canManage && lastRecord"
        class="result-action"
        type="primary"
        @click="openUpdate(lastRecord)"
      >
        处理此故障
      </el-button>
    </el-card>

    <el-dialog
      v-model="reportVisible"
      :close-on-click-modal="false"
      title="上报生产故障"
      width="560px"
    >
      <el-form ref="reportFormRef" :model="reportForm" :rules="reportRules" label-width="110px">
        <el-form-item label="生产线编号" prop="lineId">
          <el-input-number :controls="false" v-model="reportForm.lineId" :min="1" :precision="0" />
        </el-form-item>
        <el-form-item label="故障类型" prop="faultType">
          <el-input v-model.trim="reportForm.faultType" maxlength="50" />
        </el-form-item>
        <el-form-item label="故障描述" prop="description">
          <el-input
            v-model.trim="reportForm.description"
            maxlength="500"
            show-word-limit
            type="textarea"
            :rows="4"
          />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button :disabled="reporting" @click="reportVisible = false">取消</el-button>
        <el-button :loading="reporting" type="primary" @click="submitReport">上报</el-button>
      </template>
    </el-dialog>

    <el-dialog
      v-model="updateVisible"
      :close-on-click-modal="false"
      title="处理生产故障"
      width="560px"
    >
      <el-form ref="updateFormRef" :model="updateForm" :rules="updateRules" label-width="120px">
        <el-form-item label="故障编号" prop="faultId">
          <el-input-number :controls="false" v-model="updateForm.faultId" :min="1" :precision="0" />
        </el-form-item>
        <el-form-item label="处理状态" prop="status">
          <el-select v-model="updateForm.status" style="width: 100%">
            <el-option label="待维修" value="pending_repair" />
            <el-option label="维修中" value="repairing" />
            <el-option label="已恢复" value="recovered" />
          </el-select>
        </el-form-item>
        <el-form-item label="维修负责人 ID" prop="repairerId">
          <el-input-number
            :controls="false"
            v-model="updateForm.repairerId"
            :min="1"
            :precision="0"
          />
        </el-form-item>
        <el-form-item v-if="updateForm.status === 'recovered'" label="恢复时间" prop="recoverTime">
          <el-date-picker
            :show-now="false"
            v-model="updateForm.recoverTime"
            type="datetime"
            value-format="YYYY-MM-DDTHH:mm:ss"
            placeholder="北京时间，留空使用当前时间"
            style="width: 100%"
          />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button :disabled="updating" @click="updateVisible = false">取消</el-button>
        <el-button :loading="updating" type="primary" @click="submitUpdate">保存处理</el-button>
      </template>
    </el-dialog>
  </PageContainer>
</template>

<style scoped>
.section-card {
  margin-bottom: 16px;
}
.pagination {
  display: flex;
  justify-content: flex-end;
  margin-top: 16px;
}
.result-note {
  margin: 0 0 16px;
  color: var(--el-text-color-secondary);
}
.result-action {
  margin-top: 16px;
}
</style>
