export interface MockAccessProfile {
  name?: string
  permissions: string[]
  roles: string[]
}

const systemAdministratorPermissions = [
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
  'trace:view',
]

const mockAccessProfiles: Record<string, MockAccessProfile> = {
  GD0001: {
    name: '系统管理员',
    permissions: systemAdministratorPermissions,
    roles: ['系统管理员'],
  },
}

const defaultMockAccessProfile: MockAccessProfile = {
  permissions: [],
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
