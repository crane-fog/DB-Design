import { createPersistedMockAdapter, createPersistedMockProxy } from '@/mock'
import { inventoryMock, restoreInventoryMock, snapshotInventoryMock } from '@/config/inventory-mock'

const initialState = snapshotInventoryMock()
const adapter = createPersistedMockAdapter({
  key: 'inventory',
  restore: restoreInventoryMock,
  seedFactory: () => structuredClone(initialState),
  snapshot: snapshotInventoryMock,
})

const writeMethods = new Set<PropertyKey>([
  'addCompletionInbound',
  'generateAlerts',
  'handleAlert',
  'handleObsolete',
  'lockStock',
  'releaseLock',
])

export const inventoryRepository = createPersistedMockProxy(inventoryMock, adapter, writeMethods)

export function resetInventoryMockData() {
  return adapter.reset()
}
