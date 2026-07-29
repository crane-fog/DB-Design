import type {
  MaterialBomDetail,
  MaterialBomListItem,
  MaterialBomListQuery,
  MaterialBomStatus,
  MaterialBomSummary,
} from '@/types/material'
import type { PageResult } from '@/services/pagination'

const bomDetails: MaterialBomDetail[] = [
  {
    audits: [
      { action: '创建 BOM 草稿', operatedAt: '2026-07-12T09:10:00', operator: '工艺工程师' },
      { action: '发布版本 V3.2', operatedAt: '2026-07-18T14:26:00', operator: '物料主管' },
    ],
    bomCode: 'BOM-FG-AX100-032',
    bomId: 'bom-ax100-v32',
    componentCount: 6,
    components: [
      {
        componentId: 'bom-ax100-001',
        leadTimeDays: 2,
        lineNo: 10,
        lossRate: 1.2,
        materialCode: 'RM-AL-6061',
        materialName: '铝合金型材 6061',
        quantity: 2.4,
        type: 'material',
        unit: 'kg',
        workCenter: '机加线 A',
      },
      {
        componentId: 'bom-ax100-002',
        leadTimeDays: 1,
        lineNo: 20,
        lossRate: 0.6,
        materialCode: 'SF-PCB-C01',
        materialName: '控制板组件 C01',
        quantity: 1,
        type: 'semiFinished',
        unit: 'pcs',
        workCenter: '总装线 1',
      },
      {
        componentId: 'bom-ax100-003',
        leadTimeDays: 3,
        lineNo: 30,
        lossRate: 2,
        materialCode: 'RM-CBL-025',
        materialName: '屏蔽线缆 0.25mm',
        quantity: 3.5,
        substituteGroup: 'CAB-A',
        type: 'material',
        unit: 'm',
        workCenter: '线束工位',
      },
      {
        componentId: 'bom-ax100-004',
        leadTimeDays: 1,
        lineNo: 40,
        lossRate: 0.4,
        materialCode: 'RM-SCR-M4',
        materialName: '内六角螺钉 M4',
        quantity: 12,
        type: 'material',
        unit: 'pcs',
        workCenter: '总装线 1',
      },
      {
        componentId: 'bom-ax100-005',
        leadTimeDays: 4,
        lineNo: 50,
        lossRate: 1.5,
        materialCode: 'RM-LBL-AX',
        materialName: 'AX 系列铭牌',
        quantity: 1,
        remark: '按销售区域切换标签模板',
        type: 'material',
        unit: 'pcs',
        workCenter: '包装工位',
      },
      {
        componentId: 'bom-ax100-006',
        leadTimeDays: 2,
        lineNo: 60,
        lossRate: 0.8,
        materialCode: 'SF-HSG-A02',
        materialName: 'A02 外壳预装件',
        quantity: 1,
        type: 'semiFinished',
        unit: 'pcs',
        workCenter: '总装线 1',
      },
    ],
    description: 'AX100 标准成品版本，用于常规订单排产与缺料计算。',
    effectiveDate: '2026-07-20',
    materialCode: 'FG-AX100',
    materialName: '智能控制终端 AX100',
    owner: '工艺工程师',
    status: 'released',
    totalLossRate: 1.1,
    totalQuantity: 20.9,
    unit: 'set',
    updatedAt: '2026-07-18T14:26:00',
    version: 'V3.2',
  },
  {
    audits: [
      { action: '复制 V3.2 生成试制版', operatedAt: '2026-07-22T10:15:00', operator: '研发工程师' },
      { action: '调整控制板组件', operatedAt: '2026-07-23T16:40:00', operator: '研发工程师' },
    ],
    bomCode: 'BOM-FG-AX100-040',
    bomId: 'bom-ax100-v40',
    componentCount: 7,
    components: [
      {
        componentId: 'bom-ax100-v40-001',
        leadTimeDays: 2,
        lineNo: 10,
        lossRate: 1.2,
        materialCode: 'RM-AL-6061',
        materialName: '铝合金型材 6061',
        quantity: 2.45,
        type: 'material',
        unit: 'kg',
        workCenter: '机加线 A',
      },
      {
        componentId: 'bom-ax100-v40-002',
        leadTimeDays: 1,
        lineNo: 20,
        lossRate: 0.6,
        materialCode: 'SF-PCB-C02',
        materialName: '控制板组件 C02',
        quantity: 1,
        type: 'semiFinished',
        unit: 'pcs',
        workCenter: '总装线 1',
      },
      {
        componentId: 'bom-ax100-v40-003',
        leadTimeDays: 3,
        lineNo: 30,
        lossRate: 1.8,
        materialCode: 'RM-CBL-025',
        materialName: '屏蔽线缆 0.25mm',
        quantity: 3.2,
        substituteGroup: 'CAB-A',
        type: 'material',
        unit: 'm',
        workCenter: '线束工位',
      },
      {
        componentId: 'bom-ax100-v40-004',
        leadTimeDays: 1,
        lineNo: 40,
        lossRate: 0.4,
        materialCode: 'RM-SCR-M4',
        materialName: '内六角螺钉 M4',
        quantity: 12,
        type: 'material',
        unit: 'pcs',
        workCenter: '总装线 1',
      },
      {
        componentId: 'bom-ax100-v40-005',
        leadTimeDays: 4,
        lineNo: 50,
        lossRate: 1.5,
        materialCode: 'RM-LBL-AX',
        materialName: 'AX 系列铭牌',
        quantity: 1,
        type: 'material',
        unit: 'pcs',
        workCenter: '包装工位',
      },
      {
        componentId: 'bom-ax100-v40-006',
        leadTimeDays: 2,
        lineNo: 60,
        lossRate: 0.8,
        materialCode: 'SF-HSG-A03',
        materialName: 'A03 外壳预装件',
        quantity: 1,
        type: 'semiFinished',
        unit: 'pcs',
        workCenter: '总装线 1',
      },
      {
        componentId: 'bom-ax100-v40-007',
        leadTimeDays: 5,
        lineNo: 70,
        lossRate: 1,
        materialCode: 'RM-SNS-T02',
        materialName: '温度传感器 T02',
        quantity: 1,
        type: 'material',
        unit: 'pcs',
        workCenter: '总装线 1',
      },
    ],
    description: 'AX100 新硬件平台试制版本，暂不参与正式排产。',
    effectiveDate: '2026-08-01',
    materialCode: 'FG-AX100',
    materialName: '智能控制终端 AX100',
    owner: '研发工程师',
    status: 'draft',
    totalLossRate: 1.04,
    totalQuantity: 21.65,
    unit: 'set',
    updatedAt: '2026-07-23T16:40:00',
    version: 'V4.0',
  },
  {
    audits: [
      { action: '发布版本 V2.1', operatedAt: '2026-06-11T11:20:00', operator: '物料主管' },
      { action: '归档历史版本', operatedAt: '2026-07-20T09:30:00', operator: '工艺工程师' },
    ],
    bomCode: 'BOM-FG-MX200-021',
    bomId: 'bom-mx200-v21',
    componentCount: 5,
    components: [
      {
        componentId: 'bom-mx200-001',
        leadTimeDays: 3,
        lineNo: 10,
        lossRate: 1.4,
        materialCode: 'RM-STEEL-304',
        materialName: '不锈钢板 304',
        quantity: 4.2,
        type: 'material',
        unit: 'kg',
        workCenter: '钣金线',
      },
      {
        componentId: 'bom-mx200-002',
        leadTimeDays: 2,
        lineNo: 20,
        lossRate: 0.9,
        materialCode: 'SF-MTR-M15',
        materialName: '驱动电机 M15',
        quantity: 2,
        type: 'semiFinished',
        unit: 'pcs',
        workCenter: '总装线 2',
      },
      {
        componentId: 'bom-mx200-003',
        leadTimeDays: 1,
        lineNo: 30,
        lossRate: 0.3,
        materialCode: 'RM-SCR-M6',
        materialName: '法兰螺栓 M6',
        quantity: 18,
        type: 'material',
        unit: 'pcs',
        workCenter: '总装线 2',
      },
      {
        componentId: 'bom-mx200-004',
        leadTimeDays: 4,
        lineNo: 40,
        lossRate: 1.1,
        materialCode: 'RM-CBL-050',
        materialName: '动力线缆 0.5mm',
        quantity: 2.8,
        type: 'material',
        unit: 'm',
        workCenter: '线束工位',
      },
      {
        componentId: 'bom-mx200-005',
        leadTimeDays: 2,
        lineNo: 50,
        lossRate: 0.5,
        materialCode: 'RM-PKG-MX',
        materialName: 'MX 系列包装套件',
        quantity: 1,
        type: 'material',
        unit: 'set',
        workCenter: '包装工位',
      },
    ],
    description: 'MX200 历史稳定版本，已被 V2.2 替代。',
    effectiveDate: '2026-06-15',
    materialCode: 'FG-MX200',
    materialName: '模块化执行器 MX200',
    owner: '物料主管',
    status: 'archived',
    totalLossRate: 0.84,
    totalQuantity: 28,
    unit: 'set',
    updatedAt: '2026-07-20T09:30:00',
    version: 'V2.1',
  },
  {
    audits: [
      { action: '创建新版本', operatedAt: '2026-07-15T09:30:00', operator: '工艺工程师' },
      { action: '发布版本 V2.2', operatedAt: '2026-07-21T15:05:00', operator: '物料主管' },
    ],
    bomCode: 'BOM-FG-MX200-022',
    bomId: 'bom-mx200-v22',
    componentCount: 6,
    components: [
      {
        componentId: 'bom-mx200-v22-001',
        leadTimeDays: 3,
        lineNo: 10,
        lossRate: 1.3,
        materialCode: 'RM-STEEL-304',
        materialName: '不锈钢板 304',
        quantity: 4,
        type: 'material',
        unit: 'kg',
        workCenter: '钣金线',
      },
      {
        componentId: 'bom-mx200-v22-002',
        leadTimeDays: 2,
        lineNo: 20,
        lossRate: 0.9,
        materialCode: 'SF-MTR-M16',
        materialName: '驱动电机 M16',
        quantity: 2,
        type: 'semiFinished',
        unit: 'pcs',
        workCenter: '总装线 2',
      },
      {
        componentId: 'bom-mx200-v22-003',
        leadTimeDays: 1,
        lineNo: 30,
        lossRate: 0.3,
        materialCode: 'RM-SCR-M6',
        materialName: '法兰螺栓 M6',
        quantity: 18,
        type: 'material',
        unit: 'pcs',
        workCenter: '总装线 2',
      },
      {
        componentId: 'bom-mx200-v22-004',
        leadTimeDays: 4,
        lineNo: 40,
        lossRate: 1,
        materialCode: 'RM-CBL-050',
        materialName: '动力线缆 0.5mm',
        quantity: 2.6,
        substituteGroup: 'CAB-MX',
        type: 'material',
        unit: 'm',
        workCenter: '线束工位',
      },
      {
        componentId: 'bom-mx200-v22-005',
        leadTimeDays: 2,
        lineNo: 50,
        lossRate: 0.5,
        materialCode: 'RM-PKG-MX',
        materialName: 'MX 系列包装套件',
        quantity: 1,
        type: 'material',
        unit: 'set',
        workCenter: '包装工位',
      },
      {
        componentId: 'bom-mx200-v22-006',
        leadTimeDays: 5,
        lineNo: 60,
        lossRate: 0.7,
        materialCode: 'RM-SNS-P01',
        materialName: '位置传感器 P01',
        quantity: 1,
        type: 'material',
        unit: 'pcs',
        workCenter: '总装线 2',
      },
    ],
    description: 'MX200 当前正式排产版本，优化电机和传感器配置。',
    effectiveDate: '2026-07-22',
    materialCode: 'FG-MX200',
    materialName: '模块化执行器 MX200',
    owner: '物料主管',
    status: 'released',
    totalLossRate: 0.78,
    totalQuantity: 28.6,
    unit: 'set',
    updatedAt: '2026-07-21T15:05:00',
    version: 'V2.2',
  },
  {
    audits: [
      { action: '创建 BOM', operatedAt: '2026-07-10T13:12:00', operator: '工艺工程师' },
      { action: '提交试产评审', operatedAt: '2026-07-24T09:00:00', operator: '研发工程师' },
    ],
    bomCode: 'BOM-SF-PCB-C02-010',
    bomId: 'bom-pcb-c02-v10',
    componentCount: 5,
    components: [
      {
        componentId: 'bom-pcb-c02-001',
        leadTimeDays: 8,
        lineNo: 10,
        lossRate: 2.5,
        materialCode: 'RM-PCB-4L',
        materialName: '四层 PCB 裸板',
        quantity: 1,
        type: 'material',
        unit: 'pcs',
        workCenter: 'SMT 线',
      },
      {
        componentId: 'bom-pcb-c02-002',
        leadTimeDays: 6,
        lineNo: 20,
        lossRate: 1,
        materialCode: 'RM-IC-MCU32',
        materialName: '32 位主控芯片',
        quantity: 1,
        substituteGroup: 'IC-MCU',
        type: 'material',
        unit: 'pcs',
        workCenter: 'SMT 线',
      },
      {
        componentId: 'bom-pcb-c02-003',
        leadTimeDays: 4,
        lineNo: 30,
        lossRate: 1.8,
        materialCode: 'RM-CAP-100N',
        materialName: '贴片电容 100nF',
        quantity: 8,
        type: 'material',
        unit: 'pcs',
        workCenter: 'SMT 线',
      },
      {
        componentId: 'bom-pcb-c02-004',
        leadTimeDays: 4,
        lineNo: 40,
        lossRate: 1.8,
        materialCode: 'RM-RES-10K',
        materialName: '贴片电阻 10K',
        quantity: 10,
        type: 'material',
        unit: 'pcs',
        workCenter: 'SMT 线',
      },
      {
        componentId: 'bom-pcb-c02-005',
        leadTimeDays: 3,
        lineNo: 50,
        lossRate: 2,
        materialCode: 'RM-CON-USB',
        materialName: 'USB-C 连接器',
        quantity: 1,
        type: 'material',
        unit: 'pcs',
        workCenter: 'DIP 线',
      },
    ],
    description: 'C02 控制板试产 BOM，供 AX100 V4.0 草稿引用。',
    effectiveDate: '2026-07-28',
    materialCode: 'SF-PCB-C02',
    materialName: '控制板组件 C02',
    owner: '研发工程师',
    status: 'draft',
    totalLossRate: 1.82,
    totalQuantity: 21,
    unit: 'pcs',
    updatedAt: '2026-07-24T09:00:00',
    version: 'V1.0',
  },
  {
    audits: [{ action: '发布量产版本', operatedAt: '2026-06-28T10:10:00', operator: '物料主管' }],
    bomCode: 'BOM-SF-PCB-C01-018',
    bomId: 'bom-pcb-c01-v18',
    componentCount: 5,
    components: [
      {
        componentId: 'bom-pcb-c01-001',
        leadTimeDays: 8,
        lineNo: 10,
        lossRate: 2.2,
        materialCode: 'RM-PCB-2L',
        materialName: '双层 PCB 裸板',
        quantity: 1,
        type: 'material',
        unit: 'pcs',
        workCenter: 'SMT 线',
      },
      {
        componentId: 'bom-pcb-c01-002',
        leadTimeDays: 6,
        lineNo: 20,
        lossRate: 1,
        materialCode: 'RM-IC-MCU16',
        materialName: '16 位主控芯片',
        quantity: 1,
        type: 'material',
        unit: 'pcs',
        workCenter: 'SMT 线',
      },
      {
        componentId: 'bom-pcb-c01-003',
        leadTimeDays: 4,
        lineNo: 30,
        lossRate: 1.6,
        materialCode: 'RM-CAP-100N',
        materialName: '贴片电容 100nF',
        quantity: 6,
        type: 'material',
        unit: 'pcs',
        workCenter: 'SMT 线',
      },
      {
        componentId: 'bom-pcb-c01-004',
        leadTimeDays: 4,
        lineNo: 40,
        lossRate: 1.6,
        materialCode: 'RM-RES-10K',
        materialName: '贴片电阻 10K',
        quantity: 8,
        type: 'material',
        unit: 'pcs',
        workCenter: 'SMT 线',
      },
      {
        componentId: 'bom-pcb-c01-005',
        leadTimeDays: 3,
        lineNo: 50,
        lossRate: 1.8,
        materialCode: 'RM-CON-MICRO',
        materialName: 'Micro USB 连接器',
        quantity: 1,
        type: 'material',
        unit: 'pcs',
        workCenter: 'DIP 线',
      },
    ],
    description: 'C01 控制板量产 BOM，供 AX100 V3.2 引用。',
    effectiveDate: '2026-07-01',
    materialCode: 'SF-PCB-C01',
    materialName: '控制板组件 C01',
    owner: '物料主管',
    status: 'released',
    totalLossRate: 1.64,
    totalQuantity: 17,
    unit: 'pcs',
    updatedAt: '2026-06-28T10:10:00',
    version: 'V1.8',
  },
  {
    audits: [
      { action: '发布标准包装 BOM', operatedAt: '2026-07-05T15:20:00', operator: '包装工程师' },
    ],
    bomCode: 'BOM-RM-PKG-MX-005',
    bomId: 'bom-pkg-mx-v05',
    componentCount: 4,
    components: [
      {
        componentId: 'bom-pkg-mx-001',
        leadTimeDays: 2,
        lineNo: 10,
        lossRate: 1,
        materialCode: 'RM-BOX-MX',
        materialName: 'MX 外箱',
        quantity: 1,
        type: 'material',
        unit: 'pcs',
        workCenter: '包装工位',
      },
      {
        componentId: 'bom-pkg-mx-002',
        leadTimeDays: 2,
        lineNo: 20,
        lossRate: 1,
        materialCode: 'RM-FOAM-MX',
        materialName: 'MX 缓冲泡棉',
        quantity: 2,
        type: 'material',
        unit: 'pcs',
        workCenter: '包装工位',
      },
      {
        componentId: 'bom-pkg-mx-003',
        leadTimeDays: 1,
        lineNo: 30,
        lossRate: 0.5,
        materialCode: 'RM-BAG-MX',
        materialName: 'MX 防静电袋',
        quantity: 1,
        type: 'material',
        unit: 'pcs',
        workCenter: '包装工位',
      },
      {
        componentId: 'bom-pkg-mx-004',
        leadTimeDays: 1,
        lineNo: 40,
        lossRate: 0.5,
        materialCode: 'RM-LBL-MX',
        materialName: 'MX 外箱标签',
        quantity: 1,
        type: 'material',
        unit: 'pcs',
        workCenter: '包装工位',
      },
    ],
    description: 'MX 系列包装套件标准 BOM。',
    effectiveDate: '2026-07-08',
    materialCode: 'RM-PKG-MX',
    materialName: 'MX 系列包装套件',
    owner: '包装工程师',
    status: 'released',
    totalLossRate: 0.75,
    totalQuantity: 5,
    unit: 'set',
    updatedAt: '2026-07-05T15:20:00',
    version: 'V0.5',
  },
  {
    audits: [
      { action: '归档旧包装 BOM', operatedAt: '2026-06-30T12:00:00', operator: '包装工程师' },
    ],
    bomCode: 'BOM-RM-PKG-AX-002',
    bomId: 'bom-pkg-ax-v02',
    componentCount: 3,
    components: [
      {
        componentId: 'bom-pkg-ax-001',
        leadTimeDays: 2,
        lineNo: 10,
        lossRate: 1,
        materialCode: 'RM-BOX-AX',
        materialName: 'AX 外箱',
        quantity: 1,
        type: 'material',
        unit: 'pcs',
        workCenter: '包装工位',
      },
      {
        componentId: 'bom-pkg-ax-002',
        leadTimeDays: 2,
        lineNo: 20,
        lossRate: 1,
        materialCode: 'RM-FOAM-AX',
        materialName: 'AX 缓冲泡棉',
        quantity: 2,
        type: 'material',
        unit: 'pcs',
        workCenter: '包装工位',
      },
      {
        componentId: 'bom-pkg-ax-003',
        leadTimeDays: 1,
        lineNo: 30,
        lossRate: 0.5,
        materialCode: 'RM-LBL-AX',
        materialName: 'AX 外箱标签',
        quantity: 1,
        type: 'material',
        unit: 'pcs',
        workCenter: '包装工位',
      },
    ],
    description: 'AX 系列历史包装 BOM，已不再用于新订单。',
    effectiveDate: '2026-05-20',
    materialCode: 'RM-PKG-AX',
    materialName: 'AX 系列旧包装套件',
    owner: '包装工程师',
    status: 'archived',
    totalLossRate: 0.83,
    totalQuantity: 4,
    unit: 'set',
    updatedAt: '2026-06-30T12:00:00',
    version: 'V0.2',
  },
]

