<script setup lang="ts">
import { ElMessage, type FormInstance, type FormRules } from 'element-plus'
import {
  type FaultRecordItem,
  type FaultReportFormData,
  type FaultStatusValue,
  type FaultUpdateFormData,
  productionService,
} from '@/services/ProductionService'
import { computed, reactive, ref } from 'vue'
import PageContainer from '@/components/common/PageContainer.vue'
import PageHeader from '@/components/common/PageHeader.vue'
import StatusTag from '@/components/common/StatusTag.vue'
import { formatDateTime } from '@/utils/format'
import { getErrorMessage } from '@/utils/error'
import { useAuthStore } from '@/stores/auth'

const faultStatusLabels: Record<FaultStatusValue, string> = {
  pending_repair: '待维修',
  recovered: '已恢复',
  repairing: '维修中',
}

const auth = useAuthStore()
const canManage = computed(() => auth.hasPermission('production:breakdown'))

// ---------- 故障上报 ----------
const reportFormRef = ref<FormInstance>()
const reporting = ref(false)
const reportForm = reactive<FaultReportFormData>({ description: '', faultType: '', lineId: 0 })
const reportRules: FormRules<FaultReportFormData> = {
  description: [
    { message: '请输入故障描述', required: true, trigger: 'blur' },
    { max: 500, message: '故障描述不能超过 500 个字符', trigger: 'blur' },
  ],
  faultType: [
    { message: '请输入故障类型', required: true, trigger: 'blur' },
    { max: 50, message: '故障类型不能超过 50 个字符', trigger: 'blur' },
  ],
  lineId: [
    { message: '请输入发生故障的生产线 ID', required: true, trigger: 'blur', type: 'number' },
    { message: '生产线 ID 必须大于 0', min: 1, trigger: 'blur', type: 'number' },
  ],
}

// ---------- 故障处理 ----------
const updateFormRef = ref<FormInstance>()
const updating = ref(false)
const updateForm = reactive<FaultUpdateFormData>({
  faultId: 0,
  recoverTime: '',
  repairerId: undefined,
  status: 'repairing',
})
const updateRules: FormRules<FaultUpdateFormData> = {
  faultId: [
    { message: '请输入故障记录 ID', required: true, trigger: 'blur', type: 'number' },
    { message: '故障记录 ID 必须大于 0', min: 1, trigger: 'blur', type: 'number' },
  ],
  recoverTime: [
    {
      trigger: 'change',
      validator: (_rule, value, callback) => {
        if (updateForm.status === 'recovered' && !value) {
          callback(new Error('故障状态为已恢复时必须填写恢复时间'))
          return
        }
        callback()
      },
    },
  ],
  status: [{ message: '请选择故障状态', required: true, trigger: 'change' }],
}

const requireRecoverTime = computed(() => updateForm.status === 'recovered')

// ---------- 处理结果列表（会话内） ----------
const records = ref<FaultRecordItem[]>([])

function upsertRecord(record: FaultRecordItem) {
  const index = records.value.findIndex((item) => item.faultId === record.faultId)
  if (index !== -1) {
    records.value.splice(index, 1, record)
  } else {
    records.value.unshift(record)
  }
}

async function submitReport() {
  const valid = await reportFormRef.value?.validate().catch(() => false)
  if (!valid || reporting.value) {
    return
  }
  reporting.value = true
  try {
    const record = await productionService.reportFault({ ...reportForm })
    ElMessage.success('故障已上报')
    if (record) {
      upsertRecord(record)
    }
    Object.assign(reportForm, { description: '', faultType: '', lineId: 0 })
    reportFormRef.value?.clearValidate()
  } catch (requestError) {
    ElMessage.error(getErrorMessage(requestError, '故障上报失败'))
  } finally {
    reporting.value = false
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
    if (requireRecoverTime.value) {
      ;({ recoverTime } = updateForm)
    }
    const record = await productionService.updateFault({
      faultId: updateForm.faultId,
      recoverTime,
      repairerId: updateForm.repairerId,
      status: updateForm.status,
    })
    ElMessage.success('故障处理进度已更新')
    if (record) {
      upsertRecord(record)
    }
  } catch (requestError) {
    ElMessage.error(getErrorMessage(requestError, '故障处理更新失败'))
  } finally {
    updating.value = false
  }
}

function fillUpdateForm(record: FaultRecordItem) {
  Object.assign(updateForm, {
    faultId: record.faultId,
    recoverTime: record.recoverTime ?? '',
    repairerId: record.repairerId,
    status: record.status,
  })
  updateFormRef.value?.clearValidate()
}
</script>

