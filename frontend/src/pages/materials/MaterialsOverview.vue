<script setup lang="ts">
import type {
  BomAnalysisRecord,
  BomAnalysisResult,
  BomComponentForm,
  BomReverseTraceResult,
  BomTreeNode,
  MaterialBomDetail,
  MaterialBomListItem,
  MaterialBomStatus,
  MaterialCategory,
  MaterialForm,
  MaterialListQuery,
  MaterialRecord,
  MaterialStatus,
  MaterialType,
} from '@/types/material'
import { EditPen, Plus, Refresh, Search, View } from '@element-plus/icons-vue'
import { computed, onMounted, reactive, ref } from 'vue'
import { formatDateTime, formatNumber } from '@/utils/format'
import { ElMessage } from 'element-plus'
import EmptyState from '@/components/common/EmptyState.vue'
import PageContainer from '@/components/common/PageContainer.vue'
import PageHeader from '@/components/common/PageHeader.vue'
import type { PageResult } from '@/services/pagination'
import { getErrorMessage } from '@/utils/error'
import { materialService } from '@/services/MaterialService'
import { useAuthStore } from '@/stores/auth'

type TagType = 'danger' | 'info' | 'primary' | 'success' | 'warning'

const auth = useAuthStore()
const activeTab = ref('materials')
const loading = ref(false)
const error = ref('')
const categories = ref<MaterialCategory[]>([])
const materialOptions = ref<MaterialRecord[]>([])
const bomOptions = ref<MaterialBomListItem[]>([])
const materialPage = ref(1)
const materialPageSize = ref(10)
const materialFilters = reactive({
  categoryId: '',
  createdRange: [] as string[],
  keyword: '',
  status: '',
  type: '',
})
const materialResult = ref<PageResult<MaterialRecord>>({
  items: [],
  page: 1,
  pageSize: 10,
  total: 0,
})
const materialDrawer = ref(false)
const currentMaterial = ref<MaterialRecord>()
const materialDialog = ref(false)
const materialEditingId = ref<string>()
const materialSaving = ref(false)
const materialForm = reactive<MaterialForm>({
  categoryId: '',
  code: '',
  model: '',
  name: '',
  status: 'active',
  type: 'raw',
  unit: '',
})

const bomResult = ref<MaterialBomListItem[]>([])
const selectedBomId = ref('')
const currentBom = ref<MaterialBomDetail>()
const versionList = ref<MaterialBomDetail[]>([])
const bomLoading = ref(false)
const bomEditorOpen = ref(false)
const bomEditorSaving = ref(false)
const bomComponentForm = reactive<BomComponentForm>({ lossRate: 0, materialCode: '', quantity: 1 })
const versionEditorOpen = ref(false)
const versionSaving = ref(false)
const versionForm = reactive({ effectiveDate: '', materialCode: '', reason: '', version: '' })

const treeBomId = ref('')
const treeLoading = ref(false)
const treeError = ref('')
const treeData = ref<BomTreeNode[]>([])
const treeExpanded = ref(true)
const treeKey = ref(0)

const analysisBomId = ref('')
const plannedQuantity = ref<number>(1)
const analysisLoading = ref(false)
const analysisError = ref('')
const analysisResult = ref<BomAnalysisResult[]>([])
const analysisHistory = ref<BomAnalysisRecord[]>([])
const reverseMaterialCode = ref('')
const reverseLoading = ref(false)
const reverseResult = ref<BomReverseTraceResult[]>([])
const reverseError = ref('')
const reverseQueried = ref(false)

const canManage = computed(
  () => auth.hasPermission('material:manage') || auth.hasRole('系统管理员'),
)
const selectedBom = computed(() =>
  bomOptions.value.find((bom) => bom.bomId === selectedBomId.value),
)
const treeBom = computed(() => bomOptions.value.find((bom) => bom.bomId === treeBomId.value))
const analysisBom = computed(() =>
  bomOptions.value.find((bom) => bom.bomId === analysisBomId.value),
)
const isCurrentBomDraft = computed(() => currentBom.value?.status === 'draft')
const materialDialogTitle = computed(() => {
  if (materialEditingId.value) {
    return '编辑物料'
  }
  return '新增物料'
})
const materialTypeLabels: Record<MaterialType, string> = {
  finished: '成品',
  raw: '原材料',
  semiFinished: '半成品',
}
const materialStatusLabels: Record<MaterialStatus, string> = { active: '启用', disabled: '停用' }
const bomStatusLabels: Record<MaterialBomStatus, string> = {
  archived: '已归档',
  draft: '草稿',
  released: '已发布',
}
const statusTone: Record<MaterialBomStatus | MaterialStatus, TagType> = {
  active: 'success',
  archived: 'info',
  disabled: 'danger',
  draft: 'warning',
  released: 'success',
}

function materialTypeLabel(type: MaterialType) {
  return materialTypeLabels[type]
}

