import type {
  BomTreeNode as ApiBomTreeNode,
  MaterialCategory as ApiMaterialCategory,
  Bom,
  BomCreateRequest,
  BomVersion,
  DemandAnalysis,
  MaterialBomApiListMaterialDataRequest,
  MaterialCreateRequest,
  MaterialDetail,
  MaterialUpdateRequest,
} from '@/api'
import { type ApiEnvelope, type PageResult, mapPageResult, unwrap } from '@/services/pagination'
import type {
  BomAnalysisRecord,
  BomAnalysisResult,
  BomComponentForm,
  BomReverseTraceResult,
  BomTreeNode,
  BomVersionForm,
  MaterialBomDetail,
  MaterialBomListItem,
  MaterialCategory,
  MaterialForm,
  MaterialListQuery,
  MaterialRecord,
  MaterialType,
} from '@/types/material'
import { materialBomApi } from '@/api/client'

const pageSize = 200
const apiMaterialTypes = {
  auxiliary: 'auxiliary',
  finished: 'finished',
  raw: 'raw_material',
  semiFinished: 'semi_finished',
} as const
const materialTypes: Record<NonNullable<MaterialDetail['material_type']>, MaterialType> = {
  auxiliary: 'auxiliary',
  finished: 'finished',
  raw_material: 'raw',
  semi_finished: 'semiFinished',
}

function idNumber(value: string | number | undefined, label: string): number {
  const id = Number(value)
  if (!Number.isSafeInteger(id) || id <= 0) {
    throw new Error(`${label}必须是有效的正整数`)
  }
  return id
}

function requiredNumber(value: number | undefined, label: string): number {
  if (typeof value !== 'number' || !Number.isFinite(value)) {
    throw new Error(`接口未返回有效的${label}`)
  }
  return value
}

function requiredData<TPayload>(payload: ApiEnvelope<unknown>): TPayload {
  const data = unwrap(payload)
  if (data === undefined || data === null) {
    throw new Error('接口未返回业务数据')
  }
  return data as TPayload
}

/** 后端限制每页最多 200 条，选项和客户端筛选必须读取全部真实分页。 */
async function listAll<TItem>(
  request: (page: number) => Promise<{ data: ApiEnvelope<unknown> }>,
): Promise<TItem[]> {
  async function readPage(page: number) {
    const response = await request(page)
    return mapPageResult<TItem, TItem>(
      requiredData(response.data),
      { page, pageSize },
      (item) => item,
    )
  }
  const first = await readPage(1)
  const pageCount = Math.ceil(first.total / first.pageSize)
  const remaining = await Promise.all(
    Array.from({ length: Math.max(0, pageCount - 1) }, (_value, index) => readPage(index + 2)),
  )
  const items = [first, ...remaining].flatMap((result) => result.items)
  if (items.length !== first.total) {
    throw new Error('接口分页数据不完整，请刷新重试')
  }
  return items
}

function mapMaterial(item: MaterialDetail): MaterialRecord {
  if (!item.material_type || !materialTypes[item.material_type]) {
    throw new Error('接口返回了未知的物料类型')
  }
  const id = String(idNumber(item.material_id, '物料编号'))
  return {
    categoryId: String(idNumber(item.category_id, '分类编号')),
    categoryName: item.category_name ?? '',
    code: id,
    createdAt: item.created_time ?? '',
    currentBomVersion: item.current_version_no ?? undefined,
    currentVersionId: item.current_version_id,
    defaultSupplierId: item.default_supplier_id,
    id,
    model: item.model ?? '',
    name: item.material_name ?? '',
    safetyStock: requiredNumber(item.safety_stock, '安全库存'),
    type: materialTypes[item.material_type],
    unit: item.unit ?? '',
    updatedAt: item.updated_time ?? '',
  }
}

function mapVersion(version: BomVersion, material: MaterialRecord): MaterialBomListItem {
  const versionId = idNumber(version.version_id, '版本编号')
  return {
    bomId: String(versionId),
    effectiveDate: version.effective_date ?? '',
    expireDate: version.expire_date ?? undefined,
    isCurrent: material.currentVersionId === versionId,
    materialCode: material.id,
    materialName: material.name,
    version: version.version_no ?? '',
  }
}

