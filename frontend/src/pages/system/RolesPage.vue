<script setup lang="ts">
import {
  type AccountStatus,
  type PageResult,
  type PermissionTreeNode,
  type RoleFormData,
  type SystemRoleSummary,
  systemService,
} from '@/services/SystemService'
import { EditPen, Plus, Refresh, SetUp } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import { computed, nextTick, onMounted, reactive, ref } from 'vue'
import PageContainer from '@/components/common/PageContainer.vue'
import PageHeader from '@/components/common/PageHeader.vue'
import { getErrorMessage } from '@/utils/error'
import { useAuthStore } from '@/stores/auth'

const pageSize = 10
const auth = useAuthStore()
const filters = reactive({ roleId: '', roleName: '', status: '' })
const page = ref(1)
const loading = ref(false)
const error = ref('')
const result = ref<PageResult<SystemRoleSummary>>({ items: [], page: 1, pageSize, total: 0 })
const roleDialogVisible = ref(false)
const roleDialogMode = ref<'create' | 'edit'>('create')
const roleFormRef = ref<FormInstance>()
const editingRoleId = ref<number>()
const submitting = ref(false)
const permissionDialogVisible = ref(false)
const permissionTreeRef = ref<{
  getCheckedKeys: (leafOnly?: boolean) => unknown[]
  setCheckedKeys: (keys: string[], leafOnly?: boolean) => void
}>()
const permissionTarget = ref<SystemRoleSummary>()
const permissionTree = ref<PermissionTreeNode[]>([])
const allPermissionNodeKeys = ref<string[]>([])
const selectedPermissionIds = ref<number[]>([])
const permissionLoading = ref(false)
const permissionError = ref('')
const permissionSubmitting = ref(false)

const roleForm = reactive<RoleFormData>({ description: '', name: '', status: 'valid' })

const canCreate = computed(() => auth.hasPermission('system:role:create'))
const canUpdate = computed(() => auth.hasPermission('system:role:update'))
const canAssignPermission = computed(() => auth.hasPermission('system:role:assign-permission'))
const roleDialogTitle = computed(() => {
  if (roleDialogMode.value === 'create') {
    return '新增角色'
  }
  return '编辑角色'
})
const roleRules: FormRules<RoleFormData> = {
  description: [{ max: 200, message: '角色描述不能超过 200 个字符', trigger: 'blur' }],
  name: [
    { message: '请输入角色名称', required: true, trigger: 'blur' },
    { max: 50, message: '角色名称不能超过 50 个字符', trigger: 'blur' },
  ],
}

function selectedStatus(): AccountStatus | undefined {
  if (filters.status === 'valid' || filters.status === 'disabled') {
    return filters.status
  }
  return undefined
}

function selectedRoleId() {
  const roleId = Number(filters.roleId)
  if (Number.isInteger(roleId) && roleId > 0) {
    return roleId
  }
  return undefined
}

function resetRoleForm() {
  Object.assign(roleForm, { description: '', name: '', status: 'valid' as AccountStatus })
  editingRoleId.value = undefined
  roleFormRef.value?.clearValidate()
}

async function loadRoles(targetPage = page.value) {
  loading.value = true
  error.value = ''
  try {
    result.value = await systemService.listRolePage({
      page: targetPage,
      pageSize,
      roleId: selectedRoleId(),
      roleName: filters.roleName,
      status: selectedStatus(),
    })
    page.value = result.value.page
  } catch (requestError) {
    error.value = getErrorMessage(requestError, '角色列表加载失败')
  } finally {
    loading.value = false
  }
}

function resetFilters() {
  Object.assign(filters, { roleId: '', roleName: '', status: '' })
  void loadRoles(1)
}

function openCreateDialog() {
  roleDialogMode.value = 'create'
  resetRoleForm()
  roleDialogVisible.value = true
}

