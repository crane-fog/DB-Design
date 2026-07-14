<script setup lang="ts">
import { RouterLink, RouterView, useRoute } from 'vue-router'
import { computed, ref, watch } from 'vue'
import { router } from '@/router'
import { useAuthStore } from '@/stores/auth'

interface MenuItem {
  module: string
  name: string
  path: string
  title: string
}

interface MenuGroup {
  items: MenuItem[]
  module: string
  title: string
  path: string
}

const route = useRoute()
const auth = useAuthStore()
const collapsed = ref(false)
const expandedModules = ref<string[]>([])

const menuGroups = computed<MenuGroup[]>(() => {
  const items = router
    .getRoutes()
    .filter((record) => record.meta.showInMenu && record.meta.module && record.name)
    .map((record) => ({
      isModule: Boolean(record.meta.isModule),
      module: record.meta.module as string,
      name: String(record.name),
      pageOrder: record.meta.pageOrder ?? Number.MAX_SAFE_INTEGER,
      path: record.path,
      title: record.meta.title ?? record.path,
    }))

  const groups = new Map<string, MenuGroup>()
  for (const item of items) {
    const group: MenuGroup = groups.get(item.module) ?? {
      items: [],
      module: item.module,
      path: item.path,
      title: item.title,
    }
    if (item.isModule) {
      group.path = item.path
      group.title = item.title
    } else {
      group.items.push(item)
    }
    groups.set(item.module, group)
  }
  return [...groups.values()]
})

const activeModule = computed(() => route.meta.module as string | undefined)
const breadcrumbTitle = computed(() => route.meta.title ?? '工作台')
const userDisplayName = computed(
  () => auth.currentUser?.name || auth.currentUser?.employeeNo || '已登录用户',
)

function isExpanded(module: string) {
  return expandedModules.value.includes(module)
}

function toggleModule(module: string) {
  if (isExpanded(module)) {
    expandedModules.value = expandedModules.value.filter((item) => item !== module)
    return
  }

  expandedModules.value = [...expandedModules.value, module]
}

function isActive(path: string) {
  return route.path === path
}

watch(
  activeModule,
  (module) => {
    if (module && !isExpanded(module)) {
      expandedModules.value = [...expandedModules.value, module]
    }
  },
  { immediate: true },
)
</script>

<template>
  <div class="admin-layout" :class="{ 'admin-layout--collapsed': collapsed }">
    <aside class="admin-sidebar" aria-label="主导航">
      <RouterLink class="admin-brand" to="/">
        <span class="admin-brand-mark">IM</span>
        <span v-if="!collapsed">工业制造物料管理系统</span>
      </RouterLink>

      <nav class="admin-nav">
        <div v-for="group in menuGroups" :key="group.module" class="admin-nav-group">
          <div
            class="admin-nav-row"
            :class="{ active: activeModule === group.module && isActive(group.path) }"
          >
            <RouterLink
              class="admin-nav-link"
              :to="group.path"
              :title="collapsed ? group.title : undefined"
            >
              <span class="admin-nav-icon" aria-hidden="true">{{ group.title.slice(0, 1) }}</span>
              <span v-if="!collapsed">{{ group.title }}</span>
            </RouterLink>
            <button
              v-if="group.items.length && !collapsed"
              class="admin-nav-toggle"
              type="button"
              :aria-label="`${isExpanded(group.module) ? '收起' : '展开'}${group.title}`"
              :aria-expanded="isExpanded(group.module)"
              @click="toggleModule(group.module)"
            >
              {{ isExpanded(group.module) ? '⌃' : '⌄' }}
            </button>
          </div>
          <div
            v-if="group.items.length && isExpanded(group.module) && !collapsed"
            class="admin-sub-nav"
          >
            <RouterLink
              v-for="item in group.items"
              :key="item.name"
              class="admin-sub-nav-link"
              :class="{ active: isActive(item.path) }"
              :to="item.path"
            >
              {{ item.title }}
            </RouterLink>
          </div>
        </div>
      </nav>
    </aside>

    <div class="admin-main">
      <header class="admin-header">
        <button
          class="sidebar-toggle"
          type="button"
          :aria-label="collapsed ? '展开侧边栏' : '收起侧边栏'"
          @click="collapsed = !collapsed"
        >
          {{ collapsed ? '☰' : '‹' }}
        </button>
        <div class="breadcrumb" aria-label="面包屑">
          <RouterLink to="/">工作台</RouterLink><span>/</span><span>{{ breadcrumbTitle }}</span>
        </div>
        <div class="user-summary">
          <span class="user-avatar" aria-hidden="true">{{ userDisplayName.slice(0, 1) }}</span>
          <span>{{ userDisplayName }}</span>
          <button class="logout-button" type="button" @click="auth.logout()">退出登录</button>
        </div>
      </header>
      <main class="admin-content"><RouterView /></main>
    </div>
  </div>
</template>
