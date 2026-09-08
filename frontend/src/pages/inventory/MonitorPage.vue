<script setup lang="ts">
import { Bell, Lock, Plus, Refresh, Search, Warning } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import type {
  InventoryAlertItem,
  InventoryAlertQuery,
  InventoryReferenceData,
  ObsoleteMaterialItem,
  ObsoleteMaterialQuery,
  StockLockItem,
  StockLockQuery,
} from '@/types/inventory'
import { inventoryService } from '@/services/InventoryService'
import { computed, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'
import { formatDateTime, formatNumber } from '@/utils/format'
import EmptyState from '@/components/common/EmptyState.vue'
import PageContainer from '@/components/common/PageContainer.vue'
import PageHeader from '@/components/common/PageHeader.vue'
import StatusTag from '@/components/common/StatusTag.vue'
import { getErrorMessage } from '@/utils/error'
import { inventoryMonitorStatusLabels as statusLabels } from '@/constants/status'
import { useAuthStore } from '@/stores/auth'
import { PermissionCode } from '@/constants/permissions'

type MonitorTab = 'alerts' | 'locks' | 'obsolete'

const auth = useAuthStore()
const operatorId = computed(() => auth.currentUser?.id)
const canViewAlerts = computed(() => auth.hasPermission(PermissionCode.InventoryAlertView))
const canGenerateAlerts = computed(() => auth.hasPermission(PermissionCode.InventoryAlertGenerate))
const canHandleAlerts = computed(() => auth.hasPermission(PermissionCode.InventoryAlertHandle))
const canViewLocks = computed(() => auth.hasPermission(PermissionCode.InventoryLockView))
const canViewObsolete = computed(() => auth.hasPermission(PermissionCode.InventoryObsoleteView))
const canDetectObsolete = computed(() => auth.hasPermission(PermissionCode.InventoryObsoleteDetect))
const canHandleObsolete = computed(() => auth.hasPermission(PermissionCode.InventoryObsoleteHandle))
function getInitialTab(): MonitorTab {
  if (canViewAlerts.value) {
    return 'alerts'
  }
  if (canViewLocks.value) {
    return 'locks'
  }
  return 'obsolete'
}
const activeTab = ref<MonitorTab>(getInitialTab())
let alive = true
const referenceLoading = ref(false)
const referenceError = ref('')
const referenceData = ref<InventoryReferenceData>({
  bomVersions: [],
  materials: [],
  productionOrders: [],
})

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
const alertDetail = ref<Awaited<ReturnType<typeof inventoryService.getAlertDetail>>>()
const alertDetailError = ref('')
const alertDetailLoading = ref(false)
let alertRequestId = 0

// 库存锁定
const lockLoading = ref(false)
const lockError = ref('')
const lockItems = ref<StockLockItem[]>([])
const lockTotal = ref(0)
const lockQuery = reactive<StockLockQuery>({ page: 1, pageSize: 10 })
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
const obsoleteDetail = ref<Awaited<ReturnType<typeof inventoryService.getObsoleteDetail>>>()
const obsoleteDetailError = ref('')
const obsoleteDetailLoading = ref(false)
const detectionForm = reactive({
  idleDaysThreshold: 90,
  materialId: undefined as number | undefined,
})
let obsoleteRequestId = 0

async function loadReferenceData() {
  if (!canGenerateAlerts.value && !canDetectObsolete.value) {
    return
  }
  referenceLoading.value = true
  referenceError.value = ''
  try {
    const references = await inventoryService.getReferenceData()
    if (!alive) {
      return
    }
    referenceData.value = references
  } catch (error) {
    if (alive) {
      referenceError.value = getErrorMessage(error, '库存操作选项加载失败')
    }
  } finally {
    if (alive) {
      referenceLoading.value = false
    }
  }
}

async function loadAlerts() {
  if (!canViewAlerts.value) {
    return
  }
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
  if (!canViewLocks.value) {
    return
  }
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
  if (!canViewObsolete.value) {
    return
  }
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

async function viewAlertDetail(alertId: number) {
  alertDetail.value = undefined
  alertDetailError.value = ''
  alertDetailLoading.value = true
  try {
    alertDetail.value = await inventoryService.getAlertDetail(alertId)
  } catch (error) {
    alertDetailError.value = getErrorMessage(error, '库存预警详情加载失败')
  } finally {
    alertDetailLoading.value = false
  }
}

async function viewObsoleteDetail(detectionId: number) {
  obsoleteDetail.value = undefined
  obsoleteDetailError.value = ''
  obsoleteDetailLoading.value = true
  try {
    obsoleteDetail.value = await inventoryService.getObsoleteDetail(detectionId)
  } catch (error) {
    obsoleteDetailError.value = getErrorMessage(error, '呆滞物料详情加载失败')
  } finally {
    obsoleteDetailLoading.value = false
  }
}

function closeAlertDetail() {
  alertDetail.value = undefined
  alertDetailError.value = ''
}

function closeObsoleteDetail() {
  obsoleteDetail.value = undefined
  obsoleteDetailError.value = ''
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
onMounted(() => {
  loadActiveTab()
  void loadReferenceData()
})
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

    <el-alert
      v-if="referenceError"
      class="list-error"
      :closable="false"
      show-icon
      :title="referenceError"
      type="error"
    >
      <template #default>
        <el-button link type="primary" @click="loadReferenceData">重新加载操作选项</el-button>
      </template>
    </el-alert>

    <el-card class="monitor-card table-card" shadow="never">
      <el-tabs v-model="activeTab">
        <el-tab-pane v-if="canViewAlerts" name="alerts">
          <template #label
            ><span class="tab-label"
              ><el-icon><Bell /></el-icon>库存预警</span
            ></template
          >
          <div class="toolbar">
            <div class="filters">
              <el-input-number
                :controls="false"
                v-model="alertQuery.materialId"
                :min="1"
                placeholder="物料 ID"
              />
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
            <el-button
              v-if="canGenerateAlerts"
              :icon="Plus"
              type="primary"
              @click="generateDialogOpen = true"
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
              <el-table-column fixed="right" label="操作" min-width="190"
                ><template #default="{ row }"
                  ><el-button link type="primary" @click="viewAlertDetail(row.alertId)"
                    >详情</el-button
                  ><template v-if="canHandleAlerts && row.status === 'pending'"
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
                  ></template
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

        <el-tab-pane v-if="canViewLocks" name="locks">
          <template #label
            ><span class="tab-label"
              ><el-icon><Lock /></el-icon>库存锁定</span
            ></template
          >
          <div class="toolbar">
            <div class="filters">
              <el-input-number
                :controls="false"
                v-model="lockQuery.orderId"
                :min="1"
                placeholder="生产订单 ID"
              />
              <el-input-number
                :controls="false"
                v-model="lockQuery.materialId"
                :min="1"
                placeholder="物料 ID"
              />
              <el-select v-model="lockQuery.status" clearable placeholder="锁定状态"
                ><el-option label="锁定中" value="locked" /><el-option
                  label="已释放"
                  value="cancelled" /><el-option label="已消耗" value="consumed"
              /></el-select>
              <el-button :icon="Search" type="primary" @click="searchLocks">查询</el-button
              ><el-button @click="resetLockQuery">重置</el-button>
            </div>
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
                label="生产订单 ID"
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

        <el-tab-pane v-if="canViewObsolete" name="obsolete">
          <template #label
            ><span class="tab-label"
              ><el-icon><Warning /></el-icon>呆滞物料</span
            ></template
          >
          <div class="toolbar">
            <div class="filters">
              <el-input-number
                :controls="false"
                v-model="obsoleteQuery.materialId"
                :min="1"
                placeholder="物料 ID"
              />
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
            <el-button
              v-if="canDetectObsolete"
              :icon="Warning"
              type="primary"
              @click="detectDialogOpen = true"
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
              <el-table-column fixed="right" label="操作" min-width="190"
                ><template #default="{ row }"
                  ><el-button link type="primary" @click="viewObsoleteDetail(row.detectionId)"
                    >详情</el-button
                  ><template v-if="canHandleObsolete && row.status === 'pending'"
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
                  ></template
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
        ><el-form-item label="物料"
          ><el-input-number
            :controls="false"
            v-if="!referenceData.materials.length"
            v-model="generateMaterialId"
            :min="1"
            :precision="0"
            placeholder="物料编号，留空扫描全部" /><el-select
            v-else
            v-model="generateMaterialId"
            clearable
            filterable
            :loading="referenceLoading"
            placeholder="留空则扫描全部"
            style="width: 100%"
            ><el-option
              v-for="material in referenceData.materials"
              :key="material.materialId"
              :label="'#' + material.materialId + ' · ' + material.materialName"
              :value="material.materialId" /></el-select></el-form-item></el-form
      ><template #footer
        ><el-button @click="generateDialogOpen = false">取消</el-button
        ><el-button :loading="generatingAlerts" type="primary" @click="generateAlerts"
          >开始生成</el-button
        ></template
      ></el-dialog
    >

    <el-dialog v-model="detectDialogOpen" title="检测呆滞物料" width="min(92vw, 450px)"
      ><el-form label-width="110px"
        ><el-form-item label="闲置天数阈值" required
          ><el-input-number
            :controls="false"
            v-model="detectionForm.idleDaysThreshold"
            :min="1"
            style="width: 100%" /></el-form-item
        ><el-form-item label="指定物料"
          ><el-input-number
            :controls="false"
            v-if="!referenceData.materials.length"
            v-model="detectionForm.materialId"
            :min="1"
            :precision="0"
            placeholder="物料编号，留空扫描全部" /><el-select
            v-else
            v-model="detectionForm.materialId"
            clearable
            filterable
            :loading="referenceLoading"
            placeholder="留空则扫描全部"
            style="width: 100%"
            ><el-option
              v-for="material in referenceData.materials"
              :key="material.materialId"
              :label="'#' + material.materialId + ' · ' + material.materialName"
              :value="material.materialId" /></el-select></el-form-item></el-form
      ><template #footer
        ><el-button @click="detectDialogOpen = false">取消</el-button
        ><el-button :loading="detecting" type="primary" @click="detectObsolete"
          >执行检测</el-button
        ></template
      ></el-dialog
    >

    <el-drawer
      :model-value="alertDetailLoading || Boolean(alertDetail) || Boolean(alertDetailError)"
      size="min(92vw, 520px)"
      title="库存预警详情"
      @close="closeAlertDetail"
    >
      <div v-loading="alertDetailLoading" class="detail-area">
        <el-alert
          v-if="alertDetailError"
          :closable="false"
          show-icon
          :title="alertDetailError"
          type="error"
        />
        <div v-else-if="alertDetail" class="detail-grid">
          <div>
            <span>预警编号</span><strong>#{{ alertDetail.alertId }}</strong>
          </div>
          <div>
            <span>物料</span><strong>{{ alertDetail.materialName }}</strong>
          </div>
          <div>
            <span>可用库存</span><strong>{{ formatNumber(alertDetail.availableQty) }}</strong>
          </div>
          <div>
            <span>安全阈值</span><strong>{{ formatNumber(alertDetail.threshold) }}</strong>
          </div>
          <div>
            <span>触发时间</span><strong>{{ formatDateTime(alertDetail.alertTime) }}</strong>
          </div>
          <div>
            <span>状态</span><StatusTag :labels="statusLabels" :value="alertDetail.status" />
          </div>
          <div>
            <span>处理人</span
            ><strong>{{ alertDetail.handlerId ? `#${alertDetail.handlerId}` : '-' }}</strong>
          </div>
          <div>
            <span>处理时间</span><strong>{{ formatDateTime(alertDetail.handleTime) }}</strong>
          </div>
          <div>
            <span>锁定数量</span><strong>{{ formatNumber(alertDetail.stock.lockedQty) }}</strong>
          </div>
          <div>
            <span>库存状态</span
            ><StatusTag :labels="statusLabels" :value="alertDetail.stock.status" />
          </div>
          <div class="detail-grid__wide">
            <span>建议处理方式</span><strong>{{ alertDetail.recommendedAction }}</strong>
          </div>
        </div>
      </div>
    </el-drawer>

    <el-drawer
      :model-value="
        obsoleteDetailLoading || Boolean(obsoleteDetail) || Boolean(obsoleteDetailError)
      "
      size="min(92vw, 520px)"
      title="呆滞物料详情"
      @close="closeObsoleteDetail"
    >
      <div v-loading="obsoleteDetailLoading" class="detail-area">
        <el-alert
          v-if="obsoleteDetailError"
          :closable="false"
          show-icon
          :title="obsoleteDetailError"
          type="error"
        />
        <div v-else-if="obsoleteDetail" class="detail-grid">
          <div>
            <span>检测编号</span><strong>#{{ obsoleteDetail.detectionId }}</strong>
          </div>
          <div>
            <span>物料</span><strong>{{ obsoleteDetail.materialName }}</strong>
          </div>
          <div>
            <span>可用库存</span><strong>{{ formatNumber(obsoleteDetail.availableQty) }}</strong>
          </div>
          <div>
            <span>闲置天数</span><strong>{{ obsoleteDetail.idleDays }} 天</strong>
          </div>
          <div>
            <span>最后出库</span><strong>{{ obsoleteDetail.lastOutDate || '-' }}</strong>
          </div>
          <div>
            <span>检测时间</span><strong>{{ formatDateTime(obsoleteDetail.detectTime) }}</strong>
          </div>
          <div>
            <span>锁定数量</span><strong>{{ formatNumber(obsoleteDetail.stock.lockedQty) }}</strong>
          </div>
          <div>
            <span>库存状态</span
            ><StatusTag :labels="statusLabels" :value="obsoleteDetail.stock.status" />
          </div>
        </div>
      </div>
    </el-drawer>
  </PageContainer>
</template>

<style scoped>
.monitor-card {
  min-width: 0;
  overflow: hidden;
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
.detail-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 12px;
}
.detail-area {
  min-height: 180px;
}
.detail-grid > div {
  display: grid;
  gap: 4px;
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 6px;
  padding: 12px;
}
.detail-grid span {
  color: var(--el-text-color-secondary);
  font-size: 12px;
}
.detail-grid__wide {
  grid-column: 1 / -1;
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
  .detail-grid {
    grid-template-columns: 1fr;
  }
}
</style>
