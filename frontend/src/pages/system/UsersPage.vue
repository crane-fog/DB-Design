<script setup lang="ts">
import { type PageResult, systemService } from '@/services/SystemService'
import { onMounted, ref } from 'vue'
import EmptyState from '@/components/common/EmptyState.vue'
import PageContainer from '@/components/common/PageContainer.vue'
import PageHeader from '@/components/common/PageHeader.vue'
import PageLoading from '@/components/common/PageLoading.vue'
import SearchPanel from '@/components/common/SearchPanel.vue'
import StatusTag from '@/components/common/StatusTag.vue'
import type { User } from '@/api'
import { formatDateTime } from '@/utils/format'
import { getErrorMessage } from '@/utils/error'

const filters = ref({ employeeNo: '', status: '', userName: '' })
const page = ref(1)
const pageSize = 10
const loading = ref(false)
const error = ref('')
const result = ref<PageResult<User>>({ items: [], page: 1, pageSize, total: 0 })

function selectedStatus() {
  const { status } = filters.value
  if (status === 'valid' || status === 'disabled') {
    return status
  }
  return undefined
}

async function loadUsers(targetPage = page.value) {
  loading.value = true
  error.value = ''
  try {
    result.value = await systemService.listUsers({
      employeeNo: filters.value.employeeNo,
      page: targetPage,
      pageSize,
      status: selectedStatus(),
      userName: filters.value.userName,
    })
    page.value = targetPage
  } catch (requestError) {
    error.value = getErrorMessage(requestError, '账号列表加载失败')
  } finally {
    loading.value = false
  }
}

function resetFilters() {
  filters.value = { employeeNo: '', status: '', userName: '' }
  void loadUsers(1)
}

function canGoNext() {
  return page.value * pageSize < result.value.total
}

onMounted(() => void loadUsers())
</script>

<template>
  <PageContainer>
    <PageHeader
      title="账号管理"
      description="标准列表页样板：查询、加载、空状态、错误提示与分页均由公共框架提供。"
    >
      <template #actions
        ><button class="button" type="button" disabled title="等待新增账号接口页面接入">
          新增账号（待接入）
        </button></template
      >
    </PageHeader>

    <SearchPanel>
      <label class="form-control"
        ><span>工号</span
        ><input v-model.trim="filters.employeeNo" placeholder="工号" @keyup.enter="loadUsers(1)"
      /></label>
      <label class="form-control"
        ><span>姓名</span
        ><input v-model.trim="filters.userName" placeholder="姓名" @keyup.enter="loadUsers(1)"
      /></label>
      <label class="form-control"
        ><span>状态</span
        ><select v-model="filters.status">
          <option value="">全部</option>
          <option value="valid">启用</option>
          <option value="disabled">停用</option>
        </select></label
      >
      <template #actions
        ><button class="button" type="button" :disabled="loading" @click="loadUsers(1)">查询</button
        ><button
          class="button button--secondary"
          type="button"
          :disabled="loading"
          @click="resetFilters"
        >
          重置
        </button></template
      >
    </SearchPanel>

    <section class="content-card">
      <PageLoading v-if="loading" text="正在加载账号列表..." />
      <p v-else-if="error" class="page-error">{{ error }}</p>
      <EmptyState
        v-else-if="!result.items.length"
        title="未查询到账号"
        description="暂无数据，或当前账号没有查询权限。"
      />
      <template v-else>
        <table class="data-table">
          <thead>
            <tr>
              <th>工号</th>
              <th>姓名</th>
              <th>手机号</th>
              <th>状态</th>
              <th>最近登录</th>
              <th>创建时间</th>
              <th>操作</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="user in result.items" :key="user.user_id ?? user.employee_no">
              <td>{{ user.employee_no || '-' }}</td>
              <td>{{ user.user_name || '-' }}</td>
              <td>{{ user.phone || '-' }}</td>
              <td><StatusTag :value="user.status" /></td>
              <td>{{ formatDateTime(user.last_login_time) }}</td>
              <td>{{ formatDateTime(user.created_time) }}</td>
              <td>
                <button class="table-action" type="button" disabled title="详情功能待接入">
                  查看
                </button>
              </td>
            </tr>
          </tbody>
        </table>
        <div class="pagination">
          <span>共 {{ result.total }} 条</span
          ><button
            class="button button--secondary"
            type="button"
            :disabled="page === 1"
            @click="loadUsers(page - 1)"
          >
            上一页</button
          ><span>第 {{ page }} 页</span
          ><button
            class="button button--secondary"
            type="button"
            :disabled="!canGoNext()"
            @click="loadUsers(page + 1)"
          >
            下一页
          </button>
        </div>
      </template>
    </section>
  </PageContainer>
</template>
