<script setup lang="ts">
import {
  Box,
  Connection,
  Goods,
  Menu as MenuIcon,
  Operation,
  Setting,
  ShoppingCart,
} from '@element-plus/icons-vue'
import { type Component, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { router } from '@/router'
import { useAuthStore } from '@/stores/auth'

interface MenuItem {
  name: string
  order: number
  path: string
  title: string
}

interface MenuGroup {
  icon: Component
  items: MenuItem[]
  module: string
  order: number
  path: string
  title: string
}

const { collapsed } = defineProps<{ collapsed: boolean }>()
const auth = useAuthStore()
const route = useRoute()
const currentRouter = useRouter()

const moduleIcons: Record<string, Component> = {
  inventory: Goods,
  materials: Box,
  production: Operation,
  purchase: ShoppingCart,
  system: Setting,
  trace: Connection,
}

function canAccessMenu(permission?: unknown) {
  if (typeof permission !== 'string' || !permission) {
    return true
  }

  return auth.hasPermission(permission)
}

function insertByOrder<ItemWithOrder extends { order: number }>(
  collection: ItemWithOrder[],
  item: ItemWithOrder,
) {
  const insertionIndex = collection.findIndex((currentItem) => currentItem.order > item.order)
  if (insertionIndex === -1) {
    collection.push(item)
    return
  }

  collection.splice(insertionIndex, 0, item)
}

const menuGroups = computed<MenuGroup[]>(() => {
  const routeRecords = router.getRoutes()
  const moduleRoutes = new Map(
    routeRecords
      .filter((record) => record.meta.isModule && record.meta.module)
      .map((record) => [String(record.meta.module), record]),
  )
  const items = routeRecords
    .filter(
      (record) =>
        record.meta.showInMenu &&
        record.meta.module &&
        record.name &&
        canAccessMenu(record.meta.permission),
    )
    .map((record) => ({
      isModule: Boolean(record.meta.isModule),
      module: String(record.meta.module),
      name: String(record.name),
      order: Number(record.meta.pageOrder ?? Number.MAX_SAFE_INTEGER),
      path: record.path,
      title: String(record.meta.title ?? record.path),
    }))

  const groups = new Map<string, MenuGroup>()
  for (const item of items) {
    const moduleRoute = moduleRoutes.get(item.module)
    const group = groups.get(item.module) ?? {
      icon: moduleIcons[item.module] ?? MenuIcon,
      items: [],
      module: item.module,
      order: item.order,
      path: moduleRoute?.path ?? item.path,
      title: String(moduleRoute?.meta.title ?? item.title),
    }

    group.order = Math.min(group.order, item.order)
    if (item.isModule) {
      group.path = item.path
      group.title = item.title
    } else {
      insertByOrder(group.items, item)
    }
    groups.set(item.module, group)
  }

  const orderedGroups: MenuGroup[] = []
  for (const group of groups.values()) {
    insertByOrder(orderedGroups, group)
  }
  return orderedGroups
})

const activePath = computed(() => route.path)

function navigateToModule(path: string) {
  if (route.path !== path) {
    void currentRouter.push(path)
  }
}
</script>

<template>
  <aside class="admin-sidebar" aria-label="主导航">
    <el-menu
      class="sidebar-menu"
      :collapse="collapsed"
      :collapse-transition="false"
      :default-active="activePath"
      :unique-opened="true"
      router
    >
      <template v-for="group in menuGroups" :key="group.module">
        <el-sub-menu v-if="group.items.length" :index="group.module">
          <template #title>
            <el-icon><component :is="group.icon" /></el-icon>
            <span class="sidebar-module-title" @click="navigateToModule(group.path)">
              {{ group.title }}
            </span>
          </template>
          <el-menu-item v-for="item in group.items" :key="item.name" :index="item.path">
            {{ item.title }}
          </el-menu-item>
        </el-sub-menu>
        <el-menu-item v-else :index="group.path">
          <el-icon><component :is="group.icon" /></el-icon>
          <template #title>{{ group.title }}</template>
        </el-menu-item>
      </template>
    </el-menu>
  </aside>
</template>
