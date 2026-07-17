<script setup lang="ts">
import {
  type CapacityConfigFormData,
  type CapacityConfigItem,
  type LineTypeFormData,
  type LineTypeItem,
  type PageResult,
  type ProductionCalendarFormData,
  type ProductionCalendarItem,
  type ProductionLineFormData,
  type ProductionLineItem,
  type ProductionLineRunStatus,
  productionService,
} from '@/services/ProductionService'
import { Delete, EditPen, Plus, Refresh } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import { computed, onMounted, reactive, ref } from 'vue'
import PageContainer from '@/components/common/PageContainer.vue'
import PageHeader from '@/components/common/PageHeader.vue'
import StatusTag from '@/components/common/StatusTag.vue'
import { getErrorMessage } from '@/utils/error'
import { useAuthStore } from '@/stores/auth'

type CapacityTab = 'calendars' | 'configs' | 'lines' | 'types'

const lineStatusLabels: Record<ProductionLineRunStatus, string> = {
  fault: '故障',
  idle: '空闲',
  running: '运行中',
}

const pageSize = 10
const auth = useAuthStore()
const canManage = computed(() => auth.hasPermission('production:capacity'))
const activeTab = ref<CapacityTab>('configs')

const lineTypeOptions = ref<LineTypeItem[]>([])

async function loadLineTypeOptions() {
  try {
    lineTypeOptions.value = await productionService.listAllLineTypes()
  } catch {
    lineTypeOptions.value = []
  }
}

// ---------- 产能配置 ----------
const configFilters = reactive({ materialId: '', typeId: undefined as number | undefined })
const configPage = ref(1)
const configLoading = ref(false)
const configError = ref('')
const configResult = ref<PageResult<CapacityConfigItem>>({
  items: [],
  page: 1,
  pageSize,
  total: 0,
})
const configDialogVisible = ref(false)
const configDialogMode = ref<'create' | 'edit'>('create')
const configFormRef = ref<FormInstance>()
const configSubmitting = ref(false)
const configForm = reactive<CapacityConfigFormData>({
  configId: undefined,
  materialId: 0,
  typeId: 0,
  unitTime: 1,
})
const configDialogTitle = computed(() => {
  if (configDialogMode.value === 'create') {
    return '新增产能配置'
  }
  return '修改产能配置'
})
const configRules: FormRules<CapacityConfigFormData> = {
  materialId: [
    { message: '请输入产品物料 ID', required: true, trigger: 'blur', type: 'number' },
    { message: '物料 ID 必须大于 0', min: 1, trigger: 'blur', type: 'number' },
  ],
  typeId: [
    { message: '请选择生产线类型', required: true, trigger: 'change', type: 'number' },
    { message: '请选择生产线类型', min: 1, trigger: 'change', type: 'number' },
  ],
  unitTime: [
    { message: '请输入单件工时', required: true, trigger: 'blur', type: 'number' },
    { message: '单件工时必须大于 0', min: 0.01, trigger: 'blur', type: 'number' },
  ],
}

function parsePositiveInt(value: string) {
  const parsed = Number(value)
  if (Number.isInteger(parsed) && parsed > 0) {
    return parsed
  }
  return undefined
}

async function loadConfigs(targetPage = configPage.value) {
  configLoading.value = true
  configError.value = ''
  try {
    configResult.value = await productionService.listCapacityConfigs({
      materialId: parsePositiveInt(configFilters.materialId),
      page: targetPage,
      pageSize,
      typeId: configFilters.typeId,
    })
    configPage.value = configResult.value.page
  } catch (requestError) {
    configError.value = getErrorMessage(requestError, '产能配置列表加载失败')
  } finally {
    configLoading.value = false
  }
}

function resetConfigFilters() {
  configFilters.materialId = ''
  configFilters.typeId = undefined
  void loadConfigs(1)
}

function openConfigCreate() {
  configDialogMode.value = 'create'
  Object.assign(configForm, { configId: undefined, materialId: 0, typeId: 0, unitTime: 1 })
  configFormRef.value?.clearValidate()
  configDialogVisible.value = true
}

