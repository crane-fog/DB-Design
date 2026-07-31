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

const materialWriteMethods = new Set<PropertyKey>([
  'addBomComponent',
  'createBomVersion',
  'createMaterial',
  'removeBomComponent',
  'setBomVersionReleased',
  'updateBomComponent',
  'updateMaterial',
])

export const materialRepository = createPersistedMockProxy(
  materialBomMock,
  adapter,
  materialWriteMethods,
)

export function resetMaterialMockData() {
  return adapter.reset()
}