function toListItem(detail: MaterialBomDetail): MaterialBomListItem {
  const {
    audits: _audits,
    components: _components,
    description: _description,
    ...listItem
  } = detail
  return { ...listItem }
}

function matchesKeyword(item: MaterialBomDetail, keyword?: string) {
  const normalizedKeyword = keyword?.trim().toLowerCase()
  if (!normalizedKeyword) {
    return true
  }
  return [item.bomCode, item.materialCode, item.materialName, item.version, item.owner]
    .join(' ')
    .toLowerCase()
    .includes(normalizedKeyword)
}

function matchesOwner(item: MaterialBomDetail, owner?: string) {
  const normalizedOwner = owner?.trim().toLowerCase()
  if (!normalizedOwner) {
    return true
  }
  return item.owner.toLowerCase().includes(normalizedOwner)
}

function matchesStatus(item: MaterialBomDetail, status?: MaterialBomStatus) {
  return !status || item.status === status
}

function queryBoms(query: MaterialBomListQuery) {
  return bomDetails.filter(
    (item) =>
      matchesKeyword(item, query.keyword) &&
      matchesOwner(item, query.owner) &&
      matchesStatus(item, query.status),
  )
}

export function snapshotMaterialBomMock() {
  return structuredClone({ bomDetails })
}

