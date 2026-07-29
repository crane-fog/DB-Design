<script setup lang="ts">
import {
  type AccountStatus,
  type PageResult,
  type SystemRole,
  type SystemUser,
  type UserCreateFormData,
  type UserFormData,
  systemService,
} from '@/services/SystemService'
import { EditPen, Key, Plus, Refresh, UserFilled } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import { computed, onMounted, reactive, ref } from 'vue'
import PageContainer from '@/components/common/PageContainer.vue'
import PageHeader from '@/components/common/PageHeader.vue'
import { formatDateTime } from '@/utils/format'
import { getErrorMessage } from '@/utils/error'
import { useAuthStore } from '@/stores/auth'

interface UserFormModel extends UserFormData {
  password: string
}

const pageSize = 10
const auth = useAuthStore()
const filters = reactive({ employeeNo: '', status: '', userName: '' })
const page = ref(1)
const loading = ref(false)
const error = ref('')
const result = ref<PageResult<SystemUser>>({ items: [], page: 1, pageSize, total: 0 })
const userDialogVisible = ref(false)
const userDialogMode = ref<'create' | 'edit'>('create')
const userFormRef = ref<FormInstance>()
const editingUserId = ref<number>()
const submitting = ref(false)
const statusSubmitting = ref(false)
const passwordDialogVisible = ref(false)
const passwordFormRef = ref<FormInstance>()
const passwordSubmitting = ref(false)
const passwordTarget = ref<SystemUser>()
const roleDialogVisible = ref(false)
const roleLoading = ref(false)
const roleSubmitting = ref(false)
const roleError = ref('')
const roleTarget = ref<SystemUser>()
const roles = ref<SystemRole[]>([])
const assignedRoleIds = ref<number[]>([])
const roleIds = ref<number[]>([])

const userForm = reactive<UserFormModel>({
  email: '',
  employeeNo: '',
  name: '',
  password: '',
  phone: '',
  status: 'valid',
})
const passwordForm = reactive({ confirmPassword: '', password: '' })

const canCreate = computed(() => auth.hasPermission('system:user:create'))
const canUpdate = computed(() => auth.hasPermission('system:user:update'))
const userDialogTitle = computed(() => {
  if (userDialogMode.value === 'create') {
    return '新增用户'
  }
  return '编辑用户'
})
const userRules = computed<FormRules<UserFormModel>>(() => {
  const rules: FormRules<UserFormModel> = {
    employeeNo: [
      { message: '请输入工号', required: true, trigger: 'blur' },
      { max: 20, message: '工号不能超过 20 个字符', trigger: 'blur' },
    ],
    name: [
      { message: '请输入姓名', required: true, trigger: 'blur' },
      { max: 50, message: '姓名不能超过 50 个字符', trigger: 'blur' },
    ],
    phone: [
      { message: '请输入联系电话', required: true, trigger: 'blur' },
      { max: 20, message: '联系电话不能超过 20 个字符', trigger: 'blur' },
    ],
  }
  if (userDialogMode.value === 'create') {
    rules.password = [
      { message: '请输入初始密码', required: true, trigger: 'blur' },
      { message: '密码至少为 6 位', min: 6, trigger: 'blur' },
    ]
  }
  return rules
})
const passwordRules: FormRules<typeof passwordForm> = {
  confirmPassword: [
    { message: '请再次输入新密码', required: true, trigger: 'blur' },
    {
      trigger: 'blur',
      validator: (_rule, value, callback) => {
        if (value === passwordForm.password) {
          callback()
          return
        }
        callback(new Error('两次输入的密码不一致'))
      },
    },
  ],
  password: [
    { message: '请输入新密码', required: true, trigger: 'blur' },
    { message: '密码至少为 6 位', min: 6, trigger: 'blur' },
  ],
}

function selectedStatus(): AccountStatus | undefined {
  if (filters.status === 'valid' || filters.status === 'disabled') {
    return filters.status
  }
  return undefined
}

function resetUserForm() {
  Object.assign(userForm, {
    email: '',
    employeeNo: '',
    name: '',
    password: '',
    phone: '',
    status: 'valid' as AccountStatus,
  })
  editingUserId.value = undefined
  userFormRef.value?.clearValidate()
}

async function loadUsers(targetPage = page.value) {
  loading.value = true
  error.value = ''
  try {
    result.value = await systemService.listUsers({
      employeeNo: filters.employeeNo,
      page: targetPage,
      pageSize,
      status: selectedStatus(),
      userName: filters.userName,
    })
    page.value = result.value.page
  } catch (requestError) {
    error.value = getErrorMessage(requestError, '用户列表加载失败')
  } finally {
    loading.value = false
  }
}