function materialStatusLabel(status: MaterialStatus) {
  return materialStatusLabels[status]
}

function statusTagType(status: MaterialBomStatus | MaterialStatus) {
  return statusTone[status]
}

function bomStatusLabel(status: MaterialBomStatus) {
  return bomStatusLabels[status]
}
const summarizedAnalysis = computed(() => {
  const groups = new Map<string, BomAnalysisResult & { paths: string[] }>()
  analysisResult.value.forEach((item) => {
    const current = groups.get(item.materialCode)
    if (current) {
      current.theoreticalQuantity += item.theoreticalQuantity
      current.withLossQuantity += item.withLossQuantity
      current.paths.push(item.path)
    } else {
      groups.set(item.materialCode, { ...item, paths: [item.path] })
    }
  })
  return [...groups.values()]
})
const reverseQuantitySummary = computed(() => {
  const totals = new Map<string, number>()
  reverseResult.value.forEach((item) => {
    totals.set(item.unit, (totals.get(item.unit) ?? 0) + item.cumulativeQuantity)
  })
  return [...totals.entries()]
    .map(([unit, quantity]) => `${formatNumber(quantity)} ${unit}`)
    .join('；')
})

function makeMaterialQuery(): MaterialListQuery {
  return {
    categoryId: materialFilters.categoryId || undefined,
    createdFrom: materialFilters.createdRange[0],
    createdTo: materialFilters.createdRange[1],
    keyword: materialFilters.keyword,
    page: materialPage.value,
    pageSize: materialPageSize.value,
    status: materialFilters.status as MaterialStatus | undefined,
    type: materialFilters.type as MaterialType | undefined,
  }
}

async function loadMaterials(targetPage = materialPage.value) {
  loading.value = true
  error.value = ''
  materialPage.value = targetPage
  try {
    materialResult.value = await materialService.listMaterials(makeMaterialQuery())
    materialPage.value = materialResult.value.page
  } catch (requestError) {
    error.value = getErrorMessage(requestError, '物料数据加载失败')
  } finally {
    loading.value = false
  }
}

async function loadOptions() {
  const [categoryData, materialData, bomData] = await Promise.all([
    materialService.listCategories(),
    materialService.listMaterials({ page: 1, pageSize: 300 }),
    materialService.listBomRecords({ page: 1, pageSize: 300 }),
  ])
  categories.value = categoryData
  materialOptions.value = materialData.items
  bomOptions.value = bomData.items
  bomResult.value = bomData.items
}

async function refreshAll() {
  loading.value = true
  error.value = ''
  try {
    await Promise.all([loadOptions(), loadMaterials(materialPage.value), loadAnalysisHistory()])
  } catch (requestError) {
    error.value = getErrorMessage(requestError, '物料与 BOM 数据加载失败')
  } finally {
    loading.value = false
  }
}

function resetMaterialFilters() {
  Object.assign(materialFilters, {
    categoryId: '',
    createdRange: [],
    keyword: '',
    status: '',
    type: '',
  })
  void loadMaterials(1)
}

async function changeMaterialPageSize() {
  await loadMaterials(1)
}

async function openMaterialDetail(record: MaterialRecord) {
  currentMaterial.value = await materialService.getMaterial(record.id)
  materialDrawer.value = true
}

function resetMaterialForm() {
  Object.assign(materialForm, {
    categoryId: '',
    code: '',
    model: '',
    name: '',
    status: 'active',
    type: 'raw',
    unit: '',
  })
}

function openMaterialEditor(record?: MaterialRecord) {
  materialEditingId.value = record?.id
  if (record) {
    Object.assign(materialForm, {
      categoryId: record.categoryId,
      code: record.code,
      model: record.model,
      name: record.name,
      status: record.status,
      type: record.type,
      unit: record.unit,
    })
  } else {
    resetMaterialForm()
  }
  materialDialog.value = true
}

async function saveMaterial() {
  materialSaving.value = true
  try {
    let record = {} as MaterialRecord
    let message = '物料已新增'
    if (materialEditingId.value) {
      record = await materialService.updateMaterial(materialEditingId.value, materialForm)
      message = '物料已更新'
    } else {
      record = await materialService.createMaterial(materialForm)
    }
    materialDialog.value = false
    currentMaterial.value = record
    ElMessage.success(message)
    await refreshAll()
  } catch (requestError) {
    ElMessage.error(getErrorMessage(requestError, '物料保存失败'))
  } finally {
    materialSaving.value = false
  }
}

async function selectBom(bomId: string) {
  selectedBomId.value = bomId
  if (!bomId) {
    currentBom.value = undefined
    versionList.value = []
    return
  }
  bomLoading.value = true
  try {
    currentBom.value = await materialService.getBomDetail(bomId)
    versionList.value = await materialService.listBomVersions(currentBom.value.materialCode)
  } catch (requestError) {
    ElMessage.error(getErrorMessage(requestError, 'BOM 明细加载失败'))
  } finally {
    bomLoading.value = false
  }
}

