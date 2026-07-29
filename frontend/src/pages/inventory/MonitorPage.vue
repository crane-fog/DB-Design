<script setup lang="ts">
import { Bell, Lock, Plus, Refresh, Search, Warning } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import type {
  InventoryAlertItem,
  InventoryAlertQuery,
  ObsoleteMaterialItem,
  ObsoleteMaterialQuery,
  StockLockFormData,
  StockLockItem,
  StockLockQuery,
} from '@/types/inventory'
import { computed, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'
import { formatDateTime, formatNumber } from '@/utils/format'
import EmptyState from '@/components/common/EmptyState.vue'
import PageContainer from '@/components/common/PageContainer.vue'
import PageHeader from '@/components/common/PageHeader.vue'
import StatusTag from '@/components/common/StatusTag.vue'
import { getErrorMessage } from '@/utils/error'
import { inventoryService } from '@/services/InventoryService'
import { inventoryMonitorStatusLabels as statusLabels } from '@/constants/status'
import { useAuthStore } from '@/stores/auth'

type MonitorTab = 'alerts' | 'locks' | 'obsolete'

const auth = useAuthStore()
const operatorId = computed(() => auth.currentUser?.id)
const activeTab = ref<MonitorTab>('alerts')
let alive = true

// 库存预警
const alertLoading = ref(false)
const alertError = ref('')
const alertItems = ref<InventoryAlertItem[]>([])
const alertTotal = ref(0)
const alertQuery = reactive<InventoryAlertQuery>({ page: 1, pageSize: 10 })
const alertDateRange = ref<[string, string]>()
const generatingAlerts = ref(false)
const generateDialogOpen = ref(false)
const generateMaterialId = ref<number>()
const handlingAlert = ref<{ alertId: number; status: 'handled' | 'ignored' }>()
let alertRequestId = 0

// 库存锁定
const lockLoading = ref(false)
const lockError = ref('')
const lockItems = ref<StockLockItem[]>([])
const lockTotal = ref(0)
const lockQuery = reactive<StockLockQuery>({ page: 1, pageSize: 10 })
const lockDialogOpen = ref(false)
const locking = ref(false)
const releasingLockId = ref<number>()
const lockFormRef = ref<FormInstance>()
const lockForm = reactive<StockLockFormData>({
  items: [{ lockQty: 1, materialId: 0 }],
  operatorId: 0,
  orderId: 0,
})
const lockRules: FormRules<StockLockFormData> = {
  orderId: [
    { message: '请输入生产订单 ID', required: true, trigger: 'blur', type: 'number' },
    { message: '生产订单 ID 必须大于 0', min: 1, trigger: 'blur', type: 'number' },
  ],
}
let lockRequestId = 0

// 呆滞物料
const obsoleteLoading = ref(false)
const obsoleteError = ref('')
const obsoleteItems = ref<ObsoleteMaterialItem[]>([])
const obsoleteTotal = ref(0)
const obsoleteQuery = reactive<ObsoleteMaterialQuery>({ page: 1, pageSize: 10 })
const obsoleteDateRange = ref<[string, string]>()
const detectDialogOpen = ref(false)
const detecting = ref(false)
const handlingObsolete = ref<{
  detectionId: number
  status: 'handled' | 'ignored'
}>()
const detectionForm = reactive({
  idleDaysThreshold: 90,
  materialId: undefined as number | undefined,
})
let obsoleteRequestId = 0

async function loadAlerts() {
  const currentRequestId = ++alertRequestId
  alertLoading.value = true
  alertError.value = ''
  try {
    const result = await inventoryService.listAlerts({
      ...alertQuery,
      endTime: alertDateRange.value?.[1],
      startTime: alertDateRange.value?.[0],
    })
    if (!alive || currentRequestId !== alertRequestId) {
      return
    }
    alertItems.value = result.items
    alertTotal.value = result.total
  } catch (error) {
    if (alive && currentRequestId === alertRequestId) {
      alertError.value = getErrorMessage(error, '库存预警加载失败')
    }
  } finally {
    if (alive && currentRequestId === alertRequestId) {
      alertLoading.value = false
    }
  }
}

async function loadLocks() {
  const currentRequestId = ++lockRequestId
  lockLoading.value = true
  lockError.value = ''
  try {
    const result = await inventoryService.listLocks(lockQuery)
    if (!alive || currentRequestId !== lockRequestId) {
      return
    }
    lockItems.value = result.items
    lockTotal.value = result.total
  } catch (error) {
    if (alive && currentRequestId === lockRequestId) {
      lockError.value = getErrorMessage(error, '库存锁定记录加载失败')
    }
  } finally {
    if (alive && currentRequestId === lockRequestId) {
      lockLoading.value = false
    }
  }
}

async function loadObsolete() {
  const currentRequestId = ++obsoleteRequestId
  obsoleteLoading.value = true
  obsoleteError.value = ''
  try {
    const result = await inventoryService.listObsolete({
      ...obsoleteQuery,
      endTime: obsoleteDateRange.value?.[1],
      startTime: obsoleteDateRange.value?.[0],
    })
    if (!alive || currentRequestId !== obsoleteRequestId) {
      return
    }
    obsoleteItems.value = result.items
    obsoleteTotal.value = result.total
  } catch (error) {
    if (alive && currentRequestId === obsoleteRequestId) {
      obsoleteError.value = getErrorMessage(error, '呆滞物料记录加载失败')
    }
  } finally {
    if (alive && currentRequestId === obsoleteRequestId) {
      obsoleteLoading.value = false
    }
  }
}

function loadActiveTab() {
  if (activeTab.value === 'alerts') {
    void loadAlerts()
  }
  if (activeTab.value === 'locks') {
    void loadLocks()
  }
  if (activeTab.value === 'obsolete') {
    void loadObsolete()
  }
}

function resetAlertQuery() {
  Object.assign(alertQuery, { materialId: undefined, page: 1, status: undefined })
  alertDateRange.value = undefined
  void loadAlerts()
}

function resetLockQuery() {
  Object.assign(lockQuery, {
    materialId: undefined,
    orderId: undefined,
    page: 1,
    status: undefined,
  })
  void loadLocks()
}

function resetObsoleteQuery() {
  Object.assign(obsoleteQuery, { materialId: undefined, page: 1, status: undefined })
  obsoleteDateRange.value = undefined
  void loadObsolete()
}

function searchAlerts() {
  alertQuery.page = 1
  void loadAlerts()
}

function searchLocks() {
  lockQuery.page = 1
  void loadLocks()
}

function searchObsolete() {
  obsoleteQuery.page = 1
  void loadObsolete()
}

async function generateAlerts() {
  if (generatingAlerts.value) {
    return
  }
  generatingAlerts.value = true
  try {
    const result = await inventoryService.generateAlerts(generateMaterialId.value)
    let message = `未生成新预警，跳过 ${result.skippedPendingCount} 条待处理记录`
    if (result.generatedCount > 0) {
      message = `已生成 ${result.generatedCount} 条库存预警`
    }
    ElMessage.success(message)
    generateDialogOpen.value = false
    await loadAlerts()
  } catch (error) {
    ElMessage.error(getErrorMessage(error, '生成库存预警失败'))
  } finally {
    generatingAlerts.value = false
  }
}

async function handleAlert(item: InventoryAlertItem, status: 'handled' | 'ignored') {
  if (!operatorId.value) {
    ElMessage.error('当前会话缺少操作人信息，请重新登录')
    return
  }
  if (handlingAlert.value) {
    return
  }
  handlingAlert.value = { alertId: item.alertId, status }
  try {
    let message = `确认将预警 #${item.alertId} 标记为已处理？`
    let title = '完成库存预警'
    let type: 'info' | 'warning' = 'info'
    if (status === 'ignored') {
      message = `确认忽略物料 #${item.materialId} 的低库存预警？`
      title = '忽略库存预警'
      type = 'warning'
    }
    await ElMessageBox.confirm(message, title, { confirmButtonText: '确认', type })
    await inventoryService.handleAlert(item.alertId, status, operatorId.value)
    ElMessage.success('库存预警状态已更新')
    await loadAlerts()
  } catch (error) {
    if (error !== 'cancel' && error !== 'close') {
      ElMessage.error(getErrorMessage(error, '预警处理失败'))
    }
  } finally {
    handlingAlert.value = undefined
  }
}

function openLockDialog() {
  Object.assign(lockForm, {
    items: [{ lockQty: 1, materialId: 0 }],
    operatorId: operatorId.value ?? 0,
    orderId: 0,
  })
  lockDialogOpen.value = true
}

function addLockLine() {
  lockForm.items.push({ lockQty: 1, materialId: 0 })
}

function removeLockLine(index: number) {
  if (lockForm.items.length > 1) {
    lockForm.items.splice(index, 1)
  }
}

async function submitLock() {
  const valid = await lockFormRef.value?.validate().catch(() => false)
  if (!valid || locking.value) {
    return
  }
  if (!operatorId.value) {
    ElMessage.error('当前会话缺少操作人信息，请重新登录')
    return
  }
  if (lockForm.items.some((item) => item.materialId <= 0 || item.lockQty <= 0)) {
    ElMessage.warning('请完整填写锁定物料与数量')
    return
  }
  locking.value = true
  try {
    const result = await inventoryService.lockStock({
      items: lockForm.items.map((item) => ({ ...item })),
      operatorId: operatorId.value,
      orderId: lockForm.orderId,
    })
    if (!result.success) {
      const detail = result.shortages
        .map((item) => `物料 #${item.materialId} 缺 ${formatNumber(item.shortageQty)}`)
        .join('；')
      ElMessage.warning(detail || '库存不足，无法完成锁定')
      return
    }
    ElMessage.success(`已创建 ${result.items.length} 条库存锁定记录`)
    lockDialogOpen.value = false
    await loadLocks()
  } catch (error) {
    ElMessage.error(getErrorMessage(error, '锁定库存失败'))
  } finally {
    locking.value = false
  }
}

async function releaseLock(item: StockLockItem) {
  if (!operatorId.value || releasingLockId.value !== undefined) {
    return
  }
  releasingLockId.value = item.lockId
  try {
    await ElMessageBox.confirm(
      `释放后将恢复物料 #${item.materialId} 的 ${formatNumber(item.lockQty)} 库存，确认继续？`,
      '释放库存锁定',
      { confirmButtonText: '确认释放', type: 'warning' },
    )
    await inventoryService.releaseLock(item.lockId, operatorId.value)
    ElMessage.success('库存锁定已释放')
    await loadLocks()
  } catch (error) {
    if (error !== 'cancel' && error !== 'close') {
      ElMessage.error(getErrorMessage(error, '释放库存失败'))
    }
  } finally {
    releasingLockId.value = undefined
  }
}

async function detectObsolete() {
  if (detecting.value || detectionForm.idleDaysThreshold < 1) {
    return
  }
  detecting.value = true
  try {
    const result = await inventoryService.detectObsolete(
      detectionForm.idleDaysThreshold,
      detectionForm.materialId,
    )
    ElMessage.success(`检测完成，发现 ${result.detectedCount} 条记录`)
    detectDialogOpen.value = false
    await loadObsolete()
  } catch (error) {
    ElMessage.error(getErrorMessage(error, '呆滞物料检测失败'))
  } finally {
    detecting.value = false
  }
}

async function handleObsolete(item: ObsoleteMaterialItem, status: 'handled' | 'ignored') {
  if (!operatorId.value) {
    return
  }
  if (handlingObsolete.value) {
    return
  }
  handlingObsolete.value = { detectionId: item.detectionId, status }
  try {
    let statusLabel = '已处理'
    let type: 'info' | 'warning' = 'info'
    if (status === 'ignored') {
      statusLabel = '已忽略'
      type = 'warning'
    }
    await ElMessageBox.confirm(
      `确认将检测记录 #${item.detectionId} 标记为${statusLabel}？`,
      '处理呆滞物料',
      { confirmButtonText: '确认', type },
    )
    await inventoryService.handleObsolete(item.detectionId, status, operatorId.value)
    ElMessage.success('呆滞物料状态已更新')
    await loadObsolete()
  } catch (error) {
    if (error !== 'cancel' && error !== 'close') {
      ElMessage.error(getErrorMessage(error, '记录处理失败'))
    }
  } finally {
    handlingObsolete.value = undefined
  }
}

watch(activeTab, loadActiveTab)
onMounted(() => void loadAlerts())
onBeforeUnmount(() => {
  alive = false
  alertRequestId += 1
  lockRequestId += 1
  obsoleteRequestId += 1
})
</script>

<template>
  <PageContainer>
    <PageHeader title="库存监控" description="集中处理低库存预警、生产库存锁定和呆滞物料风险。">
      <template #actions>
        <el-button :icon="Refresh" @click="loadActiveTab">刷新当前列表</el-button>
      </template>
    </PageHeader>

    <el-card class="monitor-card" shadow="never">
      <el-tabs v-model="activeTab">
        <el-tab-pane name="alerts">
          <template #label
            ><span class="tab-label"
              ><el-icon><Bell /></el-icon>库存预警</span
            ></template
          >
          <div class="toolbar">
            <div class="filters">
              <el-input-number v-model="alertQuery.materialId" :min="1" placeholder="物料 ID" />
              <el-select v-model="alertQuery.status" clearable placeholder="处理状态">
                <el-option label="待处理" value="pending" /><el-option
                  label="已处理"
                  value="handled"
                /><el-option label="已忽略" value="ignored" />
              </el-select>
              <el-date-picker
                v-model="alertDateRange"
                end-placeholder="结束日期"
                range-separator="至"
                start-placeholder="开始日期"
                type="daterange"
                value-format="YYYY-MM-DD"
              />
              <el-button :icon="Search" type="primary" @click="searchAlerts">查询</el-button>
              <el-button @click="resetAlertQuery">重置</el-button>
            </div>
            <el-button :icon="Plus" type="primary" @click="generateDialogOpen = true"
              >生成预警</el-button
            >
          </div>
          <el-alert
            v-if="alertError"
            class="list-error"
            :closable="false"
            :title="alertError"
            type="error"
            ><template #default
              ><el-button link type="primary" @click="loadAlerts">重试</el-button></template
            ></el-alert
          >
          <div v-loading="alertLoading" class="table-area">
            <EmptyState
              v-if="!alertLoading && !alertError && !alertItems.length"
              description="当前筛选条件下没有库存预警。"
            />
            <el-table v-else :data="alertItems" stripe>
              <el-table-column label="预警 ID" min-width="90" prop="alertId" />
              <el-table-column label="物料" min-width="190"
                ><template #default="{ row }"
                  ><div class="primary-cell">
                    <strong>{{ row.materialName || `物料 #${row.materialId}` }}</strong
                    ><small>ID {{ row.materialId }}</small>
                  </div></template
                ></el-table-column
              >
              <el-table-column label="可用 / 阈值" min-width="130"
                ><template #default="{ row }"
                  ><strong class="risk-value">{{ formatNumber(row.availableQty) }}</strong> /
                  {{ formatNumber(row.threshold) }}</template
                ></el-table-column
              >
              <el-table-column label="预警时间" min-width="170"
                ><template #default="{ row }">{{
                  formatDateTime(row.alertTime)
                }}</template></el-table-column
              >
              <el-table-column label="状态" min-width="100"
                ><template #default="{ row }"
                  ><StatusTag :labels="statusLabels" :value="row.status" /></template
              ></el-table-column>
              <el-table-column fixed="right" label="操作" min-width="150"
                ><template #default="{ row }"
                  ><template v-if="row.status === 'pending'"
                    ><el-button
                      :disabled="handlingAlert !== undefined"
                      link
                      :loading="
                        handlingAlert?.alertId === row.alertId &&
                        handlingAlert?.status === 'handled'
                      "
                      type="primary"
                      @click="handleAlert(row, 'handled')"
                      >完成</el-button
                    ><el-button
                      :disabled="handlingAlert !== undefined"
                      link
                      :loading="
                        handlingAlert?.alertId === row.alertId &&
                        handlingAlert?.status === 'ignored'
                      "
                      type="danger"
                      @click="handleAlert(row, 'ignored')"
                      >忽略</el-button
                    ></template
                  ><span v-else>-</span></template
                ></el-table-column
              >
            </el-table>
          </div>
          <el-pagination
            v-if="alertTotal"
            v-model:current-page="alertQuery.page"
            v-model:page-size="alertQuery.pageSize"
            :page-sizes="[10, 20, 50]"
            background
            layout="total, sizes, prev, pager, next"
            :total="alertTotal"
            @change="loadAlerts"
          />
        </el-tab-pane>

        <el-tab-pane name="locks">
          <template #label
            ><span class="tab-label"
              ><el-icon><Lock /></el-icon>库存锁定</span
            ></template
          >
          <div class="toolbar">
            <div class="filters">
              <el-input-number v-model="lockQuery.orderId" :min="1" placeholder="订单 ID" />
              <el-input-number v-model="lockQuery.materialId" :min="1" placeholder="物料 ID" />
              <el-select v-model="lockQuery.status" clearable placeholder="锁定状态"
                ><el-option label="锁定中" value="locked" /><el-option
                  label="已释放"
                  value="cancelled" /><el-option label="已消耗" value="consumed"
              /></el-select>
              <el-button :icon="Search" type="primary" @click="searchLocks">查询</el-button
              ><el-button @click="resetLockQuery">重置</el-button>
            </div>
            <el-button :icon="Lock" type="primary" @click="openLockDialog">锁定库存</el-button>
          </div>
          <el-alert
            v-if="lockError"
            class="list-error"
            :closable="false"
            :title="lockError"
            type="error"
            ><template #default
              ><el-button link type="primary" @click="loadLocks">重试</el-button></template
            ></el-alert
          >
          <div v-loading="lockLoading" class="table-area">
            <EmptyState
              v-if="!lockLoading && !lockError && !lockItems.length"
              description="当前筛选条件下没有库存锁定记录。"
            />
            <el-table v-else :data="lockItems" stripe>
              <el-table-column label="锁定 ID" min-width="90" prop="lockId" /><el-table-column
                label="生产订单"
                min-width="110"
                ><template #default="{ row }">#{{ row.orderId }}</template></el-table-column
              >
              <el-table-column label="物料" min-width="190"
                ><template #default="{ row }"
                  ><div class="primary-cell">
                    <strong>{{ row.materialName || `物料 #${row.materialId}` }}</strong
                    ><small>ID {{ row.materialId }}</small>
                  </div></template
                ></el-table-column
              >
              <el-table-column label="锁定数量" min-width="110"
                ><template #default="{ row }">{{
                  formatNumber(row.lockQty)
                }}</template></el-table-column
              >
              <el-table-column label="锁定时间" min-width="170"
                ><template #default="{ row }">{{
                  formatDateTime(row.lockTime)
                }}</template></el-table-column
              >
              <el-table-column label="状态" min-width="100"
                ><template #default="{ row }"
                  ><StatusTag :labels="statusLabels" :value="row.status" /></template
              ></el-table-column>
              <el-table-column fixed="right" label="操作" min-width="100"
                ><template #default="{ row }"
                  ><el-button
                    v-if="row.status === 'locked'"
                    :disabled="releasingLockId !== undefined"
                    link
                    :loading="releasingLockId === row.lockId"
                    type="danger"
                    @click="releaseLock(row)"
                    >释放</el-button
                  ><span v-else>-</span></template
                ></el-table-column
              >
            </el-table>
          </div>
          <el-pagination
            v-if="lockTotal"
            v-model:current-page="lockQuery.page"
            v-model:page-size="lockQuery.pageSize"
            :page-sizes="[10, 20, 50]"
            background
            layout="total, sizes, prev, pager, next"
            :total="lockTotal"
            @change="loadLocks"
          />
        </el-tab-pane>

        <el-tab-pane name="obsolete">
          <template #label
            ><span class="tab-label"
              ><el-icon><Warning /></el-icon>呆滞物料</span
            ></template
          >
          <div class="toolbar">
            <div class="filters">
              <el-input-number v-model="obsoleteQuery.materialId" :min="1" placeholder="物料 ID" />
              <el-select v-model="obsoleteQuery.status" clearable placeholder="处理状态"
                ><el-option label="待处理" value="pending" /><el-option
                  label="已处理"
                  value="handled" /><el-option label="已忽略" value="ignored"
              /></el-select>
              <el-date-picker
                v-model="obsoleteDateRange"
                end-placeholder="结束日期"
                range-separator="至"
                start-placeholder="开始日期"
                type="daterange"
                value-format="YYYY-MM-DD"
              />
              <el-button :icon="Search" type="primary" @click="searchObsolete">查询</el-button
              ><el-button @click="resetObsoleteQuery">重置</el-button>
            </div>
            <el-button :icon="Warning" type="primary" @click="detectDialogOpen = true"
              >执行检测</el-button
            >
          </div>
          <el-alert
            v-if="obsoleteError"
            class="list-error"
            :closable="false"
            :title="obsoleteError"
            type="error"
            ><template #default
              ><el-button link type="primary" @click="loadObsolete">重试</el-button></template
            ></el-alert
          >
          <div v-loading="obsoleteLoading" class="table-area">
            <EmptyState
              v-if="!obsoleteLoading && !obsoleteError && !obsoleteItems.length"
              description="当前筛选条件下没有呆滞物料记录。"
            />
            <el-table v-else :data="obsoleteItems" stripe>
              <el-table-column label="检测 ID" min-width="90" prop="detectionId" />
              <el-table-column label="物料" min-width="190"
                ><template #default="{ row }"
                  ><div class="primary-cell">
                    <strong>{{ row.materialName || `物料 #${row.materialId}` }}</strong
                    ><small>ID {{ row.materialId }}</small>
                  </div></template
                ></el-table-column
              >
              <el-table-column label="可用数量" min-width="110"
                ><template #default="{ row }">{{
                  formatNumber(row.availableQty)
                }}</template></el-table-column
              ><el-table-column label="闲置天数" min-width="100" prop="idleDays" /><el-table-column
                label="最后出库"
                min-width="120"
                prop="lastOutDate"
              />
              <el-table-column label="检测时间" min-width="170"
                ><template #default="{ row }">{{
                  formatDateTime(row.detectTime)
                }}</template></el-table-column
              >
              <el-table-column label="状态" min-width="100"
                ><template #default="{ row }"
                  ><StatusTag :labels="statusLabels" :value="row.status" /></template
              ></el-table-column>
              <el-table-column fixed="right" label="操作" min-width="150"
                ><template #default="{ row }"
                  ><template v-if="row.status === 'pending'"
                    ><el-button
                      :disabled="handlingObsolete !== undefined"
                      link
                      :loading="
                        handlingObsolete?.detectionId === row.detectionId &&
                        handlingObsolete?.status === 'handled'
                      "
                      type="primary"
                      @click="handleObsolete(row, 'handled')"
                      >完成</el-button
                    ><el-button
                      :disabled="handlingObsolete !== undefined"
                      link
                      :loading="
                        handlingObsolete?.detectionId === row.detectionId &&
                        handlingObsolete?.status === 'ignored'
                      "
                      type="danger"
                      @click="handleObsolete(row, 'ignored')"
                      >忽略</el-button
                    ></template
                  ><span v-else>-</span></template
                ></el-table-column
              >
            </el-table>
          </div>
          <el-pagination
            v-if="obsoleteTotal"
            v-model:current-page="obsoleteQuery.page"
            v-model:page-size="obsoleteQuery.pageSize"
            :page-sizes="[10, 20, 50]"
            background
            layout="total, sizes, prev, pager, next"
            :total="obsoleteTotal"
            @change="loadObsolete"
          />
        </el-tab-pane>
      </el-tabs>
    </el-card>

    <el-dialog v-model="generateDialogOpen" title="生成库存预警" width="min(92vw, 430px)"
      ><el-form label-width="90px"
        ><el-form-item label="物料 ID"
          ><el-input-number
            v-model="generateMaterialId"
            :min="1"
            placeholder="留空则扫描全部"
            style="width: 100%" /></el-form-item></el-form
      ><template #footer
        ><el-button @click="generateDialogOpen = false">取消</el-button
        ><el-button :loading="generatingAlerts" type="primary" @click="generateAlerts"
          >开始生成</el-button
        ></template
      ></el-dialog
    >

    <el-dialog
      v-model="lockDialogOpen"
      title="锁定生产订单库存"
      width="min(94vw, 680px)"
      @closed="lockFormRef?.resetFields()"
      ><el-form ref="lockFormRef" :model="lockForm" :rules="lockRules" label-width="100px"
        ><el-form-item label="生产订单" prop="orderId"
          ><el-input-number v-model="lockForm.orderId" :min="1" style="width: 100%" /></el-form-item
        ><el-form-item label="锁定明细"
          ><div class="lock-lines">
            <div v-for="(item, index) in lockForm.items" :key="index" class="lock-line">
              <el-input-number
                v-model="item.materialId"
                :min="1"
                placeholder="物料 ID"
              /><el-input-number
                v-model="item.lockQty"
                :min="0.01"
                :precision="2"
                placeholder="锁定数量"
              /><el-button
                :disabled="lockForm.items.length === 1"
                text
                type="danger"
                @click="removeLockLine(index)"
                >移除</el-button
              >
            </div>
            <el-button :icon="Plus" text type="primary" @click="addLockLine">添加物料</el-button>
          </div></el-form-item
        ></el-form
      ><template #footer
        ><el-button @click="lockDialogOpen = false">取消</el-button
        ><el-button :loading="locking" type="primary" @click="submitLock"
          >确认锁定</el-button
        ></template
      ></el-dialog
    >

    <el-dialog v-model="detectDialogOpen" title="检测呆滞物料" width="min(92vw, 450px)"
      ><el-form label-width="110px"
        ><el-form-item label="闲置天数阈值" required
          ><el-input-number
            v-model="detectionForm.idleDaysThreshold"
            :min="1"
            style="width: 100%" /></el-form-item
        ><el-form-item label="指定物料 ID"
          ><el-input-number
            v-model="detectionForm.materialId"
            :min="1"
            placeholder="留空则扫描全部"
            style="width: 100%" /></el-form-item></el-form
      ><template #footer
        ><el-button @click="detectDialogOpen = false">取消</el-button
        ><el-button :loading="detecting" type="primary" @click="detectObsolete"
          >执行检测</el-button
        ></template
      ></el-dialog
    >
  </PageContainer>
