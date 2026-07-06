<script setup lang="ts">
import { type ModuleKey, pages } from '@/router'
import { RouterLink, RouterView, useRoute } from 'vue-router'
import { computed, ref, watch } from 'vue'

const route = useRoute()
const expandedPageKeys = ref<ModuleKey[]>([])

const activeModuleKey = computed(() => route.meta.moduleKey as ModuleKey | undefined)
const activeSubPageKey = computed(() => route.meta.subPageKey as string | undefined)

function isActiveModule(key: ModuleKey) {
  return activeModuleKey.value === key
}

function isActiveModuleOverview(key: ModuleKey) {
  return activeModuleKey.value === key && !activeSubPageKey.value
}

function isActiveSubPage(key: string) {
  return activeSubPageKey.value === key
}

function isPageExpanded(key: ModuleKey) {
  return expandedPageKeys.value.includes(key)
}

function togglePage(key: ModuleKey) {
  if (isPageExpanded(key)) {
    expandedPageKeys.value = expandedPageKeys.value.filter((pageKey) => pageKey !== key)
    return
  }

  expandedPageKeys.value = [...expandedPageKeys.value, key]
}

watch(
  activeModuleKey,
  (key) => {
    if (key && !isPageExpanded(key)) {
      expandedPageKeys.value = [...expandedPageKeys.value, key]
    }
  },
  { immediate: true },
)
</script>

<template>
  <div class="app-shell">
    <aside class="sidebar" aria-label="主导航">
      <div class="brand">
        <RouterLink class="brand-link" to="/">
          <p class="brand-title">工业制造物料管理系统</p>
        </RouterLink>
      </div>

      <nav class="nav-list">
        <div v-for="page in pages" :key="page.key" class="nav-group">
          <div class="nav-row" :class="{ active: isActiveModuleOverview(page.key) }">
            <RouterLink class="nav-item" :to="page.path">
              <span>{{ page.title }}</span>
            </RouterLink>

            <button
              v-if="page.subPages.length"
              type="button"
              class="nav-toggle"
              :aria-expanded="isPageExpanded(page.key)"
              :aria-label="`${isPageExpanded(page.key) ? '折叠' : '展开'}${page.title}子页面`"
              @click="togglePage(page.key)"
            >
              <span class="nav-arrow" :class="{ expanded: isPageExpanded(page.key) }">›</span>
            </button>
          </div>

          <div v-if="page.subPages.length && isPageExpanded(page.key)" class="sub-nav">
            <RouterLink
              v-for="subPage in page.subPages"
              :key="subPage.key"
              class="sub-nav-item"
              :class="{ active: isActiveModule(page.key) && isActiveSubPage(subPage.key) }"
              :to="subPage.path"
            >
              {{ subPage.title }}
            </RouterLink>
          </div>
        </div>
      </nav>
    </aside>

    <main class="content">
      <RouterView />
    </main>
  </div>
</template>
