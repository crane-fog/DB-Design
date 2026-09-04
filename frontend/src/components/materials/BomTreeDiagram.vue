<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'
import type { BomTreeNode } from '@/types/material'
import { formatNumber } from '@/utils/format'

interface PositionedNode {
  data: BomTreeNode
  depth: number
  height: number
  left: number
  parentPath?: string
  top: number
}

interface TreeEdge {
  key: string
  pathDefinition: string
}

const { defaultExpanded, nodes } = defineProps<{
  defaultExpanded: boolean
  nodes: BomTreeNode[]
}>()

const nodeWidth = 80
const defaultNodeHeight = 52
const siblingGap = 10
const levelGap = 38
const stagePadding = 14
const minZoom = 0.5
const maxZoom = 2
const zoomStep = 0.05
const collapsedPaths = ref<Set<string>>(new Set())
const zoom = ref(1)
const measuredNodeHeights = reactive(new Map<string, number>())
const nodeElements = new Map<string, HTMLElement>()
let nodeResizeObserver: ResizeObserver | undefined = undefined

function collectBranchPaths(treeNodes: BomTreeNode[], paths: string[] = []) {
  treeNodes.forEach((node) => {
    if (node.children.length) {
      paths.push(node.path)
      collectBranchPaths(node.children, paths)
    }
  })
  return paths
}

function countTreeNodes(treeNodes: BomTreeNode[]): number {
  return treeNodes.reduce((total, node) => total + 1 + countTreeNodes(node.children), 0)
}

watch(
  [() => nodes, () => defaultExpanded],
  ([currentNodes, expandByDefault]) => {
    if (expandByDefault) {
      collapsedPaths.value = new Set()
      return
    }
    collapsedPaths.value = new Set(collectBranchPaths(currentNodes))
  },
  { immediate: true },
)

function toggleNode(node: BomTreeNode) {
  if (!node.children.length) {
    return
  }
  const next = new Set(collapsedPaths.value)
  if (next.has(node.path)) {
    next.delete(node.path)
  } else {
    next.add(node.path)
  }
  collapsedPaths.value = next
}

function updateNodeHeight(nodePath: string, height: number) {
  const roundedHeight = Math.ceil(height)
  if (roundedHeight > 0 && measuredNodeHeights.get(nodePath) !== roundedHeight) {
    measuredNodeHeights.set(nodePath, roundedHeight)
  }
}

function setNodeElement(nodePath: string, element: unknown) {
  const previous = nodeElements.get(nodePath)
  if (previous === element) {
    return
  }
  if (previous) {
    nodeResizeObserver?.unobserve(previous)
  }
  if (!(element instanceof HTMLElement)) {
    nodeElements.delete(nodePath)
    return
  }
  nodeElements.set(nodePath, element)
  updateNodeHeight(nodePath, element.offsetHeight)
  nodeResizeObserver?.observe(element)
}

onMounted(() => {
  nodeResizeObserver = new ResizeObserver((entries) => {
    entries.forEach((entry) => {
      const element = entry.target as HTMLElement
      const { nodePath } = element.dataset
      if (nodePath) {
        updateNodeHeight(nodePath, element.offsetHeight)
      }
    })
  })
  nodeElements.forEach((element) => nodeResizeObserver?.observe(element))
})

onBeforeUnmount(() => nodeResizeObserver?.disconnect())

const treeLayout = computed(() => {
  const positionedNodes: PositionedNode[] = []
  let nextLeafLeft = stagePadding
  let maxDepth = 0

  function positionNode(node: BomTreeNode, depth: number, parentPath?: string): PositionedNode {
    const positioned: PositionedNode = {
      data: node,
      depth,
      height: measuredNodeHeights.get(node.path) ?? defaultNodeHeight,
      left: 0,
      parentPath,
      top: 0,
    }
    positionedNodes.push(positioned)
    maxDepth = Math.max(maxDepth, depth)

    let visibleChildren = node.children
    if (collapsedPaths.value.has(node.path)) {
      visibleChildren = []
    }
    if (!visibleChildren.length) {
      positioned.left = nextLeafLeft
      nextLeafLeft += nodeWidth + siblingGap
      return positioned
    }

    const children = visibleChildren.map((child) => positionNode(child, depth + 1, node.path))
    positioned.left = (children[0]!.left + children[children.length - 1]!.left) / 2
    return positioned
  }

  nodes.forEach((node) => positionNode(node, 0))
  const rowHeights = new Map<number, number>()
  positionedNodes.forEach((node) => {
    rowHeights.set(node.depth, Math.max(rowHeights.get(node.depth) ?? 0, node.height))
  })
  const rowTops = new Map<number, number>()
  let nextRowTop = stagePadding
  for (let depth = 0; depth <= maxDepth; depth += 1) {
    rowTops.set(depth, nextRowTop)
    nextRowTop += (rowHeights.get(depth) ?? defaultNodeHeight) + levelGap
  }
  positionedNodes.forEach((node) => {
    node.top = rowTops.get(node.depth) ?? stagePadding
  })
  const byPath = new Map(positionedNodes.map((node) => [node.data.path, node]))
  const edges: TreeEdge[] = []
  positionedNodes.forEach((node) => {
    if (!node.parentPath) {
      return
    }
    const parent = byPath.get(node.parentPath)
    if (!parent) {
      return
    }
    const startX = parent.left + nodeWidth / 2
    const startY = parent.top + parent.height
    const endX = node.left + nodeWidth / 2
    const endY = node.top
    const curveY = (startY + endY) / 2
    edges.push({
      key: `${parent.data.path}-${node.data.path}`,
      pathDefinition: `M ${startX} ${startY} C ${startX} ${curveY}, ${endX} ${curveY}, ${endX} ${endY}`,
    })
  })

  return {
    edges,
    height: nextRowTop - levelGap + stagePadding,
    nodes: positionedNodes,
    width: Math.max(nodeWidth + stagePadding * 2, nextLeafLeft - siblingGap + stagePadding),
  }
})

