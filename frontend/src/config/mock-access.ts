export interface MockAccessProfile {
  name?: string
  permissions: string[]
  roles: string[]
}

export const systemAdministratorPermissions = [
  'inventory:calc',
  'inventory:monitor',
  'inventory:register',
  'inventory:view',
  'material:view',
  'production:breakdown',
  'production:capacity',
  'production:orders',
  'production:view',
  'purchase:view',
  'system:audit:view',
  'system:role:assign-permission',
  'system:role:create',
  'system:role:update',
  'system:role:view',
  'system:user:create',
  'system:user:update',
  'system:user:view',
  'system:view',
  'trace:manage',
  'trace:view',
]

export const ordinaryUserPermissions = [
  'inventory:view',
  'material:view',
  'production:view',
  'trace:view',
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
