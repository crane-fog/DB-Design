import { type MockAccessProfile, getMockAccessProfile } from '@/config/mock-access'
import type { SystemAccessContext } from '@/services/SystemService'

export interface MockAuthAccount extends MockAccessProfile {
  account: string
  id: number
  name: string
  password: string
  status: 'valid'
  token: string
}

export interface MockAuthSession {
  access: SystemAccessContext
  accessToken: string
  expiresInSeconds: number
}

const devAdminAccess = getMockAccessProfile('DEV_ADMIN')
const externalCustomerAccess = getMockAccessProfile('EXT_CUSTOMER')
const devUserAccess = getMockAccessProfile('DEV_USER')

const mockAuthAccounts: MockAuthAccount[] = [
  {
    account: 'DEV_ADMIN',
    id: -1001,
    name: '本地开发管理员',
    password: 'dev-admin-123',
    permissions: devAdminAccess.permissions,
    roles: devAdminAccess.roles,
    status: 'valid',
    token: 'local-dev.mock.dev-admin.v1',
  },
  {
    account: 'EXT_CUSTOMER',
    id: 301,
    name: '本地外部客户',
    password: 'customer-123',
    permissions: externalCustomerAccess.permissions,
    roles: externalCustomerAccess.roles,
    status: 'valid',
    token: 'local-dev.mock.external-customer.v1',
  },
  {
    account: 'DEV_USER',
    id: -1002,
    name: '本地开发普通用户',
    password: 'dev-user-123',
    permissions: devUserAccess.permissions,
    roles: devUserAccess.roles,
    status: 'valid',
    token: 'local-dev.mock.dev-user.v1',
  },
]

export function authenticateMockAccount(account: string, password: string): MockAuthSession {
  const mockAccount = mockAuthAccounts.find(
    (candidate) => candidate.account === account && candidate.password === password,
  )
  if (!mockAccount) {
    throw new Error('账号或密码错误')
  }

  return {
    access: {
      currentUser: {
        employeeNo: mockAccount.account,
        id: mockAccount.id,
        name: mockAccount.name,
      },
      permissions: [...mockAccount.permissions],
      roles: [...mockAccount.roles],
    },
    accessToken: mockAccount.token,
    expiresInSeconds: 8 * 60 * 60,
  }
}

export function getMockLoginAccounts() {
  return mockAuthAccounts.map(({ account, password }) => ({ account, password }))
}