const totalNodeCount = computed(() => countTreeNodes(nodes))
const zoomPercent = computed(() => Math.round(zoom.value * 100))
const scaledTreeSize = computed(() => ({
  height: `${Math.ceil(treeLayout.value.height * zoom.value)}px`,
  width: `${Math.ceil(treeLayout.value.width * zoom.value)}px`,
}))

function setZoom(nextZoom: number) {
  const constrainedZoom = Math.min(maxZoom, Math.max(minZoom, nextZoom))
  zoom.value = Math.round(constrainedZoom * 100) / 100
}

function zoomIn() {
  setZoom(zoom.value + zoomStep)
}

function zoomOut() {
  setZoom(zoom.value - zoomStep)
}

function resetZoom() {
  zoom.value = 1
}

function handleCanvasWheel(event: WheelEvent) {
  if (!event.ctrlKey) {
    return
  }
  event.preventDefault()
  if (event.deltaY > 0) {
    zoomOut()
    return
  }
  zoomIn()
}
</script>

<template>
  <div class="bom-diagram">
    <div class="bom-diagram__summary">
      <span>共 {{ totalNodeCount }} 个节点，当前显示 {{ treeLayout.nodes.length }} 个</span>
      <div class="bom-diagram__tools">
        <span>点击节点收起分支；Ctrl + 滚轮缩放</span>
        <div aria-label="画布缩放" class="bom-diagram__zoom" role="group">
          <button :disabled="zoom <= minZoom" title="缩小" type="button" @click="zoomOut">−</button>
          <button title="恢复 100%" type="button" @click="resetZoom">{{ zoomPercent }}%</button>
          <button :disabled="zoom >= maxZoom" title="放大" type="button" @click="zoomIn">+</button>
        </div>
      </div>
    </div>
    <div class="bom-diagram__viewport" @wheel="handleCanvasWheel">
      <div class="bom-diagram__canvas" :style="scaledTreeSize">
        <div
          class="bom-diagram__stage"
          :style="{
            height: `${treeLayout.height}px`,
            transform: `scale(${zoom})`,
            width: `${treeLayout.width}px`,
          }"
        >
          <svg
            aria-hidden="true"
            class="bom-diagram__edges"
            :height="treeLayout.height"
            :viewBox="`0 0 ${treeLayout.width} ${treeLayout.height}`"
            :width="treeLayout.width"
          >
            <path v-for="edge in treeLayout.edges" :key="edge.key" :d="edge.pathDefinition" />
          </svg>

          <button
            v-for="node in treeLayout.nodes"
            :key="node.data.path"
            :aria-expanded="
              node.data.children.length ? !collapsedPaths.has(node.data.path) : undefined
            "
            :ref="(element) => setNodeElement(node.data.path, element)"
            class="bom-diagram-node"
            :class="{
              'bom-diagram-node--branch': node.data.children.length,
              'bom-diagram-node--collapsed': collapsedPaths.has(node.data.path),
              'bom-diagram-node--leaf': !node.data.children.length,
              'bom-diagram-node--root': node.data.level === 0,
            }"
            :style="{
              transform: `translate(${node.left}px, ${node.top}px)`,
              width: `${nodeWidth}px`,
            }"
            :data-node-path="node.data.path"
            :title="`${node.data.materialName}（${node.data.materialCode}）· 用量 ${formatNumber(node.data.quantity)} ${node.data.unit}`"
            type="button"
            @click="toggleNode(node.data)"
          >
            <strong>{{ node.data.materialName }}</strong>
            <span class="bom-diagram-node__meta">
              <small>#{{ node.data.materialCode }}</small>
              <span class="bom-diagram-node__quantity"
                >×{{ formatNumber(node.data.quantity) }} {{ node.data.unit }}</span
              >
            </span>
            <span v-if="node.data.children.length" class="bom-diagram-node__toggle">
              {{ collapsedPaths.has(node.data.path) ? '+' : '−' }}
            </span>
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.bom-diagram {
  min-width: 0;
}

.bom-diagram__summary {
  display: flex;
  justify-content: space-between;
  gap: 16px;
  margin: 12px 2px 8px;
  color: var(--el-text-color-secondary);
  font-size: 13px;
}