function openConfigEdit(config: CapacityConfigItem) {
  configDialogMode.value = 'edit'
  Object.assign(configForm, {
    configId: config.configId,
    materialId: config.materialId,
    typeId: config.typeId,
    unitTime: config.unitTime,
  })
  configFormRef.value?.clearValidate()
  configDialogVisible.value = true
}

async function submitConfigForm() {
  const valid = await configFormRef.value?.validate().catch(() => false)
  if (!valid || configSubmitting.value) {
    return
  }
  configSubmitting.value = true
  try {
    await productionService.saveCapacityConfig({ ...configForm })
    let successMessage = '产能配置已更新'
    if (configDialogMode.value === 'create') {
      successMessage = '产能配置已新增'
    }
    ElMessage.success(successMessage)
    configDialogVisible.value = false
    await loadConfigs(configPage.value)
  } catch (requestError) {
    ElMessage.error(getErrorMessage(requestError, '产能配置提交失败'))
  } finally {
    configSubmitting.value = false
  }
}

// ---------- 生产线 ----------
const lineFilters = reactive({
  status: '' as '' | ProductionLineRunStatus,
  typeId: undefined as number | undefined,
})
const linePage = ref(1)
const lineLoading = ref(false)
const lineError = ref('')
const lineResult = ref<PageResult<ProductionLineItem>>({ items: [], page: 1, pageSize, total: 0 })
const lineDialogVisible = ref(false)
const lineDialogMode = ref<'create' | 'edit'>('create')
const lineFormRef = ref<FormInstance>()
const lineSubmitting = ref(false)
const editingLineId = ref<number>()
const lineForm = reactive<ProductionLineFormData>({ managerId: 0, startDate: '', typeId: 0 })
const lineDialogTitle = computed(() => {
  if (lineDialogMode.value === 'create') {
    return '新增生产线'
  }
  return '修改生产线'
})
const lineRules: FormRules<ProductionLineFormData> = {
  managerId: [
    { message: '请输入负责人用户 ID', required: true, trigger: 'blur', type: 'number' },
    { message: '负责人 ID 必须大于 0', min: 1, trigger: 'blur', type: 'number' },
  ],
  startDate: [{ message: '请选择启用日期', required: true, trigger: 'change' }],
  typeId: [
    { message: '请选择生产线类型', required: true, trigger: 'change', type: 'number' },
    { message: '请选择生产线类型', min: 1, trigger: 'change', type: 'number' },
  ],
}

function selectedLineStatus(): ProductionLineRunStatus | undefined {
  return lineFilters.status || undefined
}

async function loadLines(targetPage = linePage.value) {
  lineLoading.value = true
  lineError.value = ''
  try {
    lineResult.value = await productionService.listLines({
      page: targetPage,
      pageSize,
      status: selectedLineStatus(),
      typeId: lineFilters.typeId,
    })
    linePage.value = lineResult.value.page
  } catch (requestError) {
    lineError.value = getErrorMessage(requestError, '生产线列表加载失败')
  } finally {
    lineLoading.value = false
  }
}

function resetLineFilters() {
  lineFilters.status = ''
  lineFilters.typeId = undefined
  void loadLines(1)
}

function openLineCreate() {
  lineDialogMode.value = 'create'
  Object.assign(lineForm, { managerId: 0, startDate: '', typeId: 0 })
  editingLineId.value = undefined
  lineFormRef.value?.clearValidate()
  lineDialogVisible.value = true
}

function openLineEdit(line: ProductionLineItem) {
  lineDialogMode.value = 'edit'
  Object.assign(lineForm, {
    managerId: line.managerId,
    startDate: line.startDate,
    typeId: line.typeId,
  })
  editingLineId.value = line.lineId
  lineFormRef.value?.clearValidate()
  lineDialogVisible.value = true
}