function openEditDialog(role: SystemRoleSummary) {
  roleDialogMode.value = 'edit'
  Object.assign(roleForm, {
    description: role.description ?? '',
    name: role.name,
    status: role.status,
  })
  editingRoleId.value = role.id
  roleFormRef.value?.clearValidate()
  roleDialogVisible.value = true
}

async function submitRoleForm() {
  const valid = await roleFormRef.value?.validate().catch(() => false)
  if (!valid || submitting.value) {
    return
  }

  submitting.value = true
  try {
    if (roleDialogMode.value === 'create') {
      await systemService.createRole({ ...roleForm })
      ElMessage.success('角色新增成功')
    } else if (editingRoleId.value !== undefined) {
      await systemService.updateRole(editingRoleId.value, { ...roleForm })
      ElMessage.success('角色信息已更新')
    }
    roleDialogVisible.value = false
    await loadRoles(page.value)
  } catch (requestError) {
    ElMessage.error(getErrorMessage(requestError, '角色提交失败'))
  } finally {
    submitting.value = false
  }
}

async function updateStatus(role: SystemRoleSummary) {
  let nextStatus: AccountStatus = 'valid'
  let action = '启用'
  if (role.status === 'valid') {
    nextStatus = 'disabled'
    action = '停用'
  }
  try {
    await ElMessageBox.confirm(`确定要${action}角色“${role.name}”吗？`, `${action}角色`, {
      confirmButtonText: `确定${action}`,
      type: 'warning',
    })
    await systemService.updateRoleStatus(role, nextStatus)
    ElMessage.success(`角色已${action}`)
    await loadRoles(page.value)
  } catch (requestError) {
    if (requestError !== 'cancel' && requestError !== 'close') {
      ElMessage.error(getErrorMessage(requestError, `${action}角色失败`))
    }
  }
}

function syncSelectedPermissions() {
  const nodeKeys = permissionTreeRef.value?.getCheckedKeys(true) ?? []
  selectedPermissionIds.value = systemService.getPermissionIdsFromNodeKeys(nodeKeys)
}

async function loadRolePermissionAssignment() {
  if (!permissionTarget.value) {
    return
  }

  permissionLoading.value = true
  permissionError.value = ''
  permissionTree.value = []
  allPermissionNodeKeys.value = []
  selectedPermissionIds.value = []
  try {
    const assignment = await systemService.loadRolePermissionAssignment(permissionTarget.value.id)
    permissionTree.value = assignment.tree
    allPermissionNodeKeys.value = assignment.allPermissionNodeKeys
    await nextTick()
    permissionTreeRef.value?.setCheckedKeys(assignment.checkedPermissionNodeKeys, true)
    syncSelectedPermissions()
  } catch (requestError) {
    permissionError.value = getErrorMessage(requestError, '权限信息加载失败')
  } finally {
    permissionLoading.value = false
  }
}

function openPermissionDialog(role: SystemRoleSummary) {
  if (role.status === 'disabled') {
    ElMessage.warning('停用角色不允许继续分配权限')
    return
  }
  permissionTarget.value = role
  permissionDialogVisible.value = true
  void loadRolePermissionAssignment()
}

function selectAllPermissions() {
  permissionTreeRef.value?.setCheckedKeys(allPermissionNodeKeys.value, true)
  syncSelectedPermissions()
}

function clearPermissions() {
  permissionTreeRef.value?.setCheckedKeys([], true)
  selectedPermissionIds.value = []
}

async function submitPermissionAssignment() {
  if (!permissionTarget.value || permissionSubmitting.value || permissionLoading.value) {
    return
  }
  if (!selectedPermissionIds.value.length) {
    ElMessage.warning('当前接口要求至少选择一个权限，无法提交空集合')
    return
  }

  permissionSubmitting.value = true
  try {
    await systemService.assignRolePermissions(
      permissionTarget.value.id,
      selectedPermissionIds.value,
    )
    permissionDialogVisible.value = false
    ElMessage.success('角色权限已更新')
    await loadRoles(page.value)
  } catch (requestError) {
    ElMessage.error(getErrorMessage(requestError, '权限分配失败'))
  } finally {
    permissionSubmitting.value = false
  }
}

