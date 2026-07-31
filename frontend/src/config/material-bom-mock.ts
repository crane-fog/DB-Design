import type {
  BomAnalysisRecord,
  BomAnalysisResult,
  BomComponentForm,
  BomReverseTraceResult,
  BomTreeNode,
  BomVersionForm,
  MaterialBomDetail,
  MaterialBomListItem,
  MaterialBomListQuery,
  MaterialBomStatus,
  MaterialBomSummary,
  MaterialCategory,
  MaterialForm,
  MaterialListQuery,
  MaterialRecord,
  MaterialType,
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

const materialCategories: MaterialCategory[] = [
  { id: 'finished', name: '成品' },
  { id: 'semi-finished', name: '半成品' },
  { id: 'raw', name: '原材料' },
]

const materialTypeLabels: Record<MaterialType, string> = {
  finished: 'finished',
  raw: 'raw',
  semiFinished: 'semi-finished',
}

function inferMaterialType(code: string): MaterialType {
  if (code.startsWith('FG-')) {
    return 'finished'
  }
  if (code.startsWith('SF-')) {
    return 'semiFinished'
  }
  return 'raw'
}

function categoryForType(type: MaterialType) {
  const categoryId = materialTypeLabels[type]
  return materialCategories.find((category) => category.id === categoryId)!
}

function materialSource() {
  return bomDetails.flatMap((bom) => [
    { code: bom.materialCode, name: bom.materialName, unit: bom.unit },
    ...bom.components.map((component) => ({
      code: component.materialCode,
      name: component.materialName,
      unit: component.unit,
    })),
  ])
}

const materialRecords: MaterialRecord[] = [
  ...new Map(materialSource().map((material) => [material.code, material])).values(),
].map((material, index) => {
  const type = inferMaterialType(material.code)
  const category = categoryForType(type)
  return {
    categoryId: category.id,
    categoryName: category.name,
    code: material.code,
    createdAt: `2026-0${(index % 6) + 1}-15T09:00:00`,
    id: `material-${index + 1}`,
    model: material.name.match(/[A-Z]+[\d.-]+/)?.[0] ?? '-',
    name: material.name,
    status: 'active',
    type,
    unit: material.unit,
    updatedAt: '2026-07-24T09:00:00',
  }
})

const analysisHistory: BomAnalysisRecord[] = []

function cloneComponent(component: MaterialBomDetail['components'][number]) {
  return { ...component }
}

function cloneDetail(detail: MaterialBomDetail): MaterialBomDetail {
  return {
    ...detail,
    audits: detail.audits.map((audit) => ({ ...audit })),
    components: detail.components.map(cloneComponent),
  }
}

function getMaterialByCode(code: string) {
  return materialRecords.find((material) => material.code === code)
}

function materialWithCurrentBom(material: MaterialRecord): MaterialRecord {
  const copy = structuredClone(material)
  const releasedVersion = bomDetails.find(
    (bom) => bom.materialCode === material.code && bom.status === 'released',
  )
  if (releasedVersion) {
    copy.currentBomVersion = releasedVersion.version
  }
  return copy
}

function synchronizeBomDetail(detail: MaterialBomDetail) {
  detail.componentCount = detail.components.length
  detail.totalQuantity = detail.components.reduce(
    (total, component) => total + component.quantity,
    0,
  )
  detail.totalLossRate = 0
  if (detail.components.length) {
    detail.totalLossRate =
      detail.components.reduce((total, component) => total + component.lossRate, 0) /
      detail.components.length
  }
  detail.updatedAt = new Date().toISOString()
}

function componentTypeFor(material: MaterialRecord) {
  if (material.type === 'semiFinished') {
    return 'semiFinished' as const
  }
  return 'material' as const
}

function assertComponentCanBeSaved(
  detail: MaterialBomDetail,
  form: BomComponentForm,
  componentId?: string,
) {
  if (!form.materialCode.trim()) {
    throw new Error('请选择子项物料')
  }
  if (!Number.isFinite(form.quantity) || form.quantity <= 0) {
    throw new Error('单位用量必须大于 0')
  }
  if (!Number.isFinite(form.lossRate) || form.lossRate < 0 || form.lossRate > 100) {
    throw new Error('损耗率必须是 0 到 100 之间的百分比')
  }
  if (form.materialCode === detail.materialCode) {
    throw new Error('父项与子项不能相同')
  }

  const visited = new Set<string>()
  function canReach(from: string, target: string): boolean {
    if (from === target) {
      return true
    }
    if (visited.has(from)) {
      return false
    }
    visited.add(from)
    return bomDetails
      .filter((bom) => bom.materialCode === from)
      .some((bom) =>
        bom.components
          .filter((component) => component.componentId !== componentId)
          .some((component) => canReach(component.materialCode, target)),
      )
  }
  if (canReach(form.materialCode, detail.materialCode)) {
    throw new Error('检测到循环依赖：该子项会间接引用当前父项')
  }
}

function createTree(detail: MaterialBomDetail): BomTreeNode[] {
  interface TreeContext {
    chain: Set<string>
    cumulativeQuantity: number
    level: number
    lossRate?: number
    name: string
    path: string
    quantity?: number
    unit: string
  }
  function makeNode(code: string, context: TreeContext): BomTreeNode {
    const nextChain = new Set(context.chain)
    nextChain.add(code)
    const childBom = bomDetails.find(
      (bom) => bom.materialCode === code && bom.status !== 'archived',
    )
    const children: BomTreeNode[] = []
    if (childBom && !context.chain.has(code)) {
      childBom.components.forEach((component) => {
        children.push(
          makeNode(component.materialCode, {
            chain: nextChain,
            cumulativeQuantity: context.cumulativeQuantity * component.quantity,
            level: context.level + 1,
            lossRate: component.lossRate,
            name: component.materialName,
            path: `${context.path} / ${component.materialCode}`,
            quantity: component.quantity,
            unit: component.unit,
          }),
        )
      })
    }
    return {
      children,
      cumulativeQuantity: context.cumulativeQuantity,
      isLeaf: children.length === 0,
      level: context.level,
      lossRate: context.lossRate,
      materialCode: code,
      materialName: context.name,
      path: context.path,
      quantity: context.quantity,
      unit: context.unit,
    }
  }
  return [
    makeNode(detail.materialCode, {
      chain: new Set(),
      cumulativeQuantity: 1,
      level: 0,
      name: detail.materialName,
      path: detail.materialCode,
      unit: detail.unit,
    }),
  ]
}

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
  return structuredClone({ analysisHistory, bomDetails, materialRecords })
}

