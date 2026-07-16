<script setup lang="ts">
import {
  type LoginResult,
  type PageResult,
  type SystemLoginLog,
  type SystemOperationLog,
  systemService,
} from '@/services/SystemService'
import { Refresh, View } from '@element-plus/icons-vue'
import { computed, onMounted, reactive, ref } from 'vue'
import PageContainer from '@/components/common/PageContainer.vue'
import PageHeader from '@/components/common/PageHeader.vue'
import { formatDateTime } from '@/utils/format'
import { getErrorMessage } from '@/utils/error'
import { useAuthStore } from '@/stores/auth'

const pageSize = 10
const auth = useAuthStore()
const activeTab = ref<'login' | 'operation'>('login')
const loginFilters = reactive({ result: '', timeRange: [] as string[], userId: '' })
const operationFilters = reactive({
  action: '',
  module: '',
  operatorId: '',
  timeRange: [] as string[],
})
const loginPage = ref(1)
const operationPage = ref(1)
const loginLoading = ref(false)
const operationLoading = ref(false)
const loginError = ref('')
const operationError = ref('')
const operationLoaded = ref(false)
const loginResult = ref<PageResult<SystemLoginLog>>({ items: [], page: 1, pageSize, total: 0 })
const operationResult = ref<PageResult<SystemOperationLog>>({
  items: [],
  page: 1,
  pageSize,
  total: 0,
})
const detailDialogVisible = ref(false)
const selectedOperation = ref<SystemOperationLog>()

const canViewAudit = computed(() => auth.hasPermission('system:audit:view'))
const canResolveAuditUserNames = computed(() => auth.hasPermission('system:user:view'))

function selectedUserId(value: string) {
  const userId = Number(value)
  if (Number.isInteger(userId) && userId > 0) {
    return userId
  }
  return undefined
}

function selectedLoginResult(): LoginResult | undefined {
  if (loginFilters.result === 'success' || loginFilters.result === 'failure') {
    return loginFilters.result
  }
  return undefined
}

function getTimeRange(timeRange: string[]) {
  return { endTime: timeRange[1], startTime: timeRange[0] }
}

async function loadLoginLogs(targetPage = loginPage.value) {
  if (!canViewAudit.value) {
    return
  }

  loginLoading.value = true
  loginError.value = ''
  try {
    const { endTime, startTime } = getTimeRange(loginFilters.timeRange)
    loginResult.value = await systemService.listLoginLogs(
      {
        endTime,
        page: targetPage,
        pageSize,
        result: selectedLoginResult(),
        startTime,
        userId: selectedUserId(loginFilters.userId),
      },
      canResolveAuditUserNames.value,
    )
    loginPage.value = loginResult.value.page
  } catch (requestError) {
    loginError.value = getErrorMessage(requestError, '登录日志加载失败')
  } finally {
    loginLoading.value = false
  }
}

async function loadOperationLogs(targetPage = operationPage.value) {
  if (!canViewAudit.value) {
    return
  }

  operationLoaded.value = true
  operationLoading.value = true
  operationError.value = ''
  try {
    const { endTime, startTime } = getTimeRange(operationFilters.timeRange)
    operationResult.value = await systemService.listOperationLogs(
      {
        action: operationFilters.action,
        endTime,
        module: operationFilters.module,
        operatorId: selectedUserId(operationFilters.operatorId),
        page: targetPage,
        pageSize,
        startTime,
      },
      canResolveAuditUserNames.value,
    )
    operationPage.value = operationResult.value.page
  } catch (requestError) {
    operationError.value = getErrorMessage(requestError, '操作日志加载失败')
  } finally {
    operationLoading.value = false
  }
}

function resetLoginFilters() {
  Object.assign(loginFilters, { result: '', timeRange: [], userId: '' })
  void loadLoginLogs(1)
}

function resetOperationFilters() {
  Object.assign(operationFilters, { action: '', module: '', operatorId: '', timeRange: [] })
  void loadOperationLogs(1)
}

function handleTabChange(tabName: string | number) {
  if (tabName === 'operation' && !operationLoaded.value) {
    void loadOperationLogs(1)
  }
}

function openOperationDetail(log: SystemOperationLog) {
  selectedOperation.value = log
  detailDialogVisible.value = true
}

onMounted(() => void loadLoginLogs())
</script>