function materialRequest(form: MaterialForm): MaterialCreateRequest {
  if (!form.name.trim() || !form.unit.trim()) {
    throw new Error('请填写物料名称和计量单位')
  }
  return {
    category_id: idNumber(form.categoryId, '分类编号'),
    material_name: form.name.trim(),
    material_type: apiMaterialTypes[form.type],
    model: form.model.trim(),
    unit: form.unit.trim(),
  }
}

/** updateMaterialData 是整条更新：读取最新记录，保留页面未编辑的业务字段。 */
function materialUpdateRequest(
  material: MaterialRecord,
  form: MaterialForm,
): MaterialUpdateRequest {
  return {
    ...materialRequest(form),
    current_version_id: material.currentVersionId ?? undefined,
    default_supplier_id: material.defaultSupplierId ?? undefined,
    material_id: idNumber(material.id, '物料编号'),
    safety_stock: material.safetyStock,
  }
}

async function getVersion(bomId: string): Promise<BomVersion> {
  const response = await materialBomApi.getBomVersionData({
    versionId: idNumber(bomId, '版本编号'),
  })
  return requiredData<BomVersion>(response.data)
}

async function getMaterial(materialId: string): Promise<MaterialRecord> {
  const response = await materialBomApi.getMaterialData({
    materialId: idNumber(materialId, '物料编号'),
  })
  return mapMaterial(requiredData<MaterialDetail>(response.data))
}

async function getTreeRows(bomId: string): Promise<ApiBomTreeNode[]> {
  const version = await getVersion(bomId)
  const response = await materialBomApi.getBomTreeData({
    materialId: idNumber(version.material_id, '物料编号'),
    versionId: idNumber(version.version_id, '版本编号'),
  })
  return requiredData<ApiBomTreeNode[]>(response.data)
}

function mapTree(rows: ApiBomTreeNode[]): BomTreeNode[] {
  const byPath = new Map<string, BomTreeNode>()
  const roots: BomTreeNode[] = []
  rows.forEach((row) => {
    if (!row.path || byPath.has(row.path)) {
      throw new Error('BOM 树返回了无效或重复的路径')
    }
    byPath.set(row.path, {
      children: [],
      cumulativeQuantity: requiredNumber(row.accumulated_quantity, '累计用量'),
      isLeaf: row.is_leaf === true,
      level: requiredNumber(row.depth, '节点层级'),
      materialCode: String(idNumber(row.material_id, '物料编号')),
      materialName: row.material_name ?? '',
      path: row.path,
      quantity: requiredNumber(row.quantity, '单位用量'),
      unit: row.unit ?? '',
    })
  })
  byPath.forEach((node) => {
    if (node.level === 0) {
      roots.push(node)
      return
    }
    // 同一物料可在不同分支出现，必须按完整路径挂接，不能按物料 ID 去重。
    const parent = byPath.get(node.path.slice(0, node.path.lastIndexOf('/')))
    if (!parent) {
      throw new Error('BOM 树缺少父节点')
    }
    parent.children.push(node)
  })
  return roots
}

async function componentRequest(bomId: string, form: BomComponentForm): Promise<BomCreateRequest> {
  if (!Number.isFinite(form.quantity) || form.quantity <= 0) {
    throw new Error('单位用量必须大于 0')
  }
  if (!Number.isFinite(form.lossRate) || form.lossRate < 0 || form.lossRate >= 100) {
    throw new Error('损耗率必须大于等于 0 且小于 100%')
  }
  const version = await getVersion(bomId)
  return {
    child_material_id: idNumber(form.materialCode, '子项物料编号'),
    loss_rate: form.lossRate / 100,
    parent_material_id: idNumber(version.material_id, '父项物料编号'),
    quantity: form.quantity,
    version_id: idNumber(version.version_id, '版本编号'),
  }
}

async function mapAnalysisRecord(data: DemandAnalysis): Promise<BomAnalysisRecord> {
  const materialCode = String(idNumber(data.material_id, '物料编号'))
  const bomId = String(idNumber(data.version_id, '版本编号'))
  const [material, version] = await Promise.all([getMaterial(materialCode), getVersion(bomId)])
  return {
    bomId,
    executedAt: data.analysis_time ?? '',
    id: String(idNumber(data.analysis_id, '分析编号')),
    materialCode,
    materialName: material.name,
    plannedQuantity: requiredNumber(data.production_qty, '计划数量'),
    version: version.version_no ?? '',
  }
}

