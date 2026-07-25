<script setup lang="ts">
import {
  CollectionTag,
  DocumentChecked,
  Refresh,
  Search,
  Tickets,
  View,
} from '@element-plus/icons-vue'
import type {
  MaterialBomDetail,
  MaterialBomListItem,
  MaterialBomStatus,
  MaterialBomSummary,
} from '@/types/material'
import { computed, onMounted, onUnmounted, reactive, ref } from 'vue'
import { formatDateTime, formatNumber } from '@/utils/format'
import EmptyState from '@/components/common/EmptyState.vue'
import PageContainer from '@/components/common/PageContainer.vue'
import PageHeader from '@/components/common/PageHeader.vue'
import type { PageResult } from '@/services/pagination'
import { getErrorMessage } from '@/utils/error'
import { materialService } from '@/services/MaterialService'

type TagType = 'danger' | 'info' | 'primary' | 'success' | 'warning'

const pageSize = 8
const filters = reactive({ keyword: '', owner: '', status: '' })
const page = ref(1)
const loading = ref(false)
const error = ref('')
const summary = ref<MaterialBomSummary>({
  activeCount: 0,
  archivedCount: 0,
  draftCount: 0,
  releasedCount: 0,
})
const result = ref<PageResult<MaterialBomListItem>>({
  items: [],
  page: 1,
  pageSize,
  total: 0,
})
const detailDrawerVisible = ref(false)
const detailLoading = ref(false)
const detailError = ref('')
const currentDetail = ref<MaterialBomDetail>()
const detailTarget = ref<MaterialBomListItem>()
let isUnmounted = false
let listRequestId = 0
let detailRequestId = 0

const statusLabels: Record<MaterialBomStatus, string> = {
  archived: '已归档',
  draft: '草稿',
  released: '已发布',
}

const statusTone: Record<MaterialBomStatus, TagType> = {
  archived: 'info',
  draft: 'warning',
  released: 'success',
}

const componentTypeLabels = {
  material: '原材料',
  semiFinished: '半成品',
} satisfies Record<MaterialBomDetail['components'][number]['type'], string>

const summaryCards = computed(() => [
  {
    description: '草稿和已发布版本',
    icon: CollectionTag,
    title: '有效 BOM',
    value: summary.value.activeCount,
  },
  {
    description: '可用于排产与缺料计算',
    icon: DocumentChecked,
    title: '已发布',
    value: summary.value.releasedCount,
  },
  {
    description: '待评审或试制版本',
    icon: Tickets,
    title: '草稿',
    value: summary.value.draftCount,
  },
  {
    description: '历史停用版本',
    icon: Refresh,
    title: '已归档',
    value: summary.value.archivedCount,
  },
])

function selectedStatus(): MaterialBomStatus | undefined {
  if (
    filters.status === 'archived' ||
    filters.status === 'draft' ||
    filters.status === 'released'
  ) {
    return filters.status
  }
  return undefined
}

async function loadBomData(targetPage = page.value) {
  const currentRequestId = ++listRequestId
  loading.value = true
  error.value = ''
  try {
    const [summaryData, listData] = await Promise.all([
      materialService.getBomSummary(),
      materialService.listBomRecords({
        keyword: filters.keyword,
        owner: filters.owner,
        page: targetPage,
        pageSize,
        status: selectedStatus(),
      }),
    ])
    if (!isUnmounted && currentRequestId === listRequestId) {
      summary.value = summaryData
      result.value = listData
      page.value = listData.page
    }
  } catch (requestError) {
    if (!isUnmounted && currentRequestId === listRequestId) {
      error.value = getErrorMessage(requestError, '物料 BOM 数据加载失败')
    }
  } finally {
    if (!isUnmounted && currentRequestId === listRequestId) {
      loading.value = false
    }
  }
}

function resetFilters() {
  Object.assign(filters, { keyword: '', owner: '', status: '' })
  void loadBomData(1)
}