onMounted(() => void loadRoles())
</script>

<template>
  <PageContainer>
    <PageHeader title="角色管理" description="维护系统角色的基础信息、启停状态和关联情况。">
      <template #actions>
        <el-button v-if="canCreate" type="primary" :icon="Plus" @click="openCreateDialog">
          新增角色
        </el-button>
      </template>
    </PageHeader>

    <el-card class="role-search-card" shadow="never">
      <el-form :model="filters" inline @submit.prevent="loadRoles(1)">
        <el-form-item label="角色编号">
          <el-input v-model.trim="filters.roleId" clearable placeholder="请输入编号" />
        </el-form-item>
        <el-form-item label="角色名称">
          <el-input v-model.trim="filters.roleName" clearable placeholder="支持模糊查询" />
        </el-form-item>
        <el-form-item label="状态">
          <el-select v-model="filters.status" clearable placeholder="全部" style="width: 120px">
            <el-option label="启用" value="valid" />
            <el-option label="停用" value="disabled" />
          </el-select>
        </el-form-item>
        <el-form-item>
          <el-button type="primary" :loading="loading" @click="loadRoles(1)">查询</el-button>
          <el-button :disabled="loading" :icon="Refresh" @click="resetFilters">重置</el-button>
        </el-form-item>
      </el-form>
    </el-card>

    <el-card class="role-table-card" shadow="never">
      <el-alert
        v-if="error"
        class="role-request-error"
        :closable="false"
        show-icon
        :title="error"
        type="error"
      >
        <template #default>
          <el-button link type="primary" @click="loadRoles(page)">重新加载</el-button>
        </template>
      </el-alert>

      <el-table v-else v-loading="loading" :data="result.items" min-height="320" stripe>
        <el-table-column label="角色编号" min-width="100" prop="id" />
        <el-table-column label="角色名称" min-width="150" prop="name" />
        <el-table-column label="角色描述" min-width="240" show-overflow-tooltip>
          <template #default="{ row }">{{ row.description || '-' }}</template>
        </el-table-column>
        <el-table-column label="状态" min-width="90">
          <template #default="{ row }">
            <el-tag :type="row.status === 'valid' ? 'success' : 'info'" effect="light">
              {{ row.status === 'valid' ? '启用' : '停用' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="关联用户" min-width="110" prop="userCount" />
        <el-table-column label="关联权限" min-width="110" prop="permissionCount" />
        <el-table-column
          v-if="canUpdate || canAssignPermission"
          fixed="right"
          label="操作"
          min-width="240"
        >
          <template #default="{ row }">
            <el-button
              v-if="canUpdate"
              link
              type="primary"
              :icon="EditPen"
              @click="openEditDialog(row)"
              >编辑</el-button
            >
            <el-button
              v-if="canAssignPermission"
              link
              type="primary"
              :disabled="row.status === 'disabled'"
              :icon="SetUp"
              @click="openPermissionDialog(row)"
            >
              分配权限
            </el-button>
            <el-button
              v-if="canUpdate"
              link
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
        description="暂无符合条件的角色数据"
      />

      <div v-if="!error && result.total > 0" class="role-pagination">
        <el-pagination
          v-model:current-page="page"
          background
          layout="total, prev, pager, next"
          :page-size="pageSize"
          :total="result.total"
          @current-change="loadRoles"
        />
      </div>
    </el-card>

    <el-dialog
      v-model="roleDialogVisible"
      :close-on-click-modal="false"
      :title="roleDialogTitle"
      width="560px"
    >
      <el-form ref="roleFormRef" :model="roleForm" :rules="roleRules" label-width="92px">
        <el-form-item label="角色名称" prop="name">
          <el-input v-model.trim="roleForm.name" maxlength="50" show-word-limit />
        </el-form-item>
        <el-form-item label="角色描述" prop="description">
          <el-input
            v-model.trim="roleForm.description"
            :rows="4"
            maxlength="200"
            show-word-limit
            type="textarea"
          />
        </el-form-item>
        <el-form-item label="角色状态" prop="status">
          <el-radio-group v-model="roleForm.status">
            <el-radio value="valid">启用</el-radio>
            <el-radio value="disabled">停用</el-radio>
          </el-radio-group>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button :disabled="submitting" @click="roleDialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="submitting" @click="submitRoleForm">保存</el-button>
      </template>
    </el-dialog>

    <el-dialog
      v-model="permissionDialogVisible"
      :close-on-click-modal="false"
      title="分配权限"
      width="620px"
    >
      <template v-if="permissionTarget">
        <p class="permission-dialog-description">
          为“{{ permissionTarget.name }}”选择权限；保存将提交当前完整勾选集合。
        </p>
        <div class="permission-toolbar">
          <el-button
            :disabled="permissionLoading || permissionSubmitting"
            @click="selectAllPermissions"
          >
            全选
          </el-button>
          <el-button
            :disabled="permissionLoading || permissionSubmitting"
            @click="clearPermissions"
          >
            清空
          </el-button>
          <el-button
            :disabled="permissionSubmitting"
            :icon="Refresh"
            :loading="permissionLoading"
            @click="loadRolePermissionAssignment"
          >
            重新加载
          </el-button>
        </div>
        <el-skeleton v-if="permissionLoading" :rows="6" animated />
        <el-alert
          v-else-if="permissionError"
          :closable="false"
          show-icon
          :title="permissionError"
          type="error"
        >
          <template #default>
            <el-button link type="primary" @click="loadRolePermissionAssignment"
              >重新加载</el-button
            >
          </template>
        </el-alert>
        <el-empty v-else-if="!permissionTree.length" description="暂无可分配的权限" />
        <el-tree
          v-else
          ref="permissionTreeRef"
          class="permission-tree"
          :data="permissionTree"
          default-expand-all
          node-key="id"
          show-checkbox
          @check="syncSelectedPermissions"
        />
        <el-alert
          v-if="!permissionLoading && !permissionError && !selectedPermissionIds.length"
          class="permission-empty-hint"
          :closable="false"
          show-icon
          title="当前接口要求 permission_ids 至少包含一个权限；清空后无法保存。"
          type="warning"
        />
      </template>
      <template #footer>
        <el-button :disabled="permissionSubmitting" @click="permissionDialogVisible = false"
          >取消</el-button
        >
        <el-button
          type="primary"
          :disabled="Boolean(permissionError) || permissionLoading || !selectedPermissionIds.length"
          :loading="permissionSubmitting"
          @click="submitPermissionAssignment"
        >
          保存权限
        </el-button>
      </template>
    </el-dialog>
  </PageContainer>
</template>

<style scoped>
.role-search-card {
  margin-bottom: 16px;
}

.role-search-card :deep(.el-card__body) {
  padding-bottom: 2px;
}

.role-table-card :deep(.el-card__body) {
  padding: 0;
}

.role-request-error {
  margin: 16px 16px 0;
}

.role-pagination {
  display: flex;
  justify-content: flex-end;
  padding: 16px 20px;
}

.permission-dialog-description {
  margin: 0 0 14px;
  color: #606266;
  font-size: 14px;
}

.permission-toolbar {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
  margin-bottom: 16px;
}

.permission-tree {
  max-height: 360px;
  overflow: auto;
  border: 1px solid #ebeef5;
  border-radius: 4px;
  padding: 12px;
}

.permission-empty-hint {
  margin-top: 16px;
}

@media (max-width: 720px) {
  .role-pagination {
    justify-content: center;
  }
}
</style>
