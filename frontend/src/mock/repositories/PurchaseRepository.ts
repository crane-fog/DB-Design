import { createPersistedMockAdapter, createPersistedMockProxy } from '@/mock'
import { purchaseMock, restorePurchaseMock, snapshotPurchaseMock } from '@/config/purchase-mock'

const initialState = snapshotPurchaseMock()
const adapter = createPersistedMockAdapter({
  key: 'purchase',
  restore: restorePurchaseMock,
  seedFactory: () => structuredClone(initialState),
  snapshot: snapshotPurchaseMock,
})

const writeMethods = new Set<PropertyKey>([
  'addReceipt',
  'cancelOrder',
  'createDrafts',
  'createOrder',
  'generateReminders',
  'handleReminder',
  'submitOrder',
])

export const purchaseRepository = createPersistedMockProxy(purchaseMock, adapter, writeMethods)

export function resetPurchaseMockData() {
  return adapter.reset()
}