async function submitLineForm() {
  const valid = await lineFormRef.value?.validate().catch(() => false)
  if (!valid || lineSubmitting.value) {
    return
  }
  lineSubmitting.value = true
  try {
    if (lineDialogMode.value === 'create') {
      await productionService.createLine({ ...lineForm })
      ElMessage.success('生产线已新增')
    } else if (editingLineId.value !== undefined) {
      await productionService.updateLine(editingLineId.value, { ...lineForm })
      ElMessage.success('生产线已更新')
    }
    lineDialogVisible.value = false
    await loadLines(linePage.value)
  } catch (requestError) {
    ElMessage.error(getErrorMessage(requestError, '生产线提交失败'))
  } finally {
    lineSubmitting.value = false
  }
}

// ---------- 生产线类型 ----------
const typeFilters = reactive({ typeName: '' })
const typePage = ref(1)
const typeLoading = ref(false)
const typeError = ref('')
const typeResult = ref<PageResult<LineTypeItem>>({ items: [], page: 1, pageSize, total: 0 })
const typeDialogVisible = ref(false)
const typeDialogMode = ref<'create' | 'edit'>('create')
const typeFormRef = ref<FormInstance>()
const typeSubmitting = ref(false)
const typeForm = reactive<LineTypeFormData>({ typeId: undefined, typeName: '' })
const typeDialogTitle = computed(() => {
  if (typeDialogMode.value === 'create') {
    return '新增生产线类型'
  }
  return '修改生产线类型'
})
const typeRules: FormRules<LineTypeFormData> = {
  typeName: [
    { message: '请输入类型名称', required: true, trigger: 'blur' },
    { max: 50, message: '类型名称不能超过 50 个字符', trigger: 'blur' },
  ],
}

async function loadTypes(targetPage = typePage.value) {
  typeLoading.value = true
  typeError.value = ''
  try {
    typeResult.value = await productionService.listLineTypes({
      page: targetPage,
      pageSize,
      typeName: typeFilters.typeName.trim() || undefined,
    })
    typePage.value = typeResult.value.page
  } catch (requestError) {
    typeError.value = getErrorMessage(requestError, '生产线类型列表加载失败')
  } finally {
    typeLoading.value = false
  }
}

function resetTypeFilters() {
  typeFilters.typeName = ''
  void loadTypes(1)
}

function openTypeCreate() {
  typeDialogMode.value = 'create'
  Object.assign(typeForm, { typeId: undefined, typeName: '' })
  typeFormRef.value?.clearValidate()
  typeDialogVisible.value = true
}

function openTypeEdit(type: LineTypeItem) {
  typeDialogMode.value = 'edit'
  Object.assign(typeForm, { typeId: type.typeId, typeName: type.typeName })
  typeFormRef.value?.clearValidate()
  typeDialogVisible.value = true
}

async function submitTypeForm() {
  const valid = await typeFormRef.value?.validate().catch(() => false)
  if (!valid || typeSubmitting.value) {
    return
  }
  typeSubmitting.value = true
  try {
    await productionService.saveLineType({ ...typeForm })
    let successMessage = '生产线类型已更新'
    if (typeDialogMode.value === 'create') {
      successMessage = '生产线类型已新增'
    }
    ElMessage.success(successMessage)
    typeDialogVisible.value = false
    await Promise.all([loadTypes(typePage.value), loadLineTypeOptions()])
  } catch (requestError) {
    ElMessage.error(getErrorMessage(requestError, '生产线类型提交失败'))
  } finally {
    typeSubmitting.value = false
  }
}

