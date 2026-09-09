<script setup lang="ts">
import { computed } from 'vue'
import { buildTextDiff, type DiffToken, type TextDiffRow } from '@/utils/text-diff'

const { afterText, beforeText } = defineProps<{
  afterText: string
  beforeText: string
}>()

const result = computed(() => buildTextDiff(beforeText, afterText))

function beforeMarker(row: TextDiffRow) {
  if (row.type === 'modified' || row.type === 'removed') {
    return '-'
  }
  return ''
}

function afterMarker(row: TextDiffRow) {
  if (row.type === 'added' || row.type === 'modified') {
    return '+'
  }
  return ''
}

function tokenClass(token: DiffToken) {
  return `diff-token--${token.type}`
}
</script>

<template>
  <div class="audit-text-diff">
    <div class="diff-toolbar">
      <div v-if="result.hasChanges" class="diff-summary" aria-label="差异统计">
        <el-tag v-if="result.modifiedCount" effect="plain" size="small" type="warning">
          修改 {{ result.modifiedCount }} 行
        </el-tag>
        <el-tag v-if="result.removedCount" effect="plain" size="small" type="danger">
          删除 {{ result.removedCount }} 行
        </el-tag>
        <el-tag v-if="result.addedCount" effect="plain" size="small" type="success">
          新增 {{ result.addedCount }} 行
        </el-tag>
      </div>
      <el-tag v-else effect="plain" size="small" type="info">操作前后数据无差异</el-tag>
      <span v-if="result.limited" class="diff-limited-hint"> 数据量较大，已使用简化差异展示 </span>
    </div>

    <div class="diff-scroll" tabindex="0">
      <table class="diff-table" aria-label="操作前后数据文本差异">
        <thead>
          <tr>
            <th scope="col">操作前数据</th>
            <th scope="col">操作后数据</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="(row, rowIndex) in result.rows" :key="rowIndex">
            <td
              class="diff-side"
              :class="{
                'diff-side--empty': row.beforeLineNumber === undefined,
                'diff-side--removed': row.type === 'modified' || row.type === 'removed',
              }"
            >
              <div class="diff-line">
                <span class="diff-line-number">{{ row.beforeLineNumber ?? '' }}</span>
                <span class="diff-marker" aria-hidden="true">{{ beforeMarker(row) }}</span>
                <code class="diff-code">
                  <span
                    v-for="(token, tokenIndex) in row.beforeTokens"
                    :key="tokenIndex"
                    :class="tokenClass(token)"
                    >{{ token.text }}</span
                  >
                </code>
              </div>
            </td>
            <td
              class="diff-side"
              :class="{
                'diff-side--added': row.type === 'added' || row.type === 'modified',
                'diff-side--empty': row.afterLineNumber === undefined,
              }"
            >
              <div class="diff-line">
                <span class="diff-line-number">{{ row.afterLineNumber ?? '' }}</span>
                <span class="diff-marker" aria-hidden="true">{{ afterMarker(row) }}</span>
                <code class="diff-code">
                  <span
                    v-for="(token, tokenIndex) in row.afterTokens"
                    :key="tokenIndex"
                    :class="tokenClass(token)"
                    >{{ token.text }}</span
                  >
                </code>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>

<style scoped>
.diff-toolbar {
  display: flex;
  min-height: 28px;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 10px;
}

.diff-summary {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.diff-limited-hint {
  color: var(--el-text-color-secondary);
  font-size: 12px;
}

.diff-scroll {
  max-height: min(420px, calc(90vh - 300px));
  overflow: auto;
  border: 1px solid var(--el-border-color);
  border-radius: var(--el-border-radius-base);
  outline: none;
}

.diff-scroll:focus-visible {
  box-shadow: 0 0 0 2px var(--el-color-primary-light-5);
}

.diff-table {
  width: 100%;
  min-width: 760px;
  table-layout: fixed;
  border-spacing: 0;
  border-collapse: separate;
  color: var(--el-text-color-primary);
  font-size: 13px;
}

.diff-table th {
  position: sticky;
  z-index: 2;
  top: 0;
  padding: 9px 12px;
  border-bottom: 1px solid var(--el-border-color);
  background: var(--el-fill-color-light);
  font-weight: 600;
  text-align: left;
}

.diff-table th + th,
.diff-table td + td {
  border-left: 1px solid var(--el-border-color);
}

.diff-side {
  padding: 0;
  background: var(--el-bg-color);
  vertical-align: top;
}

.diff-side--removed {
  background: var(--el-color-danger-light-9);
  box-shadow: inset 3px 0 0 var(--el-color-danger);
}

.diff-side--added {
  background: var(--el-color-success-light-9);
  box-shadow: inset 3px 0 0 var(--el-color-success);
}

.diff-side--empty {
  background: var(--el-fill-color-lighter);
  box-shadow: none;
}

.diff-line {
  display: grid;
  min-height: 20px;
  grid-template-columns: 48px 22px minmax(0, 1fr);
  align-items: stretch;
}

.diff-line-number,
.diff-marker {
  display: flex;
  align-items: flex-start;
  justify-content: flex-end;
  padding-top: 1px;
  color: var(--el-text-color-placeholder);
  line-height: 18px;
  user-select: none;
}

.diff-line-number {
  padding-right: 9px;
  border-right: 1px solid var(--el-border-color-lighter);
  font-variant-numeric: tabular-nums;
}

.diff-marker {
  justify-content: center;
  padding-right: 0;
  font-weight: 700;
}

.diff-side--removed .diff-marker {
  color: var(--el-color-danger);
}

.diff-side--added .diff-marker {
  color: var(--el-color-success);
}

.diff-code {
  display: block;
  min-width: 0;
  min-height: 18px;
  padding: 1px 10px;
  font-family: Consolas, 'Courier New', monospace;
  line-height: 18px;
  overflow-wrap: anywhere;
  white-space: pre-wrap;
}

.diff-token--removed {
  border-radius: 2px;
  background: var(--el-color-danger-light-7);
  color: var(--el-color-danger-dark-2);
}

.diff-token--added {
  border-radius: 2px;
  background: var(--el-color-success-light-7);
  color: var(--el-color-success-dark-2);
}

@media (max-width: 720px) {
  .diff-toolbar {
    align-items: flex-start;
    flex-direction: column;
  }

  .diff-table {
    min-width: 680px;
  }
}
</style>