<template>
  <PageContainer>
    <PageHeader title="系统审计" description="查询用户登录与系统操作记录，日志仅供审计查看。" />

    <el-result
      v-if="!canViewAudit"
      icon="warning"
      sub-title="当前账号没有 system:audit:view 权限。"
      title="无审计日志查看权限"
    />

    <el-tabs v-else v-model="activeTab" class="audit-tabs" @tab-change="handleTabChange">
      <el-tab-pane label="登录日志" name="login">
        <el-card class="audit-search-card" shadow="never">
          <el-form :model="loginFilters" inline @submit.prevent="loadLoginLogs(1)">
            <el-form-item label="用户编号">
              <el-input v-model.trim="loginFilters.userId" clearable placeholder="请输入用户编号" />
            </el-form-item>
            <el-form-item label="登录结果">
              <el-select
                v-model="loginFilters.result"
                clearable
                placeholder="全部"
                style="width: 120px"
              >
                <el-option label="成功" value="success" />
                <el-option label="失败" value="failure" />
              </el-select>
            </el-form-item>
            <el-form-item label="登录时间">
              <el-date-picker
                v-model="loginFilters.timeRange"
                end-placeholder="结束时间"
                range-separator="至"
                start-placeholder="开始时间"
                type="datetimerange"
                value-format="YYYY-MM-DDTHH:mm:ss"
              />
            </el-form-item>
            <el-form-item>
              <el-button type="primary" :loading="loginLoading" @click="loadLoginLogs(1)"
                >查询</el-button
              >
              <el-button :disabled="loginLoading" :icon="Refresh" @click="resetLoginFilters"
                >重置</el-button
              >
            </el-form-item>
          </el-form>
        </el-card>

        <el-card class="audit-table-card" shadow="never">
          <el-alert
            v-if="loginError"
            class="audit-request-error"
            :closable="false"
            show-icon
            :title="loginError"
            type="error"
          >
            <template #default>
              <el-button link type="primary" @click="loadLoginLogs(loginPage)">重新加载</el-button>
            </template>
          </el-alert>
          <el-table
            v-else
            v-loading="loginLoading"
            :data="loginResult.items"
            min-height="320"
            stripe
          >
            <el-table-column label="工号" min-width="120">
              <template #default="{ row }">
                {{ row.employeeNo || (row.userId ? `用户 #${row.userId}` : '-') }}
              </template>
            </el-table-column>
            <el-table-column label="用户姓名" min-width="120">
              <template #default="{ row }">{{ row.userName || '-' }}</template>
            </el-table-column>
            <el-table-column label="登录时间" min-width="175">
              <template #default="{ row }">{{ formatDateTime(row.loginTime) }}</template>
            </el-table-column>
            <el-table-column label="IP 地址" min-width="140">
              <template #default="{ row }">{{ row.ipAddress || '-' }}</template>
            </el-table-column>
            <el-table-column label="登录结果" min-width="100">
              <template #default="{ row }">
                <el-tag :type="row.result === 'success' ? 'success' : 'danger'" effect="light">
                  {{ row.result === 'success' ? '成功' : '失败' }}
                </el-tag>
              </template>
            </el-table-column>
            <el-table-column label="失败原因" min-width="200" show-overflow-tooltip>
              <template #default="{ row }">{{ row.failReason || '-' }}</template>
            </el-table-column>
          </el-table>
          <el-empty
            v-if="!loginLoading && !loginError && !loginResult.items.length"
            description="暂无符合条件的登录日志"
          />
          <div v-if="!loginError && loginResult.total > 0" class="audit-pagination">
            <el-pagination
              v-model:current-page="loginPage"
              background
              layout="total, prev, pager, next"
              :page-size="pageSize"
              :total="loginResult.total"
              @current-change="loadLoginLogs"
            />
          </div>
        </el-card>
      </el-tab-pane>

      <el-tab-pane label="操作日志" name="operation">
        <el-card class="audit-search-card" shadow="never">
          <el-form :model="operationFilters" inline @submit.prevent="loadOperationLogs(1)">
            <el-form-item label="操作人编号">
              <el-input
                v-model.trim="operationFilters.operatorId"
                clearable
                placeholder="请输入用户编号"
              />
            </el-form-item>
            <el-form-item label="业务模块">
              <el-input
                v-model.trim="operationFilters.module"
                clearable
                placeholder="支持模糊查询"
              />
            </el-form-item>
            <el-form-item label="操作类型">
              <el-input
                v-model.trim="operationFilters.action"
                clearable
                placeholder="支持模糊查询"
              />
            </el-form-item>
            <el-form-item label="操作时间">
              <el-date-picker
                v-model="operationFilters.timeRange"
                end-placeholder="结束时间"
                range-separator="至"
                start-placeholder="开始时间"
                type="datetimerange"
                value-format="YYYY-MM-DDTHH:mm:ss"
              />
            </el-form-item>
            <el-form-item>
              <el-button type="primary" :loading="operationLoading" @click="loadOperationLogs(1)">
                查询
              </el-button>
              <el-button
                :disabled="operationLoading"
                :icon="Refresh"
                @click="resetOperationFilters"
              >
                重置
              </el-button>
            </el-form-item>
          </el-form>
        </el-card>

        <el-card class="audit-table-card" shadow="never">
          <el-alert
            v-if="operationError"
            class="audit-request-error"
            :closable="false"
            show-icon
            :title="operationError"
            type="error"
          >
            <template #default>
              <el-button link type="primary" @click="loadOperationLogs(operationPage)">
                重新加载
              </el-button>
            </template>
          </el-alert>
          <el-table
            v-else
            v-loading="operationLoading"
            :data="operationResult.items"
            min-height="320"
            stripe
          >
            <el-table-column label="操作人" min-width="140">
              <template #default="{ row }">
                {{ row.operatorName || (row.operatorId ? `用户 #${row.operatorId}` : '-') }}
              </template>
            </el-table-column>
            <el-table-column label="业务模块" min-width="140">
              <template #default="{ row }">{{ row.module || '-' }}</template>
            </el-table-column>
            <el-table-column label="操作类型" min-width="140">
              <template #default="{ row }">{{ row.action || '-' }}</template>
            </el-table-column>
            <el-table-column label="操作时间" min-width="175">
              <template #default="{ row }">{{ formatDateTime(row.operateTime) }}</template>
            </el-table-column>
            <el-table-column label="IP 地址" min-width="140">
              <template #default="{ row }">{{ row.ipAddress || '-' }}</template>
            </el-table-column>
            <el-table-column fixed="right" label="操作" min-width="100">
              <template #default="{ row }">
                <el-button link type="primary" :icon="View" @click="openOperationDetail(row)">
                  查看详情
                </el-button>
              </template>
            </el-table-column>
          </el-table>
          <el-empty
            v-if="!operationLoading && !operationError && !operationResult.items.length"
            description="暂无符合条件的操作日志"
          />
          <div v-if="!operationError && operationResult.total > 0" class="audit-pagination">
            <el-pagination
              v-model:current-page="operationPage"
              background
              layout="total, prev, pager, next"
              :page-size="pageSize"
              :total="operationResult.total"
              @current-change="loadOperationLogs"
            />
          </div>
        </el-card>
      </el-tab-pane>
    </el-tabs>

    <el-dialog v-model="detailDialogVisible" title="操作日志详情" width="760px">
      <template v-if="selectedOperation">
        <el-descriptions :column="2" border class="operation-detail-summary">
          <el-descriptions-item label="操作人">
            {{ selectedOperation.operatorName || selectedOperation.operatorId || '-' }}
          </el-descriptions-item>
          <el-descriptions-item label="操作时间">
            {{ formatDateTime(selectedOperation.operateTime) }}
          </el-descriptions-item>
          <el-descriptions-item label="业务模块">
            {{ selectedOperation.module || '-' }}
          </el-descriptions-item>
          <el-descriptions-item label="操作类型">
            {{ selectedOperation.action || '-' }}
          </el-descriptions-item>
          <el-descriptions-item label="IP 地址" :span="2">
            {{ selectedOperation.ipAddress || '-' }}
          </el-descriptions-item>
        </el-descriptions>
        <div class="operation-snapshots">
          <section class="snapshot-panel">
            <h3>操作前数据</h3>
            <el-empty
              v-if="!selectedOperation.beforeData"
              :image-size="56"
              description="无操作前快照"
            />
            <el-input
              v-else
              :autosize="{ maxRows: 12, minRows: 5 }"
              :model-value="selectedOperation.beforeData"
              readonly
              type="textarea"
            />
          </section>
          <section class="snapshot-panel">
            <h3>操作后数据</h3>
            <el-empty
              v-if="!selectedOperation.afterData"
              :image-size="56"
              description="无操作后快照"
            />
            <el-input
              v-else
              :autosize="{ maxRows: 12, minRows: 5 }"
              :model-value="selectedOperation.afterData"
              readonly
              type="textarea"
            />
          </section>
        </div>
      </template>
      <template #footer>
        <el-button type="primary" @click="detailDialogVisible = false">关闭</el-button>
      </template>
    </el-dialog>
  </PageContainer>
</template>

<style scoped>
.audit-tabs :deep(.el-tabs__header) {
  margin-bottom: 16px;
}

.audit-search-card {
  margin-bottom: 16px;
}

.audit-search-card :deep(.el-card__body) {
  padding-bottom: 2px;
}

.audit-table-card :deep(.el-card__body) {
  padding: 0;
}

.audit-request-error {
  margin: 16px 16px 0;
}

.audit-pagination {
  display: flex;
  justify-content: flex-end;
  padding: 16px 20px;
}

.operation-detail-summary {
  margin-bottom: 20px;
}

.operation-snapshots {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 16px;
}

.snapshot-panel {
  min-width: 0;
}

.snapshot-panel h3 {
  margin: 0 0 10px;
  color: #303133;
  font-size: 15px;
}

.snapshot-panel :deep(textarea) {
  font-family: Consolas, 'Courier New', monospace;
}

@media (max-width: 720px) {
  .audit-pagination {
    justify-content: center;
  }

  .operation-snapshots {
    grid-template-columns: 1fr;
  }
}
</style>