// ---------- 生产日历 ----------
const calendarFilters = reactive({
  calendarDateEnd: '',
  calendarDateStart: '',
  lineId: '',
})
const calendarPage = ref(1)
const calendarLoading = ref(false)
const calendarError = ref('')
const calendarResult = ref<PageResult<ProductionCalendarItem>>({
  items: [],
  page: 1,
  pageSize,
  total: 0,
})
const calendarDialogVisible = ref(false)
const calendarFormRef = ref<FormInstance>()
const calendarSubmitting = ref(false)
const calendarDeleting = ref(false)
const calendarForm = reactive<ProductionCalendarFormData>({
  calendarDate: '',
  configId: 0,
  lineId: 0,
})
const calendarRules: FormRules<ProductionCalendarFormData> = {
  calendarDate: [{ message: '请选择排产日期', required: true, trigger: 'change' }],
  configId: [
    { message: '请输入产能配置 ID', required: true, trigger: 'blur', type: 'number' },
    { message: '产能配置 ID 必须大于 0', min: 1, trigger: 'blur', type: 'number' },
  ],
  lineId: [
    { message: '请输入生产线 ID', required: true, trigger: 'blur', type: 'number' },
    { message: '生产线 ID 必须大于 0', min: 1, trigger: 'blur', type: 'number' },
  ],
}

async function loadCalendars(targetPage = calendarPage.value) {
  calendarLoading.value = true
  calendarError.value = ''
  try {
    calendarResult.value = await productionService.listCalendars({
      calendarDateEnd: calendarFilters.calendarDateEnd || undefined,
      calendarDateStart: calendarFilters.calendarDateStart || undefined,
      lineId: parsePositiveInt(calendarFilters.lineId),
      page: targetPage,
      pageSize,
    })
    calendarPage.value = calendarResult.value.page
  } catch (requestError) {
    calendarError.value = getErrorMessage(requestError, '生产日历列表加载失败')
  } finally {
    calendarLoading.value = false
  }
}

function resetCalendarFilters() {
  Object.assign(calendarFilters, { calendarDateEnd: '', calendarDateStart: '', lineId: '' })
  void loadCalendars(1)
}

function openCalendarCreate() {
  Object.assign(calendarForm, { calendarDate: '', configId: 0, lineId: 0 })
  calendarFormRef.value?.clearValidate()
  calendarDialogVisible.value = true
}

async function submitCalendarForm() {
  const valid = await calendarFormRef.value?.validate().catch(() => false)
  if (!valid || calendarSubmitting.value) {
    return
  }
  calendarSubmitting.value = true
  try {
    await productionService.saveCalendar({ ...calendarForm })
    ElMessage.success('排产日历已保存')
    calendarDialogVisible.value = false
    await loadCalendars(calendarPage.value)
  } catch (requestError) {
    ElMessage.error(getErrorMessage(requestError, '排产日历保存失败'))
  } finally {
    calendarSubmitting.value = false
  }
}

async function removeCalendar(calendar: ProductionCalendarItem) {
  if (calendarDeleting.value) {
    return
  }
  try {
    calendarDeleting.value = true
    await ElMessageBox.confirm(
      `确定要删除生产线 #${calendar.lineId} 在 ${calendar.calendarDate} 的排产吗？`,
      '删除排产',
      { confirmButtonText: '确认删除', type: 'warning' },
    )
    await productionService.deleteCalendar(calendar.calendarDate, calendar.lineId)
    ElMessage.success('排产已删除')
    await loadCalendars(calendarPage.value)
  } catch (requestError) {
    if (requestError !== 'cancel' && requestError !== 'close') {
      ElMessage.error(getErrorMessage(requestError, '排产删除失败'))
    }
  } finally {
    calendarDeleting.value = false
  }
}

function handleTabChange(tab: string) {
  const target = tab as CapacityTab
  if (target === 'configs' && !configResult.value.items.length && !configError.value) {
    void loadConfigs(1)
  } else if (target === 'lines' && !lineResult.value.items.length && !lineError.value) {
    void loadLines(1)
  } else if (target === 'types' && !typeResult.value.items.length && !typeError.value) {
    void loadTypes(1)
  } else if (target === 'calendars' && !calendarResult.value.items.length && !calendarError.value) {
    void loadCalendars(1)
  }
}

onMounted(() => {
  void loadLineTypeOptions()
  void loadConfigs()
})
</script>

