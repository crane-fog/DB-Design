import { createPersistedMockAdapter, createPersistedMockProxy } from '@/mock'
import { restoreTraceMock, snapshotTraceMock, traceMock } from '@/config/trace-mock'

const initialState = snapshotTraceMock()
const adapter = createPersistedMockAdapter({
  key: 'trace',
  restore: restoreTraceMock,
  seedFactory: () => structuredClone(initialState),
  snapshot: snapshotTraceMock,
})

const writeMethods = new Set<PropertyKey>([
  'createBatchConsumption',
  'deleteBatchConsumption',
  'updateBatchConsumption',
])

export const traceRepository = createPersistedMockProxy(traceMock, adapter, writeMethods)

export function resetTraceMockData() {
  return adapter.reset()
}