function resetFilters() {
  Object.assign(filters, { employeeNo: '', status: '', userName: '' })
  void loadUsers(1)
}

function openCreateDialog() {
  userDialogMode.value = 'create'
  resetUserForm()
  userDialogVisible.value = true
}

function openEditDialog(user: SystemUser) {
  userDialogMode.value = 'edit'
  Object.assign(userForm, {
    email: user.email ?? '',
    employeeNo: user.employeeNo,
    name: user.name,
    password: '',
    phone: user.phone,
    status: user.status,
  })
  editingUserId.value = user.id
  userFormRef.value?.clearValidate()
  userDialogVisible.value = true
}

async function submitUserForm() {
  const valid = await userFormRef.value?.validate().catch(() => false)
  if (!valid || submitting.value) {
    return
  }

  submitting.value = true
  try {
    if (userDialogMode.value === 'create') {
      await systemService.createUser({ ...userForm } satisfies UserCreateFormData)
      ElMessage.success('用户新增成功')
    } else if (editingUserId.value !== undefined) {
      const { password: _password, ...form } = userForm
      await systemService.updateUser(editingUserId.value, form)
      ElMessage.success('用户信息已更新')
    }
    userDialogVisible.value = false
    await loadUsers(page.value)
  } catch (requestError) {
    ElMessage.error(getErrorMessage(requestError, '用户提交失败'))
  } finally {
    submitting.value = false
  }
}

async function updateStatus(user: SystemUser) {
  if (statusSubmitting.value) {
    return
  }

  let nextStatus: AccountStatus = 'valid'
  let action = '启用'
  if (user.status === 'valid') {
    nextStatus = 'disabled'
    action = '停用'
  }
  try {
    statusSubmitting.value = true
    await ElMessageBox.confirm(
      `确定要${action}用户“${user.name || user.employeeNo}”吗？`,
      `${action}用户`,
      {
        confirmButtonText: `确定${action}`,
        type: 'warning',
      },
    )
    await systemService.updateUserStatus(user.id, nextStatus)
    ElMessage.success(`用户已${action}`)
    await loadUsers(page.value)
  } catch (requestError) {
    if (requestError !== 'cancel' && requestError !== 'close') {
      ElMessage.error(getErrorMessage(requestError, `${action}用户失败`))
    }
  } finally {
    statusSubmitting.value = false
  }
}

function openPasswordDialog(user: SystemUser) {
  passwordTarget.value = user
  Object.assign(passwordForm, { confirmPassword: '', password: '' })
  passwordFormRef.value?.clearValidate()
  passwordDialogVisible.value = true
}

async function submitPasswordReset() {
  const valid = await passwordFormRef.value?.validate().catch(() => false)
  if (!valid || !passwordTarget.value || passwordSubmitting.value) {
    return
  }

  passwordSubmitting.value = true
  try {
    await ElMessageBox.confirm(
      `确定要为“${passwordTarget.value.name || passwordTarget.value.employeeNo}”重置密码吗？`,
      '重置密码',
      {
        confirmButtonText: '确定重置',
        type: 'warning',
      },
    )
    await systemService.resetUserPassword(passwordTarget.value.id, passwordForm.password)
    passwordDialogVisible.value = false
    ElMessage.success('密码已重置')
    await loadUsers(page.value)
  } catch (requestError) {
    if (requestError !== 'cancel' && requestError !== 'close') {
      ElMessage.error(getErrorMessage(requestError, '密码重置失败'))
    }
  } finally {
    passwordSubmitting.value = false
  }
}

async function openRoleDialog(user: SystemUser) {
  roleTarget.value = user
  roleDialogVisible.value = true
  roleLoading.value = true
  roleError.value = ''
  roles.value = []
  assignedRoleIds.value = []
  roleIds.value = []
  try {
    const [roleList, userRoleIds] = await Promise.all([
      systemService.listRoles(),
      systemService.listUserRoleIds(user.id),
    ])
    roles.value = roleList
    assignedRoleIds.value = userRoleIds
    roleIds.value = userRoleIds.filter((roleId) =>
      roleList.some((role) => role.id === roleId && role.status === 'valid'),
    )
  } catch (requestError) {
    roleError.value = getErrorMessage(requestError, '角色信息加载失败')
  } finally {
    roleLoading.value = false
  }
}

async function submitRoleAssignment() {
  if (!roleTarget.value || roleLoading.value || roleSubmitting.value || roleError.value) {
    return
  }

  roleSubmitting.value = true
  try {
    await ElMessageBox.confirm(
      `确定要更新“${roleTarget.value.name || roleTarget.value.employeeNo}”所关联的角色吗？`,
      '分配角色',
      {
        confirmButtonText: '确定保存',
        type: 'warning',
      },
    )
    await systemService.assignUserRoles(roleTarget.value.id, roleIds.value)
    roleDialogVisible.value = false
    ElMessage.success('用户角色已更新')
    await loadUsers(page.value)
  } catch (requestError) {
    if (requestError !== 'cancel' && requestError !== 'close') {
      ElMessage.error(getErrorMessage(requestError, '角色分配失败'))
    }
  } finally {
    roleSubmitting.value = false
  }
}