export function restoreMaterialBomMock(state: ReturnType<typeof snapshotMaterialBomMock>) {
  bomDetails.splice(0, bomDetails.length, ...structuredClone(state.bomDetails))
  materialRecords.splice(0, materialRecords.length, ...structuredClone(state.materialRecords))
  analysisHistory.splice(0, analysisHistory.length, ...structuredClone(state.analysisHistory))
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
  addBomComponent(bomId: string, form: BomComponentForm) {
    return delay(() => {
      const detail = bomDetails.find((item) => item.bomId === bomId)
      if (!detail) {
        throw new Error('未找到对应的 BOM 版本')
      }
      if (detail.status !== 'draft') {
        throw new Error('仅草稿版本允许维护 BOM 明细')
      }
      assertComponentCanBeSaved(detail, form)
      const material = getMaterialByCode(form.materialCode)
      if (!material) {
        throw new Error('未找到所选子项物料')
      }
      const componentId = `component-${crypto.randomUUID()}`
      detail.components.push({
        componentId,
        leadTimeDays: 1,
        lineNo: (detail.components[detail.components.length - 1]?.lineNo ?? 0) + 10,
        lossRate: form.lossRate,
        materialCode: material.code,
        materialName: material.name,
        quantity: form.quantity,
        type: componentTypeFor(material),
        unit: material.unit,
        workCenter: '待维护',
      })
      synchronizeBomDetail(detail)
      detail.audits.unshift({
        action: '新增 BOM 明细',
        operatedAt: detail.updatedAt,
        operator: '当前用户',
      })
      return cloneDetail(detail)
    })
  },
  createBomVersion(form: BomVersionForm) {
    return delay(() => {
      if (!/^V\d+(\.\d+)?$/i.test(form.version.trim())) {
        throw new Error('版本号格式应为 V1 或 V1.0')
      }
      if (!form.effectiveDate) {
        throw new Error('请选择生效日期')
      }
      const material = getMaterialByCode(form.materialCode)
      if (!material) {
        throw new Error('未找到产品物料')
      }
      if (
        bomDetails.some(
          (bom) => bom.materialCode === form.materialCode && bom.version === form.version,
        )
      ) {
        throw new Error('该产品的版本号已存在')
      }
      const source = bomDetails.find((bom) => bom.materialCode === form.materialCode)
      const now = new Date().toISOString()
      const detail: MaterialBomDetail = {
        audits: [
          {
            action: `创建版本：${form.reason.trim() || '未填写变更原因'}`,
            operatedAt: now,
            operator: '当前用户',
          },
        ],
        bomCode: `BOM-${form.materialCode}-${form.version.replace('.', '')}`,
        bomId: `bom-${crypto.randomUUID()}`,
        componentCount: 0,
        components: [],
        description: form.reason.trim() || `${material.name} 的新 BOM 版本`,
        effectiveDate: form.effectiveDate,
        materialCode: material.code,
        materialName: material.name,
        owner: '当前用户',
        status: 'draft',
        totalLossRate: 0,
        totalQuantity: 0,
        unit: material.unit,
        updatedAt: now,
        version: form.version.trim().toUpperCase(),
      }
      if (source) {
        source.components.forEach((component, index) => {
          const copied = cloneComponent(component)
          copied.componentId = `component-${crypto.randomUUID()}-${index}`
          detail.components.push(copied)
        })
      }
      synchronizeBomDetail(detail)
      bomDetails.unshift(detail)
      return cloneDetail(detail)
    })
  },
  createMaterial(form: MaterialForm) {
    return delay(() => {
      const code = form.code.trim().toUpperCase()
      if (!/^[A-Z][A-Z0-9-]{2,31}$/.test(code)) {
        throw new Error('物料编号仅支持大写字母、数字和连字符，长度为 3 到 32 位')
      }
      if (materialRecords.some((material) => material.code === code)) {
        throw new Error('物料编号已存在')
      }
      if (!form.name.trim() || form.name.trim().length > 80) {
        throw new Error('物料名称为必填项，且不得超过 80 个字符')
      }
      if (!form.model.trim() || !form.unit.trim()) {
        throw new Error('请填写型号和单位')
      }
      const category = materialCategories.find((item) => item.id === form.categoryId)
      if (!category) {
        throw new Error('请选择物料分类')
      }
      const now = new Date().toISOString()
      const material: MaterialRecord = {
        ...form,
        categoryId: category.id,
        categoryName: category.name,
        code,
        createdAt: now,
        id: `material-${crypto.randomUUID()}`,
        model: form.model.trim(),
        name: form.name.trim(),
        unit: form.unit.trim(),
        updatedAt: now,
      }
      materialRecords.unshift(material)
      return { ...material }
    })
  },
  getBomDetail(bomId: string) {
    return delay(() => {
      const detail = bomDetails.find((item) => item.bomId === bomId)
      if (!detail) {
        throw new Error('未找到对应的物料 BOM')
      }
      return cloneDetail(detail)
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
  getMaterial(materialId: string) {
    return delay(() => {
      const material = materialRecords.find((item) => item.id === materialId)
      if (!material) {
        throw new Error('未找到对应物料')
      }
      return materialWithCurrentBom(material)
    })
  },
  getReverseTrace(materialCode: string): Promise<BomReverseTraceResult[]> {
    return delay(() => {
      if (!materialCode) {
        return []
      }
      const results: BomReverseTraceResult[] = []
      interface TraceContext {
        level: number
        path: string[]
        quantity: number
      }
      function collect(childCode: string, context: TraceContext) {
        bomDetails.forEach((bom) => {
          bom.components
            .filter((component) => component.materialCode === childCode)
            .forEach((component) => {
              const nextPath = [bom.materialCode, ...context.path]
              results.push({
                cumulativeQuantity: context.quantity * component.quantity,
                finalMaterialCode: bom.materialCode,
                level: context.level,
                materialCode: childCode,
                materialName: getMaterialByCode(bom.materialCode)?.name ?? bom.materialName,
                parentMaterialCode: bom.materialCode,
                path: nextPath.join(' / '),
                unit: component.unit,
                version: bom.version,
              })
              if (!context.path.includes(bom.materialCode)) {
                collect(bom.materialCode, {
                  level: context.level + 1,
                  path: nextPath,
                  quantity: context.quantity * component.quantity,
                })
              }
            })
        })
      }
      collect(materialCode, { level: 1, path: [materialCode], quantity: 1 })
      return results
    })
  },
  getTree(bomId: string): Promise<BomTreeNode[]> {
    return delay(() => {
      const detail = bomDetails.find((item) => item.bomId === bomId)
      if (!detail) {
        throw new Error('未找到对应的 BOM 版本')
      }
      return createTree(detail)
    })
  },
  listAnalysisHistory(): Promise<BomAnalysisRecord[]> {
    return delay(() => analysisHistory.map((item) => ({ ...item })))
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
  listBomVersions(materialCode: string) {
    return delay(() => {
      const versions: MaterialBomDetail[] = []
      bomDetails
        .filter((item) => item.materialCode === materialCode)
        .forEach((item) => {
          const index = versions.findIndex((version) => version.updatedAt < item.updatedAt)
          if (index === -1) {
            versions.push(item)
          } else {
            versions.splice(index, 0, item)
          }
        })
      return versions.map(cloneDetail)
    })
  },
  listCategories() {
    return delay(() => materialCategories.map((category) => ({ ...category })))
  },
  listMaterials(query: MaterialListQuery): Promise<PageResult<MaterialRecord>> {
    return delay(() => {
      const keyword = query.keyword?.trim().toLowerCase()
      const filtered = materialRecords.filter((material) => {
        if (
          keyword &&
          ![material.code, material.name, material.model].join(' ').toLowerCase().includes(keyword)
        ) {
          return false
        }
        if (query.type && material.type !== query.type) {
          return false
        }
        if (query.categoryId && material.categoryId !== query.categoryId) {
          return false
        }
        if (query.status && material.status !== query.status) {
          return false
        }
        if (query.createdFrom && material.createdAt.slice(0, 10) < query.createdFrom) {
          return false
        }
        if (query.createdTo && material.createdAt.slice(0, 10) > query.createdTo) {
          return false
        }
        return true
      })
      const page = Math.max(1, query.page)
      const pageSize = Math.max(1, query.pageSize)
      const items = filtered
        .slice((page - 1) * pageSize, page * pageSize)
        .map(materialWithCurrentBom)
      return { items, page, pageSize, total: filtered.length }
    })
  },
  removeBomComponent(bomId: string, componentId: string) {
    return delay(() => {
      const detail = bomDetails.find((item) => item.bomId === bomId)
      if (!detail) {
        throw new Error('未找到对应的 BOM 版本')
      }
      if (detail.status !== 'draft') {
        throw new Error('历史或已发布版本不允许移除明细')
      }
      const index = detail.components.findIndex(
        (component) => component.componentId === componentId,
      )
      if (index === -1) {
        throw new Error('未找到对应的 BOM 明细')
      }
      detail.components.splice(index, 1)
      synchronizeBomDetail(detail)
      detail.audits.unshift({
        action: '移除 BOM 明细',
        operatedAt: detail.updatedAt,
        operator: '当前用户',
      })
      return cloneDetail(detail)
    })
  },
  runAnalysis(bomId: string, plannedQuantity: number): Promise<BomAnalysisResult[]> {
    return delay(() => {
      if (!Number.isFinite(plannedQuantity) || plannedQuantity <= 0) {
        throw new Error('计划生产数量必须大于 0')
      }
      const detail = bomDetails.find((item) => item.bomId === bomId)
      if (!detail) {
        throw new Error('未找到对应的 BOM 版本')
      }
      const rows: BomAnalysisResult[] = []
      interface AnalysisContext {
        amount: number
        chain: Set<string>
        path: string[]
      }
      function visit(current: MaterialBomDetail, context: AnalysisContext) {
        current.components.forEach((component) => {
          const theoreticalQuantity = context.amount * component.quantity
          const withLossQuantity = theoreticalQuantity * (1 + component.lossRate / 100)
          const nextPath = [...context.path, component.materialCode]
          const nested = bomDetails.find(
            (bom) => bom.materialCode === component.materialCode && bom.status !== 'archived',
          )
          if (nested && !context.chain.has(component.materialCode)) {
            const nextChain = new Set(context.chain)
            nextChain.add(component.materialCode)
            visit(nested, { amount: withLossQuantity, chain: nextChain, path: nextPath })
          } else {
            rows.push({
              cumulativeQuantity: withLossQuantity / plannedQuantity,
              lossRate: component.lossRate,
              materialCode: component.materialCode,
              materialName: component.materialName,
              path: nextPath.join(' / '),
              theoreticalQuantity,
              unit: component.unit,
              withLossQuantity,
            })
          }
        })
      }
      visit(detail, {
        amount: plannedQuantity,
        chain: new Set([detail.materialCode]),
        path: [detail.materialCode],
      })
      analysisHistory.unshift({
        bomId,
        executedAt: new Date().toISOString(),
        id: `analysis-${crypto.randomUUID()}`,
        materialCode: detail.materialCode,
        materialName: detail.materialName,
        plannedQuantity,
        version: detail.version,
      })
      return rows
    })
  },
  setBomVersionReleased(bomId: string) {
    return delay(() => {
      const target = bomDetails.find((item) => item.bomId === bomId)
      if (!target) {
        throw new Error('未找到对应的 BOM 版本')
      }
      bomDetails
        .filter((item) => item.materialCode === target.materialCode && item.status === 'released')
        .forEach((item) => {
          item.status = 'archived'
        })
      target.status = 'released'
      target.updatedAt = new Date().toISOString()
      target.audits.unshift({
        action: '设置为当前生效版本',
        operatedAt: target.updatedAt,
        operator: '当前用户',
      })
      return cloneDetail(target)
    })
  },
  updateBomComponent(bomId: string, form: BomComponentForm) {
    return delay(() => {
      const detail = bomDetails.find((item) => item.bomId === bomId)
      if (!detail || !form.componentId) {
        throw new Error('未找到对应的 BOM 明细')
      }
      if (detail.status !== 'draft') {
        throw new Error('仅草稿版本允许维护 BOM 明细')
      }
      const component = detail.components.find((item) => item.componentId === form.componentId)
      if (!component) {
        throw new Error('未找到对应的 BOM 明细')
      }
      assertComponentCanBeSaved(detail, form, component.componentId)
      const material = getMaterialByCode(form.materialCode)
      if (!material) {
        throw new Error('未找到所选子项物料')
      }
      Object.assign(component, {
        lossRate: form.lossRate,
        materialCode: material.code,
        materialName: material.name,
        quantity: form.quantity,
        type: componentTypeFor(material),
        unit: material.unit,
      })
      synchronizeBomDetail(detail)
      detail.audits.unshift({
        action: '编辑 BOM 明细',
        operatedAt: detail.updatedAt,
        operator: '当前用户',
      })
      return cloneDetail(detail)
    })
  },
  updateMaterial(materialId: string, form: MaterialForm) {
    return delay(() => {
      const material = materialRecords.find((item) => item.id === materialId)
      if (!material) {
        throw new Error('未找到对应物料')
      }
      if (!form.name.trim() || form.name.trim().length > 80) {
        throw new Error('物料名称为必填项，且不得超过 80 个字符')
      }
      const category = materialCategories.find((item) => item.id === form.categoryId)
      if (!category) {
        throw new Error('请选择物料分类')
      }
      Object.assign(material, {
        ...form,
        categoryName: category.name,
        code: material.code,
        model: form.model.trim(),
        name: form.name.trim(),
        unit: form.unit.trim(),
        updatedAt: new Date().toISOString(),
      })
      return { ...material }
    })
  },
}
