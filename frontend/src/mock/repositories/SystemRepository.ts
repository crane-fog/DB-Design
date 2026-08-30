import {
  type SystemMockState,
  restoreSystemMock,
  snapshotSystemMock,
  systemMock,
} from '@/config/system-mock'
import { createPersistedMockAdapter, createPersistedMockProxy } from '@/mock'

const initialState = snapshotSystemMock()
const adapter = createPersistedMockAdapter<SystemMockState>({
  key: 'system',
  restore: restoreSystemMock,
  seedFactory: () => structuredClone(initialState),
  snapshot: snapshotSystemMock,
})

const writeMethods = new Set<PropertyKey>([
  'assignRolePermissions',
  'assignUserRoles',
  'createRole',
  'createUser',
  'resetUserPassword',
  'updateRole',
  'updateRoleStatus',
  'updateUser',
  'updateUserStatus',
])

export const systemRepository = createPersistedMockProxy(systemMock, adapter, writeMethods)

export function resetSystemMockData() {
  return adapter.reset()
}
