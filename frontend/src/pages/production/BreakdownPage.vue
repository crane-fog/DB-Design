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
import { type SystemUser, systemService } from '@/services/SystemService'
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
const selectedLineStatus = ref<ProductionLineRunStatus | ''>('')
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
const faultsVisible = ref(false)
const faultsLoading = ref(false)
const lineFaults = ref<FaultRecordItem[]>([])
const selectedLineId = ref(0)
const users = ref<SystemUser[]>([])
const usersLoading = ref(false)

function getUserName(userId: number | undefined): string {
  if (!userId) {
    return '-'
  }
  const user = users.value.find((targetUser) => targetUser.id === userId)
  if (user) {
    return `${user.name} (${user.employeeNo})`
  }
  return `#${userId}`
}

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
  repairerId: [{ message: '请选择维修负责人', required: true, trigger: 'change', type: 'number' }],
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
    const record = await productionService.reportFault({ ...reportForm })
    ElMessage.success(`故障已上报，编号 #${record.faultId}`)
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
  } else if (record?.status === 'pending_repair') {
    status = 'pending_repair'
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

async function loadLineFaults(lineId: number) {
  selectedLineId.value = lineId
  faultsLoading.value = true
  lineFaults.value = []
  faultsVisible.value = true
  try {
    const faultsResult = await productionService.listFaults({
      lineId,
      page: 1,
      pageSize: 100,
    })
    lineFaults.value = faultsResult.items
  } catch (requestError) {
    ElMessage.error(getErrorMessage(requestError, '查询生产线故障失败'))
    faultsVisible.value = false
  } finally {
    faultsLoading.value = false
  }
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
    const record = await productionService.updateFault({
      ...updateForm,
      recoverTime,
    })
    ElMessage.success(`故障 #${record.faultId} 处理状态已更新`)
    updateVisible.value = false
    await loadLines(page.value)
  } catch (requestError) {
    ElMessage.error(getErrorMessage(requestError, '故障处理更新失败'))
  } finally {
    updating.value = false
  }
}

async function loadUsers() {
  usersLoading.value = true
  try {
    const usersResult = await systemService.listInternalUsers({
      page: 1,
      pageSize: 100,
      status: 'valid',
    })
    users.value = usersResult.items
  } catch (requestError) {
    ElMessage.error(getErrorMessage(requestError, '加载用户列表失败'))
  } finally {
    usersLoading.value = false
  }
}

onMounted(() => {
  void loadLines()
  void loadUsers()
})
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
          <el-button :loading="loading" type="primary" native-type="submit">查询</el-button>
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
        <el-table-column v-if="canManage" label="操作" min-width="180">
          <template #default="{ row }">
            <el-button link type="primary" @click="loadLineFaults(row.lineId)">
              查看故障
            </el-button>
            <el-button link type="primary" @click="openReport(row.lineId)"> 上报故障 </el-button>
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
        <el-form-item label="维修负责人" prop="repairerId">
          <el-select
            v-model="updateForm.repairerId"
            clearable
            filterable
            :loading="usersLoading"
            placeholder="请选择维修负责人"
            style="width: 100%"
          >
            <el-option
              v-for="user in users"
              :key="user.id"
              :label="`${user.name} (${user.employeeNo})`"
              :value="user.id"
            />
          </el-select>
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

    <el-dialog v-model="faultsVisible" title="生产线故障记录" width="900px">
      <p class="result-note">生产线 #{{ selectedLineId }} 的故障记录列表</p>
      <div v-loading="faultsLoading">
        <div v-if="lineFaults.length > 0" class="fault-list">
          <el-card
            v-for="fault in lineFaults"
            :key="fault.faultId"
            class="fault-card"
            shadow="never"
          >
            <el-descriptions border :column="2">
              <el-descriptions-item label="故障编号">#{{ fault.faultId }}</el-descriptions-item>
              <el-descriptions-item label="故障类型">{{ fault.faultType }}</el-descriptions-item>
              <el-descriptions-item label="处理状态">
                <StatusTag :labels="faultStatusLabels" :value="fault.status" />
              </el-descriptions-item>
              <el-descriptions-item label="发生时间">{{
                formatDateTime(fault.occurTime)
              }}</el-descriptions-item>
              <el-descriptions-item label="恢复时间">{{
                formatDateTime(fault.recoverTime)
              }}</el-descriptions-item>
              <el-descriptions-item label="上报人">{{
                getUserName(fault.reporterId)
              }}</el-descriptions-item>
              <el-descriptions-item label="维修负责人">{{
                getUserName(fault.repairerId)
              }}</el-descriptions-item>
              <el-descriptions-item label="故障描述" :span="2">{{
                fault.description
              }}</el-descriptions-item>
            </el-descriptions>
            <el-button
              v-if="canManage"
              class="fault-action"
              type="primary"
              @click="openUpdate(fault)"
            >
              处理此故障
            </el-button>
          </el-card>
        </div>
        <el-empty v-else description="该生产线暂无故障记录" />
      </div>
      <template #footer>
        <el-button @click="faultsVisible = false">关闭</el-button>
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
.fault-list {
  max-height: 500px;
  overflow-y: auto;
}
.fault-card {
  margin-bottom: 16px;
}
.fault-card:last-child {
  margin-bottom: 0;
}
.fault-action {
  margin-top: 16px;
}
</style>
