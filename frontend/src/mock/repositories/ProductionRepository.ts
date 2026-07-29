import { createPersistedMockAdapter, createPersistedMockProxy } from '@/mock'
import {
  productionMock,
  restoreProductionMock,
  snapshotProductionMock,
} from '@/config/production-mock'

const initialState = snapshotProductionMock()
const adapter = createPersistedMockAdapter({
  key: 'production',
  restore: restoreProductionMock,
  seedFactory: () => structuredClone(initialState),
  snapshot: snapshotProductionMock,
})

const writeMethods = new Set<PropertyKey>([
  'approveOrder',
  'cancelOrder',
  'createLine',
  'createOrder',
  'deleteCalendar',
  'finishOrder',
  'reportFault',
  'reviewExternalOrder',
  'saveCalendar',
  'saveCapacityConfig',
  'saveLineType',
  'startOrder',
  'updateFault',
  'updateLine',
  'updateOrder',
])

export const productionRepository = createPersistedMockProxy(productionMock, adapter, writeMethods)

export function resetProductionMockData() {
  return adapter.reset()
}