function getComponentTypeLabel(type: MaterialBomDetail['components'][number]['type']) {
  return componentTypeLabels[type]
}

async function openDetail(record: MaterialBomListItem) {
  const currentRequestId = ++detailRequestId
  detailDrawerVisible.value = true
  detailLoading.value = true
  detailError.value = ''
  currentDetail.value = undefined
  detailTarget.value = record
  try {
    const detail = await materialService.getBomDetail(record.bomId)
    if (!isUnmounted && currentRequestId === detailRequestId) {
      currentDetail.value = detail
    }
  } catch (requestError) {
    if (!isUnmounted && currentRequestId === detailRequestId) {
      detailError.value = getErrorMessage(requestError, 'BOM 明细加载失败')
    }
  } finally {
    if (!isUnmounted && currentRequestId === detailRequestId) {
      detailLoading.value = false
    }
  }
}

function retryDetail() {
  if (detailTarget.value) {
    void openDetail(detailTarget.value)
  }
}

onMounted(() => void loadBomData())
onUnmounted(() => {
  isUnmounted = true
})
</script>

<template>
  <PageContainer>
    <PageHeader
      title="物料 BOM"
      description="维护成品与半成品的 BOM 版本、组件清单和标准用量，支撑排产和缺料计算。"
    >
      <template #actions>
        <el-button :icon="Refresh" :loading="loading" @click="loadBomData(page)">刷新</el-button>
      </template>
    </PageHeader>

    <section class="bom-summary-grid" aria-label="物料 BOM 汇总">
      <el-card
        v-for="card in summaryCards"
        :key="card.title"
        class="bom-summary-card"
        shadow="never"
      >
        <div class="bom-summary-card__content">
          <div>
            <p>{{ card.title }}</p>
            <strong>{{ formatNumber(card.value, 0) }}</strong>
            <span>{{ card.description }}</span>
          </div>
          <el-icon :size="32">
            <component :is="card.icon" />
          </el-icon>
        </div>
      </el-card>
    </section>

    <el-card class="bom-search-card" shadow="never">
      <el-form :model="filters" inline @submit.prevent="loadBomData(1)">
        <el-form-item label="关键字">
          <el-input
            v-model.trim="filters.keyword"
            clearable
            placeholder="BOM 编号 / 物料编码 / 名称"
            style="width: 260px"
          />
        </el-form-item>
        <el-form-item label="状态">
          <el-select v-model="filters.status" clearable placeholder="全部" style="width: 132px">
            <el-option label="已发布" value="released" />
            <el-option label="草稿" value="draft" />
            <el-option label="已归档" value="archived" />
          </el-select>
        </el-form-item>
        <el-form-item label="维护人">
          <el-input
            v-model.trim="filters.owner"
            clearable
            placeholder="支持模糊查询"
            style="width: 180px"
          />
        </el-form-item>
        <el-form-item>
          <el-button :icon="Search" :loading="loading" type="primary" @click="loadBomData(1)">
            查询
          </el-button>
          <el-button :disabled="loading" :icon="Refresh" @click="resetFilters">重置</el-button>
        </el-form-item>
      </el-form>
    </el-card>

    <el-card class="bom-table-card" shadow="never">
      <el-alert
        v-if="error"
        class="bom-request-error"
        :closable="false"
        show-icon
        :title="error"
        type="error"
      >
        <template #default>
          <el-button link type="primary" @click="loadBomData(page)">重新加载</el-button>
        </template>
      </el-alert>

      <el-table v-else v-loading="loading" :data="result.items" min-height="360" stripe>
        <el-table-column label="BOM 编号" min-width="170" prop="bomCode" />
        <el-table-column label="成品/半成品" min-width="210">
          <template #default="{ row }">
            <div class="material-cell">
              <strong>{{ row.materialName }}</strong>
              <span>{{ row.materialCode }}</span>
            </div>
          </template>
        </el-table-column>
        <el-table-column label="版本" min-width="88" prop="version" />
        <el-table-column label="状态" min-width="96">
          <template #default="{ row }">
            <el-tag effect="light" :type="statusTone[row.status as MaterialBomStatus]">
              {{ statusLabels[row.status as MaterialBomStatus] }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="生效日期" min-width="116" prop="effectiveDate" />
        <el-table-column label="组件数" min-width="90">
          <template #default="{ row }">{{ formatNumber(row.componentCount, 0) }}</template>
        </el-table-column>
        <el-table-column label="标准用量" min-width="112">
          <template #default="{ row }">
            {{ formatNumber(row.totalQuantity) }} {{ row.unit }}
          </template>
        </el-table-column>
        <el-table-column label="综合损耗" min-width="102">
          <template #default="{ row }">{{ formatNumber(row.totalLossRate) }}%</template>
        </el-table-column>
        <el-table-column label="维护人" min-width="110" prop="owner" />
        <el-table-column label="更新时间" min-width="168">
          <template #default="{ row }">{{ formatDateTime(row.updatedAt) }}</template>
        </el-table-column>
        <el-table-column fixed="right" label="操作" min-width="96">
          <template #default="{ row }">
            <el-button link type="primary" :icon="View" @click="openDetail(row)">查看</el-button>
          </template>
        </el-table-column>
      </el-table>

      <EmptyState
        v-if="!loading && !error && !result.items.length"
        description="调整关键字、状态或维护人后重试。"
        title="暂无符合条件的 BOM"
      />

      <div v-if="!error && result.total > 0" class="bom-pagination">
        <el-pagination
          v-model:current-page="page"
          background
          layout="total, prev, pager, next"
          :page-size="pageSize"
          :total="result.total"
          @current-change="loadBomData"
        />
      </div>
    </el-card>

    <el-drawer v-model="detailDrawerVisible" size="760px" title="BOM 明细">
      <el-skeleton v-if="detailLoading" :rows="8" animated />
      <el-alert
        v-else-if="detailError"
        :closable="false"
        show-icon
        :title="detailError"
        type="error"
      >
        <template #default>
          <el-button link type="primary" @click="retryDetail">重新加载</el-button>
        </template>
      </el-alert>
      <template v-else-if="currentDetail">
        <div class="bom-detail-heading">
          <div>
            <h2>{{ currentDetail.materialName }}</h2>
            <p>{{ currentDetail.description }}</p>
          </div>
          <el-tag effect="light" :type="statusTone[currentDetail.status]">
            {{ statusLabels[currentDetail.status] }}
          </el-tag>
        </div>

        <el-descriptions border :column="2" class="bom-detail-meta">
          <el-descriptions-item label="BOM 编号">{{ currentDetail.bomCode }}</el-descriptions-item>
          <el-descriptions-item label="物料编码">
            {{ currentDetail.materialCode }}
          </el-descriptions-item>
          <el-descriptions-item label="版本">{{ currentDetail.version }}</el-descriptions-item>
          <el-descriptions-item label="生效日期">
            {{ currentDetail.effectiveDate }}
          </el-descriptions-item>
          <el-descriptions-item label="标准用量">
            {{ formatNumber(currentDetail.totalQuantity) }} {{ currentDetail.unit }}
          </el-descriptions-item>
          <el-descriptions-item label="综合损耗">
            {{ formatNumber(currentDetail.totalLossRate) }}%
          </el-descriptions-item>
          <el-descriptions-item label="维护人">{{ currentDetail.owner }}</el-descriptions-item>
          <el-descriptions-item label="更新时间">
            {{ formatDateTime(currentDetail.updatedAt) }}
          </el-descriptions-item>
        </el-descriptions>

        <section class="bom-detail-section">
          <h3>组件清单</h3>
          <el-table :data="currentDetail.components" stripe>
            <el-table-column label="行号" min-width="72" prop="lineNo" />
            <el-table-column label="组件物料" min-width="210">
              <template #default="{ row }">
                <div class="material-cell">
                  <strong>{{ row.materialName }}</strong>
                  <span>{{ row.materialCode }}</span>
                </div>
              </template>
            </el-table-column>
            <el-table-column label="类型" min-width="90">
              <template #default="{ row }">{{ getComponentTypeLabel(row.type) }}</template>
            </el-table-column>
            <el-table-column label="用量" min-width="100">
              <template #default="{ row }"
                >{{ formatNumber(row.quantity) }} {{ row.unit }}</template
              >
            </el-table-column>
            <el-table-column label="损耗" min-width="86">
              <template #default="{ row }">{{ formatNumber(row.lossRate) }}%</template>
            </el-table-column>
            <el-table-column label="工作中心" min-width="110" prop="workCenter" />
            <el-table-column label="提前期" min-width="88">
              <template #default="{ row }">{{ row.leadTimeDays }} 天</template>
            </el-table-column>
            <el-table-column label="替代组" min-width="90">
              <template #default="{ row }">{{ row.substituteGroup || '-' }}</template>
            </el-table-column>
          </el-table>
          <EmptyState
            v-if="!currentDetail.components.length"
            description="当前 BOM 暂未维护组件。"
            title="暂无组件"
          />
        </section>

        <section class="bom-detail-section">
          <h3>版本记录</h3>
          <el-timeline>
            <el-timeline-item
              v-for="audit in currentDetail.audits"
              :key="`${audit.operatedAt}-${audit.action}`"
              :timestamp="formatDateTime(audit.operatedAt)"
              type="primary"
            >
              {{ audit.operator }} · {{ audit.action }}
            </el-timeline-item>
          </el-timeline>
        </section>
      </template>
    </el-drawer>
  </PageContainer>
</template>

<style scoped>
.bom-summary-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 16px;
  margin-bottom: 16px;
}

