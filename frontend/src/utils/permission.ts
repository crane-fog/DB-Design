import type { PermissionCode } from '@/api'

export function hasPermission(permissions: readonly PermissionCode[], permission: PermissionCode) {
  return permissions.includes(permission)
}
