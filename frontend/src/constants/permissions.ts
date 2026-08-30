export const PERMISSIONS = {
  inventory: {
    calc: 'inventory:calc',
    manage: 'inventory:manage',
    monitor: 'inventory:monitor',
    register: 'inventory:register',
    view: 'inventory:view',
  },
  material: { manage: 'material:manage', view: 'material:view' },
  production: {
    breakdown: 'production:breakdown',
    capacity: 'production:capacity',
    manage: 'production:manage',
    orders: 'production:orders',
    view: 'production:view',
  },
  purchase: { manage: 'purchase:manage', view: 'purchase:view' },
  system: {
    auditView: 'system:audit:view',
    roleAssignPermission: 'system:role:assign-permission',
    roleCreate: 'system:role:create',
    roleUpdate: 'system:role:update',
    roleView: 'system:role:view',
    userCreate: 'system:user:create',
    userUpdate: 'system:user:update',
    userView: 'system:user:view',
    view: 'system:view',
  },
  trace: { manage: 'trace:manage', view: 'trace:view' },
} as const

export type PermissionCode =
  | (typeof PERMISSIONS.inventory)[keyof typeof PERMISSIONS.inventory]
  | (typeof PERMISSIONS.material)[keyof typeof PERMISSIONS.material]
  | (typeof PERMISSIONS.production)[keyof typeof PERMISSIONS.production]
  | (typeof PERMISSIONS.purchase)[keyof typeof PERMISSIONS.purchase]
  | (typeof PERMISSIONS.system)[keyof typeof PERMISSIONS.system]
  | (typeof PERMISSIONS.trace)[keyof typeof PERMISSIONS.trace]