<template>
  <PageContainer>
    <PageHeader
      title="产能配置"
      description="维护产品产能、生产线、生产线类型与排产日历等生产基础数据。"
    />

    <el-tabs v-model="activeTab" class="capacity-tabs" @tab-change="handleTabChange">
      <!-- 产能配置 -->
      <el-tab-pane label="产能配置" name="configs">
        <el-card class="section-card" shadow="never">
          <el-form :model="configFilters" inline @submit.prevent="loadConfigs(1)">
            <el-form-item label="产品物料 ID">
              <el-input
                v-model.trim="configFilters.materialId"
                clearable
                placeholder="按物料 ID 查询"
              />
            </el-form-item>
            <el-form-item label="生产线类型">
              <el-select
                v-model="configFilters.typeId"
                clearable
                placeholder="全部"
                style="width: 160px"
              >
                <el-option
                  v-for="type in lineTypeOptions"
                  :key="type.typeId"
                  :label="type.typeName"
                  :value="type.typeId"
                />
              </el-select>
            </el-form-item>
            <el-form-item>
              <el-button type="primary" :loading="configLoading" @click="loadConfigs(1)">
                查询
              </el-button>
              <el-button :disabled="configLoading" :icon="Refresh" @click="resetConfigFilters">
                重置
              </el-button>
              <el-button v-if="canManage" :icon="Plus" type="primary" @click="openConfigCreate">
                新增配置
              </el-button>
            </el-form-item>
          </el-form>
        </el-card>

        <el-alert
          v-if="configError"
          class="section-error"
          :closable="false"
          show-icon
          :title="configError"
          type="error"
        >
          <template #default>
            <el-button link type="primary" @click="loadConfigs(configPage)">重新加载</el-button>
          </template>
        </el-alert>

        <el-table v-else v-loading="configLoading" :data="configResult.items" stripe>
          <el-table-column label="配置 ID" min-width="90" prop="configId" />
          <el-table-column label="产品" min-width="160">
            <template #default="{ row }">{{
              row.materialName || `物料 #${row.materialId}`
            }}</template>
          </el-table-column>
          <el-table-column label="生产线类型" min-width="150">
            <template #default="{ row }">{{ row.typeName || `#${row.typeId}` }}</template>
          </el-table-column>
          <el-table-column label="单件工时" min-width="110" prop="unitTime" />
          <el-table-column v-if="canManage" fixed="right" label="操作" min-width="100">
            <template #default="{ row }">
              <el-button link type="primary" :icon="EditPen" @click="openConfigEdit(row)">
                修改
              </el-button>
            </template>
          </el-table-column>
        </el-table>

        <el-empty
          v-if="!configLoading && !configError && !configResult.items.length"
          description="暂无产能配置数据"
        />

        <div v-if="!configError && configResult.total > 0" class="pagination">
          <el-pagination
            v-model:current-page="configPage"
            background
            layout="total, prev, pager, next"
            :page-size="pageSize"
            :total="configResult.total"
            @current-change="loadConfigs"
          />
        </div>
      </el-tab-pane>

      <!-- 生产线 -->
      <el-tab-pane label="生产线" name="lines">
        <el-card class="section-card" shadow="never">
          <el-form :model="lineFilters" inline @submit.prevent="loadLines(1)">
            <el-form-item label="生产线类型">
              <el-select
                v-model="lineFilters.typeId"
                clearable
                placeholder="全部"
                style="width: 160px"
              >
                <el-option
                  v-for="type in lineTypeOptions"
                  :key="type.typeId"
                  :label="type.typeName"
                  :value="type.typeId"
                />
              </el-select>
            </el-form-item>
            <el-form-item label="运行状态">
              <el-select
                v-model="lineFilters.status"
                clearable
                placeholder="全部"
                style="width: 140px"
              >
                <el-option label="空闲" value="idle" />
                <el-option label="运行中" value="running" />
                <el-option label="故障" value="fault" />
              </el-select>
            </el-form-item>
            <el-form-item>
              <el-button type="primary" :loading="lineLoading" @click="loadLines(1)"
                >查询</el-button
              >
              <el-button :disabled="lineLoading" :icon="Refresh" @click="resetLineFilters">
                重置
              </el-button>
              <el-button v-if="canManage" :icon="Plus" type="primary" @click="openLineCreate">
                新增生产线
              </el-button>
            </el-form-item>
          </el-form>
        </el-card>

        <el-alert
          v-if="lineError"
          class="section-error"
          :closable="false"
          show-icon
          :title="lineError"
          type="error"
        >
          <template #default>
            <el-button link type="primary" @click="loadLines(linePage)">重新加载</el-button>
          </template>
        </el-alert>

        <el-table v-else v-loading="lineLoading" :data="lineResult.items" stripe>
          <el-table-column label="生产线 ID" min-width="100" prop="lineId" />
          <el-table-column label="生产线类型" min-width="150">
            <template #default="{ row }">{{ row.typeName || `#${row.typeId}` }}</template>
          </el-table-column>
          <el-table-column label="负责人" min-width="140">
            <template #default="{ row }">{{
              row.managerName || `用户 #${row.managerId}`
            }}</template>
          </el-table-column>
          <el-table-column label="启用日期" min-width="130">
            <template #default="{ row }">{{ row.startDate || '-' }}</template>
          </el-table-column>
          <el-table-column label="运行状态" min-width="110">
            <template #default="{ row }">
              <StatusTag :labels="lineStatusLabels" :value="row.status" />
            </template>
          </el-table-column>
          <el-table-column v-if="canManage" fixed="right" label="操作" min-width="100">
            <template #default="{ row }">
              <el-button link type="primary" :icon="EditPen" @click="openLineEdit(row)">
                修改
              </el-button>
            </template>
          </el-table-column>
        </el-table>

        <el-empty
          v-if="!lineLoading && !lineError && !lineResult.items.length"
          description="暂无生产线数据"
        />

        <div v-if="!lineError && lineResult.total > 0" class="pagination">
          <el-pagination
            v-model:current-page="linePage"
            background
            layout="total, prev, pager, next"
            :page-size="pageSize"
            :total="lineResult.total"
            @current-change="loadLines"
          />
        </div>
      </el-tab-pane>

      <!-- 生产线类型 -->
      <el-tab-pane label="生产线类型" name="types">
        <el-card class="section-card" shadow="never">
          <el-form :model="typeFilters" inline @submit.prevent="loadTypes(1)">
            <el-form-item label="类型名称">
              <el-input v-model.trim="typeFilters.typeName" clearable placeholder="支持模糊查询" />
            </el-form-item>
            <el-form-item>
              <el-button type="primary" :loading="typeLoading" @click="loadTypes(1)"
                >查询</el-button
              >
              <el-button :disabled="typeLoading" :icon="Refresh" @click="resetTypeFilters">
                重置
              </el-button>
              <el-button v-if="canManage" :icon="Plus" type="primary" @click="openTypeCreate">
                新增类型
              </el-button>
            </el-form-item>
          </el-form>
        </el-card>

        <el-alert
          v-if="typeError"
          class="section-error"
          :closable="false"
          show-icon
          :title="typeError"
          type="error"
        >
          <template #default>
            <el-button link type="primary" @click="loadTypes(typePage)">重新加载</el-button>
          </template>
        </el-alert>

        <el-table v-else v-loading="typeLoading" :data="typeResult.items" stripe>
          <el-table-column label="类型 ID" min-width="100" prop="typeId" />
          <el-table-column label="类型名称" min-width="200" prop="typeName" />
          <el-table-column v-if="canManage" fixed="right" label="操作" min-width="100">
            <template #default="{ row }">
              <el-button link type="primary" :icon="EditPen" @click="openTypeEdit(row)">
                修改
              </el-button>
            </template>
          </el-table-column>
        </el-table>

        <el-empty
          v-if="!typeLoading && !typeError && !typeResult.items.length"
          description="暂无生产线类型数据"
        />

        <div v-if="!typeError && typeResult.total > 0" class="pagination">
          <el-pagination
            v-model:current-page="typePage"
            background
            layout="total, prev, pager, next"
            :page-size="pageSize"
            :total="typeResult.total"
            @current-change="loadTypes"
          />
        </div>
      </el-tab-pane>

      <!-- 生产日历 -->
      <el-tab-pane label="生产日历" name="calendars">
        <el-card class="section-card" shadow="never">
          <el-form :model="calendarFilters" inline @submit.prevent="loadCalendars(1)">
            <el-form-item label="生产线 ID">
              <el-input
                v-model.trim="calendarFilters.lineId"
                clearable
                placeholder="按生产线 ID 查询"
              />
            </el-form-item>
            <el-form-item label="排产日期起">
              <el-date-picker
                v-model="calendarFilters.calendarDateStart"
                placeholder="开始日期"
                type="date"
                value-format="YYYY-MM-DD"
              />
            </el-form-item>
            <el-form-item label="排产日期止">
              <el-date-picker
                v-model="calendarFilters.calendarDateEnd"
                placeholder="结束日期"
                type="date"
                value-format="YYYY-MM-DD"
              />
            </el-form-item>
            <el-form-item>
              <el-button type="primary" :loading="calendarLoading" @click="loadCalendars(1)">
                查询
              </el-button>
              <el-button :disabled="calendarLoading" :icon="Refresh" @click="resetCalendarFilters">
                重置
              </el-button>
              <el-button v-if="canManage" :icon="Plus" type="primary" @click="openCalendarCreate">
                新增排产
              </el-button>
            </el-form-item>
          </el-form>
        </el-card>

        <el-alert
          v-if="calendarError"
          class="section-error"
          :closable="false"
          show-icon
          :title="calendarError"
          type="error"
        >
          <template #default>
            <el-button link type="primary" @click="loadCalendars(calendarPage)">
              重新加载
            </el-button>
          </template>
        </el-alert>

        <el-table v-else v-loading="calendarLoading" :data="calendarResult.items" stripe>
          <el-table-column label="排产日期" min-width="130" prop="calendarDate" />
          <el-table-column label="生产线" min-width="150">
            <template #default="{ row }">{{ row.lineName || `生产线 #${row.lineId}` }}</template>
          </el-table-column>
          <el-table-column label="产能配置 ID" min-width="110" prop="configId" />
          <el-table-column label="产品" min-width="150">
            <template #default="{ row }">
              {{ row.materialName || (row.materialId ? `物料 #${row.materialId}` : '-') }}
            </template>
          </el-table-column>
          <el-table-column label="生产线类型" min-width="140">
            <template #default="{ row }">
              {{ row.typeName || (row.typeId ? `#${row.typeId}` : '-') }}
            </template>
          </el-table-column>
          <el-table-column v-if="canManage" fixed="right" label="操作" min-width="100">
            <template #default="{ row }">
              <el-button
                link
                :disabled="calendarDeleting"
                :icon="Delete"
                type="danger"
                @click="removeCalendar(row)"
              >
                删除
              </el-button>
            </template>
          </el-table-column>
        </el-table>

        <el-empty
          v-if="!calendarLoading && !calendarError && !calendarResult.items.length"
          description="暂无排产日历数据"
        />

        <div v-if="!calendarError && calendarResult.total > 0" class="pagination">
          <el-pagination
            v-model:current-page="calendarPage"
            background
            layout="total, prev, pager, next"
            :page-size="pageSize"
            :total="calendarResult.total"
            @current-change="loadCalendars"
          />
        </div>
      </el-tab-pane>
    </el-tabs>

    <!-- 产能配置 dialog -->
    <el-dialog
      v-model="configDialogVisible"
      :close-on-click-modal="false"
      :title="configDialogTitle"
      width="520px"
    >
      <el-form ref="configFormRef" :model="configForm" :rules="configRules" label-width="120px">
        <el-form-item label="产品物料 ID" prop="materialId">
          <el-input-number v-model="configForm.materialId" :min="1" style="width: 100%" />
        </el-form-item>
        <el-form-item label="生产线类型" prop="typeId">
          <el-select v-model="configForm.typeId" placeholder="请选择生产线类型" style="width: 100%">
            <el-option
              v-for="type in lineTypeOptions"
              :key="type.typeId"
              :label="type.typeName"
              :value="type.typeId"
            />
          </el-select>
        </el-form-item>
        <el-form-item label="单件工时" prop="unitTime">
          <el-input-number
            v-model="configForm.unitTime"
            :min="0.01"
            :precision="2"
            :step="0.5"
            style="width: 100%"
          />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button :disabled="configSubmitting" @click="configDialogVisible = false"
          >取消</el-button
        >
        <el-button :loading="configSubmitting" type="primary" @click="submitConfigForm">
          保存
        </el-button>
      </template>
    </el-dialog>

    <!-- 生产线 dialog -->
    <el-dialog
      v-model="lineDialogVisible"
      :close-on-click-modal="false"
      :title="lineDialogTitle"
      width="520px"
    >
      <el-form ref="lineFormRef" :model="lineForm" :rules="lineRules" label-width="120px">
        <el-form-item label="生产线类型" prop="typeId">
          <el-select v-model="lineForm.typeId" placeholder="请选择生产线类型" style="width: 100%">
            <el-option
              v-for="type in lineTypeOptions"
              :key="type.typeId"
              :label="type.typeName"
              :value="type.typeId"
            />
          </el-select>
        </el-form-item>
        <el-form-item label="负责人用户 ID" prop="managerId">
          <el-input-number v-model="lineForm.managerId" :min="1" style="width: 100%" />
        </el-form-item>
        <el-form-item label="启用日期" prop="startDate">
          <el-date-picker
            v-model="lineForm.startDate"
            style="width: 100%"
            type="date"
            value-format="YYYY-MM-DD"
          />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button :disabled="lineSubmitting" @click="lineDialogVisible = false">取消</el-button>
        <el-button :loading="lineSubmitting" type="primary" @click="submitLineForm">保存</el-button>
      </template>
    </el-dialog>

    <!-- 生产线类型 dialog -->
    <el-dialog
      v-model="typeDialogVisible"
      :close-on-click-modal="false"
      :title="typeDialogTitle"
      width="460px"
    >
      <el-form ref="typeFormRef" :model="typeForm" :rules="typeRules" label-width="100px">
        <el-form-item label="类型名称" prop="typeName">
          <el-input v-model.trim="typeForm.typeName" maxlength="50" show-word-limit />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button :disabled="typeSubmitting" @click="typeDialogVisible = false">取消</el-button>
        <el-button :loading="typeSubmitting" type="primary" @click="submitTypeForm">保存</el-button>
      </template>
    </el-dialog>

    <!-- 生产日历 dialog -->
    <el-dialog
      v-model="calendarDialogVisible"
      :close-on-click-modal="false"
      title="新增排产"
      width="520px"
    >
      <el-form
        ref="calendarFormRef"
        :model="calendarForm"
        :rules="calendarRules"
        label-width="120px"
      >
        <el-form-item label="排产日期" prop="calendarDate">
          <el-date-picker
            v-model="calendarForm.calendarDate"
            style="width: 100%"
            type="date"
            value-format="YYYY-MM-DD"
          />
        </el-form-item>
        <el-form-item label="生产线 ID" prop="lineId">
          <el-input-number v-model="calendarForm.lineId" :min="1" style="width: 100%" />
        </el-form-item>
        <el-form-item label="产能配置 ID" prop="configId">
          <el-input-number v-model="calendarForm.configId" :min="1" style="width: 100%" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button :disabled="calendarSubmitting" @click="calendarDialogVisible = false">
          取消
        </el-button>
        <el-button :loading="calendarSubmitting" type="primary" @click="submitCalendarForm">
          保存
        </el-button>
      </template>
    </el-dialog>
  </PageContainer>
</template>

<style scoped>
.capacity-tabs {
  min-width: 0;
}
.section-card {
  margin-bottom: 16px;
}
.section-card :deep(.el-card__body) {
  padding-bottom: 2px;
}
.section-error {
  margin-bottom: 16px;
}
.pagination {
  display: flex;
  justify-content: flex-end;
  margin-top: 16px;
}
</style>