.bom-diagram__tools,
.bom-diagram__zoom {
  display: flex;
  align-items: center;
}

.bom-diagram__tools {
  gap: 10px;
}

.bom-diagram__zoom {
  flex: none;
  overflow: hidden;
  border: 1px solid var(--el-border-color);
  border-radius: 6px;
  background: rgba(255, 255, 255, 0.85);
}

.bom-diagram__zoom button {
  min-width: 26px;
  height: 24px;
  padding: 0 6px;
  color: var(--el-text-color-regular);
  font: inherit;
  cursor: pointer;
  border: 0;
  border-right: 1px solid var(--el-border-color-lighter);
  background: transparent;
}

.bom-diagram__zoom button:nth-child(2) {
  min-width: 48px;
  color: var(--el-color-primary);
}

.bom-diagram__zoom button:last-child {
  border-right: 0;
}

.bom-diagram__zoom button:hover:not(:disabled) {
  background: var(--el-color-primary-light-9);
}

.bom-diagram__zoom button:disabled {
  color: var(--el-disabled-text-color);
  cursor: not-allowed;
}

.bom-diagram__viewport {
  max-height: min(68vh, 820px);
  overflow: auto;
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 12px;
  background:
    linear-gradient(rgba(64, 158, 255, 0.045) 1px, transparent 1px),
    linear-gradient(90deg, rgba(64, 158, 255, 0.045) 1px, transparent 1px),
    rgba(255, 255, 255, 0.62);
  background-size: 20px 20px;
}

.bom-diagram__canvas {
  position: relative;
  margin: 0 auto;
  transition:
    width 0.16s ease,
    height 0.16s ease;
}

.bom-diagram__stage {
  position: absolute;
  top: 0;
  left: 0;
  transform-origin: top left;
  transition: transform 0.16s ease;
}

.bom-diagram__edges {
  position: absolute;
  inset: 0;
  overflow: visible;
  pointer-events: none;
}

.bom-diagram__edges path {
  fill: none;
  stroke: #86b7ff;
  stroke-linecap: round;
  stroke-width: 1.5;
}

.bom-diagram-node {
  position: absolute;
  top: 0;
  left: 0;
  box-sizing: border-box;
  display: flex;
  flex-direction: column;
  gap: 2px;
  justify-content: center;
  overflow: hidden;
  padding: 5px 6px;
  color: var(--el-text-color-primary);
  text-align: left;
  cursor: default;
  border: 1px solid rgba(64, 158, 255, 0.34);
  border-top: 2px solid var(--el-color-primary);
  border-radius: 8px;
  background: rgba(255, 255, 255, 0.96);
  box-shadow: 0 5px 14px rgba(35, 83, 153, 0.11);
  transition:
    border-color 0.2s ease,
    box-shadow 0.2s ease;
}

.bom-diagram-node--branch {
  cursor: pointer;
}

.bom-diagram-node--branch:hover,
.bom-diagram-node--branch:focus-visible {
  border-color: var(--el-color-primary);
  outline: none;
  box-shadow: 0 7px 18px rgba(35, 83, 153, 0.18);
}

.bom-diagram-node--root {
  color: #fff;
  border-color: #337ecc;
  background: linear-gradient(135deg, #409eff, #337ecc);
}

.bom-diagram-node--leaf {
  border-top-color: var(--el-color-success);
}

.bom-diagram-node--collapsed {
  border-top-color: var(--el-color-warning);
}

.bom-diagram-node__meta {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 2px;
  width: 100%;
  min-width: 0;
  overflow: hidden;
  color: var(--el-text-color-secondary);
  font-size: 9px;
  line-height: 11px;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.bom-diagram-node__meta small {
  flex: 1 1 auto;
  min-width: 0;
  overflow: hidden;
  color: inherit;
  font-size: inherit;
  line-height: inherit;
  text-overflow: ellipsis;
}

.bom-diagram-node__quantity {
  flex: none;
  max-width: 100%;
  font-size: 10px;
  white-space: nowrap;
}

.bom-diagram-node--root .bom-diagram-node__meta {
  color: rgba(255, 255, 255, 0.82);
}

.bom-diagram-node__toggle {
  position: absolute;
  top: 3px;
  right: 3px;
  display: grid;
  width: 13px;
  height: 13px;
  place-items: center;
  color: var(--el-color-primary);
  font-size: 12px;
  line-height: 1;
  border-radius: 50%;
  background: var(--el-color-primary-light-9);
}

.bom-diagram-node--root .bom-diagram-node__toggle {
  color: #fff;
  background: rgba(255, 255, 255, 0.2);
}

.bom-diagram-node strong {
  display: block;
  font-size: 11px;
  line-height: 13px;
  overflow-wrap: anywhere;
  white-space: normal;
}

.bom-diagram-node--branch strong {
  padding-right: 9px;
}

@media (max-width: 720px) {
  .bom-diagram__summary {
    flex-direction: column;
    gap: 4px;
  }

  .bom-diagram__tools {
    align-items: flex-start;
    flex-direction: column;
    gap: 6px;
  }
}
</style>