.bom-summary-card {
  min-width: 0;
}

.bom-summary-card__content {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.bom-summary-card__content p,
.bom-summary-card__content span {
  margin: 0;
  color: var(--el-text-color-secondary);
}

.bom-summary-card__content strong {
  display: block;
  margin: 8px 0;
  color: var(--el-text-color-primary);
  font-size: 28px;
  line-height: 1;
}

.bom-summary-card__content span {
  font-size: 13px;
}

.bom-summary-card__content .el-icon {
  color: var(--el-color-primary-light-3);
}

.bom-search-card {
  margin-bottom: 16px;
}

.bom-search-card :deep(.el-card__body) {
  padding-bottom: 2px;
}

.bom-table-card {
  min-width: 0;
}

.bom-table-card :deep(.el-card__body) {
  padding: 0;
}

.bom-request-error {
  margin: 16px 16px 0;
}

.material-cell {
  display: grid;
  gap: 4px;
  min-width: 0;
}

.material-cell strong {
  overflow-wrap: anywhere;
}

.material-cell span {
  color: var(--el-text-color-secondary);
  font-size: 12px;
}

.bom-pagination {
  display: flex;
  justify-content: flex-end;
  padding: 16px 20px;
}

.bom-detail-heading {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
  margin-bottom: 18px;
}

.bom-detail-heading h2 {
  margin: 0;
  color: var(--el-text-color-primary);
  font-size: 20px;
}

.bom-detail-heading p {
  margin: 8px 0 0;
  color: var(--el-text-color-secondary);
  line-height: 1.6;
}

.bom-detail-meta {
  margin-bottom: 20px;
}

.bom-detail-section {
  margin-top: 22px;
}

.bom-detail-section h3 {
  margin: 0 0 12px;
  color: var(--el-text-color-primary);
  font-size: 16px;
}

@media (max-width: 1080px) {
  .bom-summary-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 720px) {
  .bom-summary-grid {
    grid-template-columns: 1fr;
  }

  .bom-pagination {
    justify-content: center;
  }

  .bom-detail-heading {
    flex-direction: column;
  }
}
</style>