function openComponentEditor(component?: MaterialBomDetail['components'][number]) {
  if (component) {
    Object.assign(bomComponentForm, {
      componentId: component.componentId,
      lossRate: component.lossRate,
      materialCode: component.materialCode,
      quantity: component.quantity,
    })
  } else {
    Object.assign(bomComponentForm, {
      componentId: undefined,
      lossRate: 0,
      materialCode: '',
      quantity: 1,
    })
  }
  bomEditorOpen.value = true
}

async function saveBomComponent() {
  if (!currentBom.value) {
    return
  }
  bomEditorSaving.value = true
  try {
    let message = 'BOM 明细已新增'
    if (bomComponentForm.componentId) {
      currentBom.value = await materialService.updateBomComponent(
        currentBom.value.bomId,
        bomComponentForm,
      )
      message = 'BOM 明细已更新'
    } else {
      currentBom.value = await materialService.addBomComponent(
        currentBom.value.bomId,
        bomComponentForm,
      )
    }
    bomEditorOpen.value = false
    ElMessage.success(message)
    await selectBom(currentBom.value.bomId)
    await refreshAll()
  } catch (requestError) {
    ElMessage.error(getErrorMessage(requestError, 'BOM 明细保存失败'))
  } finally {
    bomEditorSaving.value = false
  }
}

async function removeBomComponent(componentId: string) {
  if (!currentBom.value) {
    return
  }
  bomEditorSaving.value = true
  try {
    currentBom.value = await materialService.removeBomComponent(currentBom.value.bomId, componentId)
    ElMessage.success('BOM 明细已移除')
    await selectBom(currentBom.value.bomId)
    await refreshAll()
  } catch (requestError) {
    ElMessage.error(getErrorMessage(requestError, 'BOM 明细移除失败'))
  } finally {
    bomEditorSaving.value = false
  }
}

function openVersionEditor() {
  Object.assign(versionForm, {
    effectiveDate: '',
    materialCode: currentBom.value?.materialCode ?? '',
    reason: '',
    version: '',
  })
  versionEditorOpen.value = true
}

async function saveVersion() {
  versionSaving.value = true
  try {
    const created = await materialService.createBomVersion(versionForm)
    versionEditorOpen.value = false
    ElMessage.success('BOM 草稿版本已创建')
    await refreshAll()
    await selectBom(created.bomId)
  } catch (requestError) {
    ElMessage.error(getErrorMessage(requestError, 'BOM 版本创建失败'))
  } finally {
    versionSaving.value = false
  }
}

async function releaseBom(bomId: string) {
  versionSaving.value = true
  try {
    const updated = await materialService.setBomVersionReleased(bomId)
    ElMessage.success(`${updated.version} 已设置为当前生效版本`)
    await refreshAll()
    await selectBom(updated.bomId)
  } catch (requestError) {
    ElMessage.error(getErrorMessage(requestError, '设置版本生效失败'))
  } finally {
    versionSaving.value = false
  }
}

async function loadTree() {
  if (!treeBomId.value) {
    return
  }
  treeLoading.value = true
  treeError.value = ''
  try {
    treeData.value = await materialService.getTree(treeBomId.value)
  } catch (requestError) {
    treeError.value = getErrorMessage(requestError, 'BOM 树加载失败')
  } finally {
    treeLoading.value = false
  }
}

function toggleTree(expanded: boolean) {
  treeExpanded.value = expanded
  treeKey.value += 1
}

async function runAnalysis() {
  if (!analysisBomId.value) {
    return
  }
  analysisLoading.value = true
  analysisError.value = ''
  try {
    analysisResult.value = await materialService.runAnalysis(
      analysisBomId.value,
      plannedQuantity.value,
    )
    await loadAnalysisHistory()
  } catch (requestError) {
    analysisError.value = getErrorMessage(requestError, '用料分析失败')
  } finally {
    analysisLoading.value = false
  }
}

async function loadAnalysisHistory() {
  analysisHistory.value = await materialService.listAnalysisHistory()
}

async function loadReverseTrace() {
  reverseLoading.value = true
  reverseError.value = ''
  reverseQueried.value = true
  try {
    reverseResult.value = await materialService.getReverseTrace(reverseMaterialCode.value)
  } catch (requestError) {
    reverseResult.value = []
    reverseError.value = getErrorMessage(requestError, '反向追溯加载失败')
  } finally {
    reverseLoading.value = false
  }
}

function resetReverseTrace() {
  reverseError.value = ''
  reverseQueried.value = false
  reverseResult.value = []
}

onMounted(() => void refreshAll())
</script>

