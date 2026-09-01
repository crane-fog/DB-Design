export type StatusTone = 'danger' | 'info' | 'neutral' | 'success' | 'warning'

export interface StatusPresentation {
  label: string
  tone: StatusTone
}

export const statusTones: Record<string, StatusTone> = {
  cancelled: 'neutral',
  completed: 'success',
  consumed: 'info',
  draft: 'warning',
  handled: 'success',
  ignored: 'neutral',
  in_progress: 'info',
  locked: 'warning',
  partial_received: 'info',
  pending: 'warning',
  pending_review: 'warning',
  pending_schedule: 'warning',
  pending_urge: 'warning',
  received: 'success',
  released: 'success',
  submitted: 'warning',
  urged: 'info',
}

type StatusMap<TStatus extends string> = Record<TStatus, StatusPresentation>

function labelsOf<TStatus extends string>(statuses: StatusMap<TStatus>) {
  return Object.fromEntries(
    (Object.entries(statuses) as [TStatus, StatusPresentation][]).map(([value, presentation]) => [
      value,
      presentation.label,
    ]),
  ) as Record<TStatus, string>
}

export const inventoryMonitorStatuses = {
  cancelled: { label: '已释放', tone: 'neutral' },
  consumed: { label: '已消耗', tone: 'info' },
  handled: { label: '已处理', tone: 'success' },
  ignored: { label: '已忽略', tone: 'neutral' },
  locked: { label: '锁定中', tone: 'warning' },
  pending: { label: '待处理', tone: 'warning' },
} satisfies StatusMap<'cancelled' | 'consumed' | 'handled' | 'ignored' | 'locked' | 'pending'>

export const purchaseOrderStatuses = {
  cancelled: { label: '已取消', tone: 'neutral' },
  completed: { label: '已完成', tone: 'success' },
  draft: { label: '草稿', tone: 'neutral' },
  partial_received: { label: '部分到货', tone: 'info' },
  submitted: { label: '已提交', tone: 'warning' },
} satisfies StatusMap<'cancelled' | 'completed' | 'draft' | 'partial_received' | 'submitted'>

export const purchaseReminderStatuses = {
  pending_urge: { label: '待催交', tone: 'warning' },
  received: { label: '已到货', tone: 'success' },
  urged: { label: '已催交', tone: 'info' },
} satisfies StatusMap<'pending_urge' | 'received' | 'urged'>

export const productionOrderStatuses = {
  cancelled: { label: '已取消', tone: 'neutral' },
  completed: { label: '已完工', tone: 'success' },
  in_progress: { label: '生产中', tone: 'info' },
  pending_review: { label: '待审核', tone: 'warning' },
  pending_schedule: { label: '待排产', tone: 'warning' },
} satisfies StatusMap<
  'cancelled' | 'completed' | 'in_progress' | 'pending_review' | 'pending_schedule'
>

export const inventoryMonitorStatusLabels = labelsOf(inventoryMonitorStatuses)
export const productionOrderStatusLabels = labelsOf(productionOrderStatuses)
export const purchaseOrderStatusLabels = labelsOf(purchaseOrderStatuses)
export const purchaseReminderStatusLabels = labelsOf(purchaseReminderStatuses)