/** 所有读取和写入均使用已生成的 materialBomApi，不缓存业务数据或伪造成功结果。 */
export const materialService = {
  async addBomComponent(bomId: string, form: BomComponentForm): Promise<void> {
    const response = await materialBomApi.addBomData({
      bomCreateRequest: await componentRequest(bomId, form),
    })
    requiredData<Bom>(response.data)
  },

  async createAnalysisRecord(bomId: string, plannedQuantity: number): Promise<BomAnalysisRecord> {
    if (!Number.isFinite(plannedQuantity) || plannedQuantity <= 0) {
      throw new Error('计划生产数量必须大于 0')
    }
    const version = await getVersion(bomId)
    const response = await materialBomApi.addRequirementAnalysis({
      demandAnalysisCreateRequest: {
        material_id: idNumber(version.material_id, '物料编号'),
        production_qty: plannedQuantity,
        version_id: idNumber(bomId, '版本编号'),
      },
    })
    return mapAnalysisRecord(requiredData<DemandAnalysis>(response.data))
  },

  async createBomVersion(form: BomVersionForm): Promise<string> {
    if (!form.version.trim() || !form.effectiveDate) {
      throw new Error('请填写版本号和生效日期')
    }
    if (form.expireDate && form.expireDate < form.effectiveDate) {
      throw new Error('失效日期不能早于生效日期')
    }
    const response = await materialBomApi.addBomVersionData({
      bomVersionCreateRequest: {
        change_reason: form.reason.trim(),
        effective_date: form.effectiveDate,
        expire_date: form.expireDate || undefined,
        material_id: idNumber(form.materialCode, '物料编号'),
        version_no: form.version.trim(),
      },
    })
    const version = requiredData<BomVersion>(response.data)
    return String(idNumber(version.version_id, '版本编号'))
  },

  async createMaterial(form: MaterialForm): Promise<MaterialRecord> {
    const response = await materialBomApi.addMaterialData({
      materialCreateRequest: materialRequest(form),
    })
    return mapMaterial(requiredData<MaterialDetail>(response.data))
  },

  async getBomDetail(bomId: string): Promise<MaterialBomDetail> {
    const version = await getVersion(bomId)
    const [materials, rows] = await Promise.all([
      listAll<MaterialDetail>((page) => materialBomApi.listMaterialData({ page, pageSize })),
      listAll<Bom>((page) =>
        materialBomApi.listBomData({ page, pageSize, versionId: idNumber(bomId, '版本编号') }),
      ),
    ])
    const byId = new Map(materials.map(mapMaterial).map((item) => [item.id, item]))
    const material = byId.get(String(idNumber(version.material_id, '父项物料编号')))
    if (!material) {
      throw new Error('BOM 父项物料不存在')
    }
    return {
      ...mapVersion(version, material),
      components: rows.map((row, index) => {
        const materialCode = String(idNumber(row.child_material_id, '子项物料编号'))
        const child = byId.get(materialCode)
        if (!child) {
          throw new Error('BOM 子项物料不存在')
        }
        return {
          componentId: String(idNumber(row.bom_id, 'BOM 明细编号')),
          lineNo: index + 1,
          lossRate: requiredNumber(row.loss_rate, '损耗率') * 100,
          materialCode,
          materialName: child.name,
          quantity: requiredNumber(row.quantity, '单位用量'),
          unit: child.unit,
        }
      }),
      description: version.change_reason ?? '',
    }
  },

  async getLatestAnalysis(bomId: string): Promise<BomAnalysisRecord | undefined> {
    const response = await materialBomApi.getRequirementAnalysis({
      versionId: idNumber(bomId, '版本编号'),
    })
    if (response.data.code === 404) {
      return undefined
    }
    return mapAnalysisRecord(requiredData<DemandAnalysis>(response.data))
  },

  getMaterial,

  async getOptions() {
    const [materials, versions] = await Promise.all([
      listAll<MaterialDetail>((page) => materialBomApi.listMaterialData({ page, pageSize })),
      listAll<BomVersion>((page) => materialBomApi.listBomVersionData({ page, pageSize })),
    ])
    const materialRecords = materials.map(mapMaterial)
    const byId = new Map(materialRecords.map((material) => [material.id, material]))
    const boms = versions.map((version) => {
      const material = byId.get(String(version.material_id))
      if (!material) {
        throw new Error('BOM 版本关联的物料不存在，请刷新重试')
      }
      return mapVersion(version, material)
    })
    return { boms, materials: materialRecords }
  },

  async getReverseTrace(
    materialCode: string,
    includeHistory = false,
  ): Promise<BomReverseTraceResult[]> {
    const [material, response] = await Promise.all([
      getMaterial(materialCode),
      materialBomApi.getReverseTraceData({
        includeHistory,
        materialId: idNumber(materialCode, '物料编号'),
      }),
    ])
    return requiredData<NonNullable<typeof response.data.data>>(response.data).map((item) => {
      if (item.version_status !== 'effective' && item.version_status !== 'history') {
        throw new Error('反向追溯返回了未知的版本状态')
      }
      return {
        cumulativeQuantity: requiredNumber(item.accumulated_quantity, '累计用量'),
        level: requiredNumber(item.depth, '追溯层级'),
        path: item.path ?? '',
        productMaterialCode: String(idNumber(item.product_material_id, '上层物料编号')),
        productMaterialName: item.product_material_name ?? '',
        unit: material.unit,
        version: item.version_no ?? '',
        versionId: String(idNumber(item.version_id, '版本编号')),
        versionStatus: item.version_status,
      }
    })
  },

  async getTree(bomId: string): Promise<BomTreeNode[]> {
    return mapTree(await getTreeRows(bomId))
  },

  async listBomVersions(materialCode: string): Promise<MaterialBomListItem[]> {
    const material = await getMaterial(materialCode)
    const rows = await listAll<BomVersion>((page) =>
      materialBomApi.listBomVersionData({
        materialId: idNumber(materialCode, '物料编号'),
        page,
        pageSize,
      }),
    )
    return rows.map((version) => mapVersion(version, material))
  },

  async listCategories(): Promise<MaterialCategory[]> {
    const rows = await listAll<ApiMaterialCategory>((page) =>
      materialBomApi.listMaterialCategoryData({ page, pageSize }),
    )
    return rows.map((row) => ({
      id: String(idNumber(row.category_id, '分类编号')),
      name: row.category_name ?? '',
    }))
  },

  async listMaterials(query: MaterialListQuery): Promise<PageResult<MaterialRecord>> {
    const filters: {
      categoryId?: number
      createdEndTime?: string
      createdStartTime?: string
      materialType?: MaterialBomApiListMaterialDataRequest['materialType']
    } = {}
    if (query.categoryId) {
      filters.categoryId = idNumber(query.categoryId, '分类编号')
    }
    if (query.createdTo) {
      filters.createdEndTime = `${query.createdTo}T23:59:59.999`
    }
    if (query.createdFrom) {
      filters.createdStartTime = `${query.createdFrom}T00:00:00`
    }
    if (query.type) {
      filters.materialType = apiMaterialTypes[query.type]
    }
    const keyword = query.keyword?.trim().toLocaleLowerCase()
    if (keyword) {
      // 契约没有“编号/名称/型号 OR”参数，保留组合搜索时只筛选完整的后端结果。
      const rows = await listAll<MaterialDetail>((page) =>
        materialBomApi.listMaterialData({ ...filters, page, pageSize }),
      )
      const matches = rows
        .map(mapMaterial)
        .filter((item) =>
          [item.code, item.name, item.model].some((value) =>
            value.toLocaleLowerCase().includes(keyword),
          ),
        )
      const start = (query.page - 1) * query.pageSize
      return {
        items: matches.slice(start, start + query.pageSize),
        page: query.page,
        pageSize: query.pageSize,
        total: matches.length,
      }
    }
    const response = await materialBomApi.listMaterialData({
      ...filters,
      page: query.page,
      pageSize: query.pageSize,
    })
    return mapPageResult<MaterialDetail, MaterialRecord>(
      requiredData(response.data),
      query,
      mapMaterial,
    )
  },

  async removeBomComponent(componentId: string): Promise<void> {
    const response = await materialBomApi.deleteBomData({
      bomDeleteRequest: { bom_id: idNumber(componentId, 'BOM 明细编号') },
    })
    unwrap(response.data)
  },

  async runAnalysis(bomId: string, plannedQuantity: number): Promise<BomAnalysisResult[]> {
    if (!Number.isFinite(plannedQuantity) || plannedQuantity <= 0) {
      throw new Error('计划生产数量必须大于 0')
    }
    const version = await getVersion(bomId)
    const [tree, response] = await Promise.all([
      getTreeRows(bomId),
      materialBomApi.calculateLossCompensation({
        lossCompensationCalculateRequest: {
          material_id: idNumber(version.material_id, '物料编号'),
          net_quantity: plannedQuantity,
          version_id: idNumber(bomId, '版本编号'),
        },
      }),
    ])
    const compensated = requiredData<NonNullable<typeof response.data.data>>(response.data)
    const groups = new Map<string, BomAnalysisResult>()
    tree
      .filter((node) => requiredNumber(node.depth, '节点层级') > 0)
      .forEach((node) => {
        const materialCode = String(idNumber(node.material_id, '物料编号'))
        const theoreticalQuantity =
          requiredNumber(node.accumulated_quantity, '累计用量') * plannedQuantity
        const existing = groups.get(materialCode)
        if (existing) {
          existing.theoreticalQuantity += theoreticalQuantity
          existing.path += `；${node.path ?? ''}`
        } else {
          groups.set(materialCode, {
            materialCode,
            materialName: node.material_name ?? '',
            path: node.path ?? '',
            theoreticalQuantity,
            unit: node.unit ?? '',
            withLossQuantity: 0,
          })
        }
      })
    const compensatedIds = new Set<string>()
    compensated.forEach((item) => {
      const id = String(idNumber(item.material_id, '物料编号'))
      const group = groups.get(id)
      if (!group) {
        throw new Error('BOM 树与损耗计算结果不一致，请重新查询')
      }
      group.withLossQuantity += requiredNumber(item.gross_quantity, '补偿后用量')
      compensatedIds.add(id)
    })
    if (compensatedIds.size !== groups.size) {
      throw new Error('损耗计算未返回全部物料，请重新查询')
    }
    return [...groups.values()]
  },

  async setBomVersionReleased(bomId: string): Promise<MaterialRecord> {
    const version = await getVersion(bomId)
    const material = await getMaterial(String(idNumber(version.material_id, '物料编号')))
    const response = await materialBomApi.updateMaterialData({
      materialUpdateRequest: {
        ...materialUpdateRequest(material, material),
        current_version_id: idNumber(version.version_id, '版本编号'),
      },
    })
    // 有效期和跨版本循环检查由后端执行，不能仅修改前端状态。
    return mapMaterial(requiredData<MaterialDetail>(response.data))
  },

  async updateBomComponent(bomId: string, form: BomComponentForm): Promise<void> {
    const response = await materialBomApi.updateBomData({
      bomUpdateRequest: {
        ...(await componentRequest(bomId, form)),
        bom_id: idNumber(form.componentId, 'BOM 明细编号'),
      },
    })
    requiredData<Bom>(response.data)
  },

  async updateBomVersionExpireDate(bomId: string, expireDate: string): Promise<void> {
    const version = await getVersion(bomId)
    const effectiveDate = version.effective_date
    const versionNo = version.version_no?.trim()
    if (!effectiveDate || !versionNo) {
      throw new Error('BOM 版本缺少版本号或生效日期，无法修改')
    }
    if (expireDate && expireDate < effectiveDate) {
      throw new Error('失效日期不能早于生效日期')
    }
    const response = await materialBomApi.updateBomVersionData({
      bomVersionUpdateRequest: {
        change_reason: version.change_reason,
        effective_date: effectiveDate,
        expire_date: expireDate || undefined,
        material_id: idNumber(version.material_id, '物料编号'),
        version_id: idNumber(version.version_id, '版本编号'),
        version_no: versionNo,
      },
    })
    requiredData<BomVersion>(response.data)
  },

  async updateMaterial(materialId: string, form: MaterialForm): Promise<MaterialRecord> {
    const material = await getMaterial(materialId)
    const response = await materialBomApi.updateMaterialData({
      materialUpdateRequest: materialUpdateRequest(material, form),
    })
    return mapMaterial(requiredData<MaterialDetail>(response.data))
  },
}