export function restoreMaterialBomMock(state: ReturnType<typeof snapshotMaterialBomMock>) {
  bomDetails.splice(0, bomDetails.length, ...structuredClone(state.bomDetails))
}

function delay<TResult>(factory: () => TResult): Promise<TResult> {
  return new Promise((resolve, reject) => {
    globalThis.setTimeout(() => {
      try {
        resolve(factory())
      } catch (error) {
        reject(error)
      }
    }, 180)
  })
}

export const materialBomMock = {
  getBomDetail(bomId: string) {
    return delay(() => {
      const detail = bomDetails.find((item) => item.bomId === bomId)
      if (!detail) {
        throw new Error('未找到对应的物料 BOM')
      }
      return { ...detail, audits: [...detail.audits], components: [...detail.components] }
    })
  },
  getBomSummary(): Promise<MaterialBomSummary> {
    return delay(() => {
      const releasedCount = bomDetails.filter((item) => item.status === 'released').length
      const draftCount = bomDetails.filter((item) => item.status === 'draft').length
      const archivedCount = bomDetails.filter((item) => item.status === 'archived').length
      return {
        activeCount: releasedCount + draftCount,
        archivedCount,
        draftCount,
        releasedCount,
      }
    })
  },
  listBomRecords(query: MaterialBomListQuery): Promise<PageResult<MaterialBomListItem>> {
    return delay(() => {
      const page = Math.max(1, query.page)
      const pageSize = Math.max(1, query.pageSize)
      const filteredItems = queryBoms(query)
      const start = (page - 1) * pageSize
      return {
        items: filteredItems.slice(start, start + pageSize).map(toListItem),
        page,
        pageSize,
        total: filteredItems.length,
      }
    })
  },
}