<template>
  <PageContainer>
    <PageHeader title="故障反馈" description="上报生产线故障并跟踪维修处理进度。" />

    <el-alert
      v-if="!canManage"
      class="section-error"
      :closable="false"
      show-icon
      title="当前账号仅可查看故障反馈页面，暂无上报与处理权限。"
      type="info"
    />

    <div class="breakdown-grid">
      <el-card class="form-card" shadow="never">
        <template #header><span>故障上报</span></template>
        <el-form
          ref="reportFormRef"
          :disabled="!canManage"
          label-width="110px"
          :model="reportForm"
          :rules="reportRules"
        >
          <el-form-item label="生产线 ID" prop="lineId">
            <el-input-number v-model="reportForm.lineId" :min="1" style="width: 100%" />
          </el-form-item>
          <el-form-item label="故障类型" prop="faultType">
            <el-input
              v-model.trim="reportForm.faultType"
              maxlength="50"
              placeholder="如：设备停机、电路异常"
              show-word-limit
            />
          </el-form-item>
          <el-form-item label="故障描述" prop="description">
            <el-input
              v-model="reportForm.description"
              maxlength="500"
              :rows="4"
              show-word-limit
              type="textarea"
            />
          </el-form-item>
          <el-form-item>
            <el-button
              :disabled="!canManage"
              :loading="reporting"
              type="primary"
              @click="submitReport"
            >
              提交上报
            </el-button>
          </el-form-item>
        </el-form>
      </el-card>

      <el-card class="form-card" shadow="never">
        <template #header><span>故障处理</span></template>
        <el-form
          ref="updateFormRef"
          :disabled="!canManage"
          label-width="110px"
          :model="updateForm"
          :rules="updateRules"
        >
          <el-form-item label="故障记录 ID" prop="faultId">
            <el-input-number v-model="updateForm.faultId" :min="1" style="width: 100%" />
          </el-form-item>
          <el-form-item label="处理状态" prop="status">
            <el-select v-model="updateForm.status" style="width: 100%">
              <el-option label="待维修" value="pending_repair" />
              <el-option label="维修中" value="repairing" />
              <el-option label="已恢复" value="recovered" />
            </el-select>
          </el-form-item>
          <el-form-item label="维修人 ID">
            <el-input-number
              v-model="updateForm.repairerId"
              :min="1"
              placeholder="可选"
              style="width: 100%"
            />
          </el-form-item>
          <el-form-item v-if="requireRecoverTime" label="恢复时间">
            <el-date-picker
              v-model="updateForm.recoverTime"
              placeholder="选择恢复时间"
              style="width: 100%"
              type="datetime"
              value-format="YYYY-MM-DD HH:mm:ss"
            />
          </el-form-item>
          <el-form-item>
            <el-button
              :disabled="!canManage"
              :loading="updating"
              type="primary"
              @click="submitUpdate"
            >
              更新进度
            </el-button>
          </el-form-item>
        </el-form>
      </el-card>
    </div>

    <el-card class="records-card" shadow="never">
      <template #header><span>本次操作记录</span></template>
      <el-empty
        v-if="!records.length"
        description="尚无操作记录，上报或处理故障后将显示在此。"
        :image-size="70"
      />
      <el-table v-else :data="records" stripe>
        <el-table-column label="故障 ID" min-width="90" prop="faultId" />
        <el-table-column label="生产线" min-width="100">
          <template #default="{ row }">{{ `#${row.lineId}` }}</template>
        </el-table-column>
        <el-table-column label="故障类型" min-width="130" prop="faultType" />
        <el-table-column
          label="故障描述"
          min-width="200"
          prop="description"
          show-overflow-tooltip
        />
        <el-table-column label="状态" min-width="100">
          <template #default="{ row }">
            <StatusTag :labels="faultStatusLabels" :value="row.status" />
          </template>
        </el-table-column>
        <el-table-column label="发生时间" min-width="170">
          <template #default="{ row }">{{ formatDateTime(row.occurTime) }}</template>
        </el-table-column>
        <el-table-column label="恢复时间" min-width="170">
          <template #default="{ row }">{{ formatDateTime(row.recoverTime) }}</template>
        </el-table-column>
        <el-table-column v-if="canManage" fixed="right" label="操作" min-width="100">
          <template #default="{ row }">
            <el-button link type="primary" @click="fillUpdateForm(row)">填入处理</el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>
  </PageContainer>
</template>

<style scoped>
.section-error {
  margin-bottom: 16px;
}
.breakdown-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 16px;
  margin-bottom: 16px;
}
.form-card {
  min-width: 0;
}
.records-card {
  min-width: 0;
}
@media (max-width: 960px) {
  .breakdown-grid {
    grid-template-columns: 1fr;
  }
}
</style>
