import { createPersistedMockAdapter, createPersistedMockProxy } from '@/mock'
import {
  materialBomMock,
  restoreMaterialBomMock,
  snapshotMaterialBomMock,
} from '@/config/material-bom-mock'

const initialState = snapshotMaterialBomMock()
const adapter = createPersistedMockAdapter({
  key: 'material-bom',
  restore: restoreMaterialBomMock,
  seedFactory: () => structuredClone(initialState),
  snapshot: snapshotMaterialBomMock,
})

export const materialRepository = createPersistedMockProxy(materialBomMock, adapter, new Set())

export function resetMaterialMockData() {
  return adapter.reset()
}