onMounted(() => void loadUsers())
</script>

<template>
  <PageContainer>
    <PageHeader title="用户管理" description="维护系统用户、账号状态及其角色授权关系。">
      <template #actions>
        <el-button v-if="canCreate" type="primary" :icon="Plus" @click="openCreateDialog">
          新增用户
        </el-button>
      </template>
    </PageHeader>

    <el-card class="user-search-card" shadow="never">
      <el-form :model="filters" inline @submit.prevent="loadUsers(1)">
        <el-form-item label="工号">
          <el-input v-model.trim="filters.employeeNo" clearable placeholder="支持模糊查询" />
        </el-form-item>
        <el-form-item label="姓名">
          <el-input v-model.trim="filters.userName" clearable placeholder="支持模糊查询" />
        </el-form-item>
        <el-form-item label="状态">
          <el-select v-model="filters.status" clearable placeholder="全部" style="width: 120px">
            <el-option label="启用" value="valid" />
            <el-option label="停用" value="disabled" />
          </el-select>
        </el-form-item>
        <el-form-item>
          <el-button type="primary" :loading="loading" @click="loadUsers(1)">查询</el-button>
          <el-button :disabled="loading" :icon="Refresh" @click="resetFilters">重置</el-button>
        </el-form-item>
      </el-form>
    </el-card>

    <el-card class="user-table-card" shadow="never">
      <el-alert
        v-if="error"
        class="user-request-error"
        :closable="false"
        show-icon
        :title="error"
        type="error"
      >
        <template #default>
          <el-button link type="primary" @click="loadUsers(page)">重新加载</el-button>
        </template>
      </el-alert>

      <el-table v-else v-loading="loading" :data="result.items" min-height="320" stripe>
        <el-table-column label="工号" min-width="120" prop="employeeNo" />
        <el-table-column label="姓名" min-width="110" prop="name" />
        <el-table-column label="手机号" min-width="130" prop="phone" />
        <el-table-column label="邮箱" min-width="190">
          <template #default="{ row }">{{ row.email || '-' }}</template>
        </el-table-column>
        <el-table-column label="状态" min-width="90">
          <template #default="{ row }">
            <el-tag :type="row.status === 'valid' ? 'success' : 'info'" effect="light">
              {{ row.status === 'valid' ? '启用' : '停用' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="最近登录" min-width="165">
          <template #default="{ row }">{{ formatDateTime(row.lastLoginTime) }}</template>
        </el-table-column>
        <el-table-column label="创建时间" min-width="165">
          <template #default="{ row }">{{ formatDateTime(row.createdTime) }}</template>
        </el-table-column>
        <el-table-column v-if="canUpdate" fixed="right" label="操作" min-width="270">
          <template #default="{ row }">
            <el-button link type="primary" :icon="EditPen" @click="openEditDialog(row)"
              >编辑</el-button
            >
            <el-button link type="primary" :icon="Key" @click="openPasswordDialog(row)"
              >重置密码</el-button
            >
            <el-button link type="primary" :icon="UserFilled" @click="openRoleDialog(row)"
              >分配角色</el-button
            >
            <el-button
              link
              :disabled="statusSubmitting"
              :type="row.status === 'valid' ? 'danger' : 'success'"
              @click="updateStatus(row)"
            >
              {{ row.status === 'valid' ? '停用' : '启用' }}
            </el-button>
          </template>
        </el-table-column>
      </el-table>

      <el-empty
        v-if="!loading && !error && !result.items.length"
        description="暂无符合条件的用户数据"
      />

      <div v-if="!error && result.total > 0" class="user-pagination">
        <el-pagination
          v-model:current-page="page"
          background
          layout="total, prev, pager, next"
          :page-size="pageSize"
          :total="result.total"
          @current-change="loadUsers"
        />
      </div>
    </el-card>

    <el-dialog
      v-model="userDialogVisible"
      :close-on-click-modal="false"
      :title="userDialogTitle"
      width="560px"
    >
      <el-form ref="userFormRef" :model="userForm" :rules="userRules" label-width="92px">
        <el-form-item label="工号" prop="employeeNo">
          <el-input v-model.trim="userForm.employeeNo" maxlength="20" show-word-limit />
        </el-form-item>
        <el-form-item label="姓名" prop="name">
          <el-input v-model.trim="userForm.name" maxlength="50" show-word-limit />
        </el-form-item>
        <el-form-item v-if="userDialogMode === 'create'" label="初始密码" prop="password">
          <el-input
            v-model="userForm.password"
            autocomplete="new-password"
            show-password
            type="password"
          />
        </el-form-item>
        <el-form-item label="手机号" prop="phone">
          <el-input v-model.trim="userForm.phone" maxlength="20" />
        </el-form-item>
        <el-form-item label="邮箱" prop="email">
          <el-input v-model.trim="userForm.email" maxlength="100" type="email" />
        </el-form-item>
        <el-form-item label="账号状态" prop="status">
          <el-radio-group v-model="userForm.status">
            <el-radio value="valid">启用</el-radio>
            <el-radio value="disabled">停用</el-radio>
          </el-radio-group>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button :disabled="submitting" @click="userDialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="submitting" @click="submitUserForm">保存</el-button>
      </template>
    </el-dialog>

    <el-dialog
      v-model="passwordDialogVisible"
      :close-on-click-modal="false"
      title="重置密码"
      width="460px"
    >
      <p class="dialog-description">
        将为“{{ passwordTarget?.name || passwordTarget?.employeeNo }}”设置新密码。
      </p>
      <el-form
        ref="passwordFormRef"
        :model="passwordForm"
        :rules="passwordRules"
        label-width="88px"
      >
        <el-form-item label="新密码" prop="password">
          <el-input
            v-model="passwordForm.password"
            autocomplete="new-password"
            show-password
            type="password"
          />
        </el-form-item>
        <el-form-item label="确认密码" prop="confirmPassword">
          <el-input
            v-model="passwordForm.confirmPassword"
            autocomplete="new-password"
            show-password
            type="password"
          />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button :disabled="passwordSubmitting" @click="passwordDialogVisible = false"
          >取消</el-button
        >
        <el-button type="primary" :loading="passwordSubmitting" @click="submitPasswordReset">
          确认重置
        </el-button>
      </template>
    </el-dialog>

    <el-dialog
      v-model="roleDialogVisible"
      :close-on-click-modal="false"
      title="分配角色"
      width="520px"
    >
      <template v-if="roleTarget">
        <p class="dialog-description">
          为“{{ roleTarget.name || roleTarget.employeeNo }}”选择有效角色。
        </p>
        <el-skeleton v-if="roleLoading" :rows="4" animated />
        <el-alert
          v-else-if="roleError"
          :closable="false"
          show-icon
          :title="roleError"
          type="error"
        />
        <template v-else>
          <el-alert
            v-if="
              assignedRoleIds.some((roleId) =>
                roles.some((role) => role.id === roleId && role.status === 'disabled'),
              )
            "
            class="role-disabled-hint"
            :closable="false"
            show-icon
            title="已关联的停用角色不会继续授权，保存后将移除这些关联。"
            type="warning"
          />
          <el-checkbox-group v-model="roleIds" class="role-checkboxes">
            <el-checkbox
              v-for="role in roles"
              :key="role.id"
              :disabled="role.status === 'disabled'"
              :label="role.id"
            >
              {{ role.name || `角色 ${role.id}` }}
              <el-tag v-if="role.status === 'disabled'" size="small" type="info">已停用</el-tag>
            </el-checkbox>
          </el-checkbox-group>
          <el-empty v-if="!roles.length" description="暂无可分配的角色" :image-size="80" />
        </template>
      </template>
      <template #footer>
        <el-button :disabled="roleSubmitting" @click="roleDialogVisible = false">取消</el-button>
        <el-button
          type="primary"
          :disabled="Boolean(roleError) || roleLoading"
          :loading="roleSubmitting"
          @click="submitRoleAssignment"
        >
          保存角色
        </el-button>
      </template>
    </el-dialog>
  </PageContainer>
</template>

<style scoped>
.user-search-card {
  margin-bottom: 16px;
}

.user-search-card :deep(.el-card__body) {
  padding-bottom: 2px;
}

.user-table-card :deep(.el-card__body) {
  padding: 0;
}

.user-request-error {
  margin: 16px 16px 0;
}

.user-pagination {
  display: flex;
  justify-content: flex-end;
  padding: 16px 20px;
}

.dialog-description {
  margin: 0 0 18px;
  color: #606266;
  font-size: 14px;
}

.role-disabled-hint {
  margin-bottom: 16px;
}

.role-checkboxes {
  display: grid;
  gap: 12px;
}

.role-checkboxes :deep(.el-checkbox) {
  height: auto;
  margin-right: 0;
}

.role-checkboxes :deep(.el-checkbox__label) {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  white-space: normal;
}

@media (max-width: 720px) {
  .user-pagination {
    justify-content: center;
  }
}
</style>
