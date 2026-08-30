export interface MockAccessProfile {
  name?: string
  permissions: string[]
  roles: string[]
}

export const systemAdministratorPermissions = [
  ...Object.values(PERMISSIONS.inventory),
  ...Object.values(PERMISSIONS.material),
  ...Object.values(PERMISSIONS.production),
  ...Object.values(PERMISSIONS.purchase),
  ...Object.values(PERMISSIONS.system),
  ...Object.values(PERMISSIONS.trace),
]

export const ordinaryUserPermissions = [
  PERMISSIONS.inventory.view,
  PERMISSIONS.material.view,
  PERMISSIONS.production.view,
  PERMISSIONS.trace.view,
]

export const mockAccessProfiles: Record<string, MockAccessProfile> = {
  DEV_ADMIN: {
    name: '本地开发管理员',
    permissions: systemAdministratorPermissions,
    roles: ['系统管理员'],
  },
  DEV_USER: {
    name: '本地开发普通用户',
    permissions: ordinaryUserPermissions,
    roles: ['普通用户'],
  },
  EXT_CUSTOMER: {
    name: '本地外部客户',
    permissions: [PERMISSIONS.production.view],
    roles: ['外部客户'],
  },
  GD0001: {
    name: '系统管理员',
    permissions: systemAdministratorPermissions,
    roles: ['系统管理员'],
  },
}

const defaultMockAccessProfile: MockAccessProfile = {
  permissions: ordinaryUserPermissions,
  roles: ['Mock普通用户'],
}

export function getMockAccessProfile(employeeNo?: string): MockAccessProfile {
  let profile: MockAccessProfile | undefined = undefined
  if (employeeNo) {
    profile = mockAccessProfiles[employeeNo.trim()]
  }
  const selectedProfile = profile ?? defaultMockAccessProfile

  return {
    name: selectedProfile.name,
    permissions: [...selectedProfile.permissions],
    roles: [...selectedProfile.roles],
  }
}

export function getSystemAdministratorPermissions() {
  return [...systemAdministratorPermissions]
}
import { PERMISSIONS } from '@/constants/permissions'