<template>
  <PageContainer>
    <PageHeader
      title="物料与 BOM"
      description="维护物料主数据、BOM 版本和组件关系，并完成树形展开、用料分析与反向追溯。"
    >
      <template #actions>
        <el-button :icon="Refresh" :loading="loading" @click="refreshAll">刷新</el-button>
      </template>
    </PageHeader>

    <el-alert
      v-if="!canManage"
      :closable="false"
      class="permission-tip"
      show-icon
      title="当前账号仅有查看权限，新增、编辑、移除和版本生效操作已禁用。"
      type="info"
    />
    <el-alert
      v-if="error"
      :closable="false"
      class="permission-tip"
      show-icon
      :title="error"
      type="error"
    />

    <el-tabs v-model="activeTab" class="material-tabs">
      <el-tab-pane label="物料主数据" name="materials">
        <el-card shadow="never">
          <el-form inline @submit.prevent="loadMaterials(1)">
            <el-form-item label="关键字"
              ><el-input
                v-model.trim="materialFilters.keyword"
                clearable
                placeholder="编号 / 名称 / 型号"
            /></el-form-item>
            <el-form-item label="类型"
              ><el-select
                v-model="materialFilters.type"
                clearable
                placeholder="全部"
                style="width: 120px"
                ><el-option
                  v-for="(label, value) in materialTypeLabels"
                  :key="value"
                  :label="label"
                  :value="value" /></el-select
            ></el-form-item>
            <el-form-item label="分类"
              ><el-select
                v-model="materialFilters.categoryId"
                clearable
                placeholder="全部"
                style="width: 130px"
                ><el-option
                  v-for="category in categories"
                  :key="category.id"
                  :label="category.name"
                  :value="category.id" /></el-select
            ></el-form-item>
            <el-form-item label="状态"
              ><el-select
                v-model="materialFilters.status"
                clearable
                placeholder="全部"
                style="width: 110px"
                ><el-option label="启用" value="active" /><el-option
                  label="停用"
                  value="disabled" /></el-select
            ></el-form-item>
            <el-form-item label="创建日期"
              ><el-date-picker
                v-model="materialFilters.createdRange"
                end-placeholder="结束日期"
                range-separator="至"
                start-placeholder="开始日期"
                type="daterange"
                value-format="YYYY-MM-DD"
            /></el-form-item>
            <el-form-item
              ><el-button :icon="Search" :loading="loading" type="primary" @click="loadMaterials(1)"
                >查询</el-button
              ><el-button @click="resetMaterialFilters">重置</el-button></el-form-item
            >
          </el-form>
          <div class="table-toolbar">
            <span>共 {{ materialResult.total }} 条物料</span
            ><el-button v-if="canManage" :icon="Plus" type="primary" @click="openMaterialEditor()"
              >新增物料</el-button
            >
          </div>
          <el-table v-loading="loading" :data="materialResult.items" min-height="360" stripe>
            <el-table-column label="物料编号" min-width="150" prop="code" /><el-table-column
              label="名称"
              min-width="180"
              prop="name"
            /><el-table-column label="类型" min-width="100"
              ><template #default="{ row }">{{
                materialTypeLabel(row.type)
              }}</template></el-table-column
            ><el-table-column label="型号" min-width="120" prop="model" /><el-table-column
              label="单位"
              min-width="80"
              prop="unit"
            /><el-table-column label="分类" min-width="100" prop="categoryName" /><el-table-column
              label="状态"
              min-width="90"
              ><template #default="{ row }"
                ><el-tag :type="statusTagType(row.status)">{{
                  materialStatusLabel(row.status)
                }}</el-tag></template
              ></el-table-column
            ><el-table-column label="当前 BOM" min-width="100"
              ><template #default="{ row }">{{
                row.currentBomVersion || '-'
              }}</template></el-table-column
            ><el-table-column label="更新时间" min-width="165"
              ><template #default="{ row }">{{
                formatDateTime(row.updatedAt)
              }}</template></el-table-column
            >
            <el-table-column fixed="right" label="操作" min-width="130"
              ><template #default="{ row }"
                ><el-button :icon="View" link type="primary" @click="openMaterialDetail(row)"
                  >查看</el-button
                ><el-button
                  v-if="canManage"
                  :icon="EditPen"
                  link
                  type="primary"
                  @click="openMaterialEditor(row)"
                  >编辑</el-button
                ></template
              ></el-table-column
            >
          </el-table>
          <EmptyState
            v-if="!loading && !materialResult.items.length"
            title="暂无符合条件的物料"
            description="请调整筛选条件后重试。"
          />
          <div class="pagination">
            <el-pagination
              v-model:current-page="materialPage"
              v-model:page-size="materialPageSize"
              background
              layout="total, sizes, prev, pager, next"
              :page-sizes="[10, 20, 50]"
              :total="materialResult.total"
              @current-change="loadMaterials"
              @size-change="changeMaterialPageSize"
            />
          </div>
        </el-card>
      </el-tab-pane>

      <el-tab-pane label="BOM 维护与版本" name="bom">
        <el-card shadow="never">
          <el-form inline
            ><el-form-item label="BOM 版本"
              ><el-select
                v-model="selectedBomId"
                filterable
                placeholder="选择 BOM 版本"
                style="width: 360px"
                @change="selectBom"
                ><el-option
                  v-for="bom in bomResult"
                  :key="bom.bomId"
                  :label="`${bom.materialCode} · ${bom.version} · ${bom.materialName}`"
                  :value="bom.bomId" /></el-select></el-form-item
            ><el-form-item
              ><el-button
                v-if="canManage"
                :disabled="!currentBom"
                :icon="Plus"
                type="primary"
                @click="openVersionEditor"
                >创建版本</el-button
              ></el-form-item
            ></el-form
          >
          <EmptyState
            v-if="!selectedBomId"
            title="请选择一个 BOM 版本"
            description="选择后可查看版本历史并维护草稿版本的组件。"
          />
          <template v-else-if="currentBom">
            <el-skeleton v-if="bomLoading" :rows="6" animated />
            <template v-else>
              <el-descriptions border :column="3"
                ><el-descriptions-item label="BOM 编号">{{
                  currentBom.bomCode
                }}</el-descriptions-item
                ><el-descriptions-item label="产品"
                  >{{ currentBom.materialName }}（{{
                    currentBom.materialCode
                  }}）</el-descriptions-item
                ><el-descriptions-item label="状态"
                  ><el-tag :type="statusTagType(currentBom.status)">{{
                    bomStatusLabel(currentBom.status)
                  }}</el-tag></el-descriptions-item
                ><el-descriptions-item label="版本">{{ currentBom.version }}</el-descriptions-item
                ><el-descriptions-item label="生效日期">{{
                  currentBom.effectiveDate
                }}</el-descriptions-item
                ><el-descriptions-item label="变更说明">{{
                  currentBom.description
                }}</el-descriptions-item></el-descriptions
              >
              <div class="section-heading">
                <h3>版本列表</h3>
                <span>历史版本只读，草稿版本可维护。</span>
              </div>
              <el-table :data="versionList" stripe
                ><el-table-column label="版本" prop="version" /><el-table-column label="状态"
                  ><template #default="{ row }"
                    ><el-tag :type="statusTagType(row.status)">{{
                      bomStatusLabel(row.status)
                    }}</el-tag></template
                  ></el-table-column
                ><el-table-column label="生效日期" prop="effectiveDate" /><el-table-column
                  label="更新时间"
                  ><template #default="{ row }">{{
                    formatDateTime(row.updatedAt)
                  }}</template></el-table-column
                ><el-table-column label="操作" min-width="170"
                  ><template #default="{ row }"
                    ><el-button link type="primary" @click="selectBom(row.bomId)">查看</el-button
                    ><el-popconfirm
                      v-if="canManage && row.status === 'draft'"
                      title="设置后当前已发布版本将归档，是否继续？"
                      confirm-button-text="确认生效"
                      @confirm="releaseBom(row.bomId)"
                      ><template #reference
                        ><el-button :loading="versionSaving" link type="danger"
                          >设为生效</el-button
                        ></template
                      ></el-popconfirm
                    ></template
                  ></el-table-column
                ></el-table
              >
              <div class="section-heading">
                <h3>组件明细</h3>
                <el-button
                  v-if="canManage && isCurrentBomDraft"
                  :icon="Plus"
                  type="primary"
                  @click="openComponentEditor()"
                  >新增明细</el-button
                >
              </div>
              <el-table :data="currentBom.components" stripe
                ><el-table-column label="行号" prop="lineNo" width="70" /><el-table-column
                  label="子项物料"
                  min-width="210"
                  ><template #default="{ row }"
                    >{{ row.materialName }}<small>{{ row.materialCode }}</small></template
                  ></el-table-column
                ><el-table-column label="用量" min-width="110"
                  ><template #default="{ row }"
                    >{{ formatNumber(row.quantity) }} {{ row.unit }}</template
                  ></el-table-column
                ><el-table-column label="损耗率" min-width="90"
                  ><template #default="{ row }"
                    >{{ formatNumber(row.lossRate) }}%</template
                  ></el-table-column
                ><el-table-column
                  label="工作中心"
                  min-width="100"
                  prop="workCenter"
                /><el-table-column label="操作" min-width="150"
                  ><template #default="{ row }"
                    ><el-button
                      v-if="canManage && isCurrentBomDraft"
                      link
                      type="primary"
                      @click="openComponentEditor(row)"
                      >编辑</el-button
                    ><el-popconfirm
                      v-if="canManage && isCurrentBomDraft"
                      title="移除后无法恢复，是否继续？"
                      confirm-button-text="移除"
                      @confirm="removeBomComponent(row.componentId)"
                      ><template #reference
                        ><el-button :loading="bomEditorSaving" link type="danger"
                          >移除</el-button
                        ></template
                      ></el-popconfirm
                    ></template
                  ></el-table-column
                ></el-table
              >
            </template>
          </template>
        </el-card>
      </el-tab-pane>

      <el-tab-pane label="BOM 结构树" name="tree">
        <el-card shadow="never"
          ><el-form inline
            ><el-form-item label="BOM 版本"
              ><el-select
                v-model="treeBomId"
                filterable
                placeholder="选择 BOM 版本"
                style="width: 360px"
                ><el-option
                  v-for="bom in bomOptions"
                  :key="bom.bomId"
                  :label="`${bom.materialCode} · ${bom.version}`"
                  :value="bom.bomId" /></el-select></el-form-item
            ><el-form-item
              ><el-button :loading="treeLoading" type="primary" @click="loadTree"
                >加载结构</el-button
              ><el-button :disabled="!treeData.length" @click="toggleTree(true)">展开全部</el-button
              ><el-button :disabled="!treeData.length" @click="toggleTree(false)"
                >收起全部</el-button
              ></el-form-item
            ></el-form
          ><el-alert
            v-if="treeError"
            :closable="false"
            :title="treeError"
            type="error" /><el-descriptions v-if="treeBom" border :column="2" class="tree-meta"
            ><el-descriptions-item label="产品">{{ treeBom.materialName }}</el-descriptions-item
            ><el-descriptions-item label="版本">{{
              treeBom.version
            }}</el-descriptions-item></el-descriptions
          ><el-tree
            v-if="treeData.length"
            :key="treeKey"
            :data="treeData"
            :default-expand-all="treeExpanded"
            node-key="path"
            :props="{ children: 'children', label: 'materialName' }"
            ><template #default="{ data }"
              ><span class="tree-node"
                ><strong>{{ data.materialName }}</strong
                ><small
                  >{{ data.materialCode }} · 层级 {{ data.level }} · 累计
                  {{ formatNumber(data.cumulativeQuantity) }} {{ data.unit }} ·
                  {{ data.isLeaf ? '叶子节点' : '组件' }}</small
                ></span
              ></template
            ></el-tree
          ><EmptyState
            v-else-if="!treeLoading && !treeError"
            title="暂无 BOM 树"
            description="选择产品版本并加载后查看多层级结构。"
        /></el-card>
      </el-tab-pane>

      <el-tab-pane label="理论用料分析" name="analysis">
        <el-card shadow="never"
          ><el-form inline
            ><el-form-item label="产品 BOM"
              ><el-select
                v-model="analysisBomId"
                filterable
                placeholder="选择 BOM 版本"
                style="width: 360px"
                ><el-option
                  v-for="bom in bomOptions"
                  :key="bom.bomId"
                  :label="`${bom.materialCode} · ${bom.version}`"
                  :value="bom.bomId" /></el-select></el-form-item
            ><el-form-item label="计划生产数量"
              ><el-input-number
                v-model="plannedQuantity"
                :min="0.01"
                :precision="2" /></el-form-item
            ><el-form-item
              ><el-button
                :disabled="!analysisBomId"
                :loading="analysisLoading"
                type="primary"
                @click="runAnalysis"
                >发起分析</el-button
              ></el-form-item
            ></el-form
          ><el-alert
            v-if="analysisError"
            :closable="false"
            :title="analysisError"
            type="error"
          /><el-descriptions v-if="analysisBom" border :column="2" class="tree-meta"
            ><el-descriptions-item label="产品">{{ analysisBom.materialName }}</el-descriptions-item
            ><el-descriptions-item label="版本">{{
              analysisBom.version
            }}</el-descriptions-item></el-descriptions
          >
          <div v-if="analysisResult.length" class="section-heading">
            <h3>用料汇总</h3>
            <span>数量不扣减库存；补偿后用量已按各层级损耗率计算。</span>
          </div>
          <el-table v-if="analysisResult.length" :data="summarizedAnalysis" stripe
            ><el-table-column label="物料" min-width="200"
              ><template #default="{ row }"
                >{{ row.materialName }}<small>{{ row.materialCode }}</small></template
              ></el-table-column
            ><el-table-column label="理论用量" min-width="120"
              ><template #default="{ row }"
                >{{ formatNumber(row.theoreticalQuantity) }} {{ row.unit }}</template
              ></el-table-column
            ><el-table-column label="补偿后用量" min-width="130"
              ><template #default="{ row }"
                >{{ formatNumber(row.withLossQuantity) }} {{ row.unit }}</template
              ></el-table-column
            ><el-table-column label="来源路径" min-width="300"
              ><template #default="{ row }">{{ row.paths.join('；') }}</template></el-table-column
            ></el-table
          >
          <div class="section-heading"><h3>分析记录</h3></div>
          <el-table :data="analysisHistory" stripe
            ><el-table-column label="产品" prop="materialName" /><el-table-column
              label="版本"
              prop="version"
            /><el-table-column label="计划数量" prop="plannedQuantity" /><el-table-column
              label="执行时间"
              ><template #default="{ row }">{{
                formatDateTime(row.executedAt)
              }}</template></el-table-column
            ></el-table
          ></el-card
        >
      </el-tab-pane>

      <el-tab-pane label="BOM 反向追溯" name="trace">
        <el-card shadow="never"
          ><el-form inline
            ><el-form-item label="物料"
              ><el-select
                v-model="reverseMaterialCode"
                filterable
                placeholder="选择物料"
                style="width: 360px"
                @change="resetReverseTrace"
                ><el-option
                  v-for="material in materialOptions"
                  :key="material.id"
                  :label="`${material.code} · ${material.name}`"
                  :value="material.code" /></el-select></el-form-item
            ><el-form-item
              ><el-button
                :disabled="!reverseMaterialCode"
                :loading="reverseLoading"
                type="primary"
                @click="loadReverseTrace"
                >查询使用关系</el-button
              ></el-form-item
            ></el-form
          ><el-alert v-if="reverseError" :closable="false" :title="reverseError" type="error"
            ><template #default
              ><el-button link type="primary" @click="loadReverseTrace"
                >重新查询</el-button
              ></template
            ></el-alert
          ><template v-else-if="reverseResult.length"
            ><div class="section-heading">
              <h3>完整上层路径</h3>
              <span
                >累计理论用量不含损耗；按路径分别展示，所有路径合计：{{
                  reverseQuantitySummary
                }}</span
              >
            </div>
            <el-table :data="reverseResult" stripe
              ><el-table-column label="直接父项" min-width="190"
                ><template #default="{ row }"
                  >{{ row.parentMaterialName }}<small>{{ row.parentMaterialCode }}</small></template
                ></el-table-column
              ><el-table-column label="最终产品" min-width="190"
                ><template #default="{ row }"
                  >{{ row.finalMaterialName }}<small>{{ row.finalMaterialCode }}</small></template
                ></el-table-column
              ><el-table-column label="BOM 版本路径" min-width="160"
                ><template #default="{ row }">{{
                  row.versions.join(' → ')
                }}</template></el-table-column
              ><el-table-column label="上溯层级" min-width="100"
                ><template #default="{ row }">第 {{ row.level }} 层</template></el-table-column
              ><el-table-column label="累计理论用量" min-width="140"
                ><template #default="{ row }"
                  >{{ formatNumber(row.cumulativeQuantity) }} {{ row.unit }}</template
                ></el-table-column
              ><el-table-column
                label="完整依赖路径"
                min-width="360"
                prop="path" /></el-table></template
          ><EmptyState
            v-else-if="!reverseLoading && reverseQueried"
            title="暂无上层 BOM 引用"
            description="所选物料当前没有被任何 BOM 版本使用。" /><EmptyState
            v-else-if="!reverseLoading"
            title="选择物料后查询"
            description="结果会展示完整上层路径、层级与累计理论用量。"
        /></el-card>
      </el-tab-pane>
    </el-tabs>

    <el-drawer v-model="materialDrawer" size="560px" title="物料详情"
      ><el-descriptions v-if="currentMaterial" border :column="1"
        ><el-descriptions-item label="物料编号">{{ currentMaterial.code }}</el-descriptions-item
        ><el-descriptions-item label="物料名称">{{ currentMaterial.name }}</el-descriptions-item
        ><el-descriptions-item label="类型">{{
          materialTypeLabels[currentMaterial.type]
        }}</el-descriptions-item
        ><el-descriptions-item label="型号">{{ currentMaterial.model }}</el-descriptions-item
        ><el-descriptions-item label="单位">{{ currentMaterial.unit }}</el-descriptions-item
        ><el-descriptions-item label="分类">{{ currentMaterial.categoryName }}</el-descriptions-item
        ><el-descriptions-item label="状态">{{
          materialStatusLabels[currentMaterial.status]
        }}</el-descriptions-item
        ><el-descriptions-item label="当前 BOM 版本">{{
          currentMaterial.currentBomVersion || '-'
        }}</el-descriptions-item
        ><el-descriptions-item label="创建时间">{{
          formatDateTime(currentMaterial.createdAt)
        }}</el-descriptions-item
        ><el-descriptions-item label="更新时间">{{
          formatDateTime(currentMaterial.updatedAt)
        }}</el-descriptions-item></el-descriptions
      ></el-drawer
    >

    <el-dialog v-model="materialDialog" :title="materialDialogTitle" width="560px"
      ><el-form label-width="92px"
        ><el-form-item label="物料编号" required
          ><el-input
            v-model.trim="materialForm.code"
            :disabled="Boolean(materialEditingId)"
            placeholder="例如 RM-AL-6061" /></el-form-item
        ><el-form-item label="物料名称" required
          ><el-input v-model.trim="materialForm.name" /></el-form-item
        ><el-form-item label="类型" required
          ><el-select v-model="materialForm.type"
            ><el-option
              v-for="(label, value) in materialTypeLabels"
              :key="value"
              :label="label"
              :value="value" /></el-select></el-form-item
        ><el-form-item label="型号" required
          ><el-input v-model.trim="materialForm.model" /></el-form-item
        ><el-form-item label="单位" required
          ><el-input v-model.trim="materialForm.unit" /></el-form-item
        ><el-form-item label="分类" required
          ><el-select v-model="materialForm.categoryId"
            ><el-option
              v-for="category in categories"
              :key="category.id"
              :label="category.name"
              :value="category.id" /></el-select></el-form-item
        ><el-form-item label="状态"
          ><el-radio-group v-model="materialForm.status"
            ><el-radio value="active">启用</el-radio
            ><el-radio value="disabled">停用</el-radio></el-radio-group
          ></el-form-item
        ></el-form
      ><template #footer
        ><el-button @click="materialDialog = false">取消</el-button
        ><el-button :loading="materialSaving" type="primary" @click="saveMaterial"
          >保存</el-button
        ></template
      ></el-dialog
    >

    <el-dialog
      v-model="bomEditorOpen"
      :title="bomComponentForm.componentId ? '编辑 BOM 明细' : '新增 BOM 明细'"
      width="500px"
      ><el-form label-width="100px"
        ><el-form-item label="子项物料" required
          ><el-select v-model="bomComponentForm.materialCode" filterable style="width: 100%"
            ><el-option
              v-for="material in materialOptions.filter(
                (item) => item.code !== currentBom?.materialCode,
              )"
              :key="material.id"
              :label="`${material.code} · ${material.name}`"
              :value="material.code" /></el-select></el-form-item
        ><el-form-item label="单位用量" required
          ><el-input-number
            v-model="bomComponentForm.quantity"
            :min="0.0001"
            :precision="4" /></el-form-item
        ><el-form-item label="损耗率" required
          ><el-input-number
            v-model="bomComponentForm.lossRate"
            :max="100"
            :min="0"
            :precision="2"
          /><span class="input-unit">%</span></el-form-item
        >
        <p class="form-hint">
          损耗率按百分比输入，如 1.5 代表 1.5%。保存时会校验父子项相同和循环依赖。
        </p></el-form
      ><template #footer
        ><el-button @click="bomEditorOpen = false">取消</el-button
        ><el-button :loading="bomEditorSaving" type="primary" @click="saveBomComponent"
          >保存</el-button
        ></template
      ></el-dialog
    >

    <el-dialog v-model="versionEditorOpen" title="创建 BOM 版本" width="520px"
      ><el-form label-width="92px"
        ><el-form-item label="产品" required
          ><el-select v-model="versionForm.materialCode" filterable style="width: 100%"
            ><el-option
              v-for="material in materialOptions.filter((item) => item.type !== 'raw')"
              :key="material.id"
              :label="`${material.code} · ${material.name}`"
              :value="material.code" /></el-select></el-form-item
        ><el-form-item label="版本号" required
          ><el-input v-model.trim="versionForm.version" placeholder="例如 V2.0" /></el-form-item
        ><el-form-item label="生效日期" required
          ><el-date-picker
            v-model="versionForm.effectiveDate"
            style="width: 100%"
            type="date"
            value-format="YYYY-MM-DD" /></el-form-item
        ><el-form-item label="变更原因"
          ><el-input
            v-model.trim="versionForm.reason"
            :rows="3"
            type="textarea" /></el-form-item></el-form
      ><template #footer
        ><el-button @click="versionEditorOpen = false">取消</el-button
        ><el-button :loading="versionSaving" type="primary" @click="saveVersion"
          >创建草稿</el-button
        ></template
      ></el-dialog
    >
  </PageContainer>
</template>

<style scoped>
.permission-tip {
  margin-bottom: 16px;
}
.material-tabs :deep(.el-tabs__content) {
  overflow: visible;
}
.table-toolbar,
.section-heading {
  align-items: center;
  display: flex;
  justify-content: space-between;
  gap: 12px;
  margin: 16px 0;
}
.table-toolbar span,
.section-heading span,
small,
.form-hint {
  color: var(--el-text-color-secondary);
  font-size: 13px;
}
small {
  display: block;
  margin-top: 3px;
}
.pagination {
  display: flex;
  justify-content: flex-end;
  margin-top: 16px;
}
.tree-meta {
  margin: 12px 0;
}
.tree-node {
  display: grid;
  gap: 2px;
  padding: 4px 0;
}
.input-unit {
  margin-left: 8px;
}
.form-hint {
  line-height: 1.6;
  margin: 0;
}
@media (max-width: 720px) {
  .pagination {
    justify-content: center;
  }
  .table-toolbar,
  .section-heading {
    align-items: flex-start;
    flex-direction: column;
  }
}
</style>