</template>

<style scoped>
.monitor-card {
  min-width: 0;
  overflow: hidden;
  border-top: 3px solid var(--primary-color);
}
.tab-label,
.toolbar,
.filters,
.primary-cell {
  display: flex;
  align-items: center;
  gap: 7px;
}
.toolbar {
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 16px;
}
.filters {
  flex-wrap: wrap;
  min-width: 0;
}
.filters :deep(.el-input-number) {
  width: 135px;
}
.filters :deep(.el-select) {
  width: 135px;
}
.filters :deep(.el-date-editor) {
  width: 250px;
}
.list-error {
  margin-bottom: 12px;
}
.table-area {
  width: 100%;
  min-width: 0;
  min-height: 250px;
}
:deep(.el-card__body),
:deep(.el-tabs__content),
:deep(.el-tab-pane) {
  min-width: 0;
}
.primary-cell {
  align-items: flex-start;
  flex-direction: column;
  gap: 2px;
}
.primary-cell small {
  color: var(--el-text-color-secondary);
}
.risk-value {
  color: var(--primary-color);
}
:deep(.el-pagination) {
  justify-content: flex-end;
  margin-top: 16px;
}
.lock-lines {
  display: grid;
  width: 100%;
  gap: 10px;
}
.lock-line {
  display: grid;
  grid-template-columns: 1fr 1fr auto;
  gap: 8px;
}
@media (max-width: 720px) {
  .toolbar {
    align-items: stretch;
    flex-direction: column;
  }
  .filters > * {
    flex: 1 1 130px;
  }
  .filters :deep(.el-date-editor) {
    width: 100%;
  }
  .lock-line {
    grid-template-columns: 1fr;
  }
}
</style>
