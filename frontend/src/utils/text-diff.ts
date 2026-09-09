import { diffLines, diffWordsWithSpace } from 'diff'

export type DiffRowType = 'added' | 'modified' | 'removed' | 'unchanged'
export type DiffTokenType = 'added' | 'normal' | 'removed'

export interface DiffToken {
  text: string
  type: DiffTokenType
}

export interface TextDiffRow {
  afterLineNumber?: number
  afterTokens: DiffToken[]
  beforeLineNumber?: number
  beforeTokens: DiffToken[]
  type: DiffRowType
}

export interface TextDiffResult {
  addedCount: number
  hasChanges: boolean
  limited: boolean
  modifiedCount: number
  removedCount: number
  rows: TextDiffRow[]
}

const maxDiffCharacters = 200_000
const lineDiffTimeout = 300
const inlineDiffTimeout = 50

function normalizeLineEndings(value: string) {
  return value.replace(/\r\n?/g, '\n')
}

function splitLines(value: string) {
  const lines = value.split('\n')
  if (value.endsWith('\n')) {
    lines.pop()
  }
  return lines
}

function splitChangeLines(value: string) {
  if (value) {
    return splitLines(value)
  }
  return []
}

function createToken(text: string, type: DiffTokenType): DiffToken {
  return { text, type }
}

function buildInlineTokens(beforeLine: string, afterLine: string) {
  const changes = diffWordsWithSpace(beforeLine, afterLine, { timeout: inlineDiffTimeout })
  if (!changes) {
    return {
      afterTokens: [createToken(afterLine, 'added')],
      beforeTokens: [createToken(beforeLine, 'removed')],
    }
  }

  const beforeTokens: DiffToken[] = []
  const afterTokens: DiffToken[] = []
  for (const change of changes) {
    if (!change.added) {
      let type: DiffTokenType = 'normal'
      if (change.removed) {
        type = 'removed'
      }
      beforeTokens.push(createToken(change.value, type))
    }
    if (!change.removed) {
      let type: DiffTokenType = 'normal'
      if (change.added) {
        type = 'added'
      }
      afterTokens.push(createToken(change.value, type))
    }
  }

  return { afterTokens, beforeTokens }
}

function appendChangedRows(
  rows: TextDiffRow[],
  changedLines: { added: string[]; removed: string[] },
  lineNumbers: { after: number; before: number },
) {
  const { added: addedLines, removed: removedLines } = changedLines
  const rowCount = Math.max(removedLines.length, addedLines.length)
  for (let index = 0; index < rowCount; index += 1) {
    const beforeLine = removedLines[index]
    const afterLine = addedLines[index]
    if (beforeLine !== undefined && afterLine !== undefined) {
      const { afterTokens, beforeTokens } = buildInlineTokens(beforeLine, afterLine)
      rows.push({
        afterLineNumber: lineNumbers.after,
        afterTokens,
        beforeLineNumber: lineNumbers.before,
        beforeTokens,
        type: 'modified',
      })
      lineNumbers.before += 1
      lineNumbers.after += 1
    } else if (beforeLine !== undefined) {
      rows.push({
        afterTokens: [],
        beforeLineNumber: lineNumbers.before,
        beforeTokens: [createToken(beforeLine, 'removed')],
        type: 'removed',
      })
      lineNumbers.before += 1
    } else if (afterLine !== undefined) {
      rows.push({
        afterLineNumber: lineNumbers.after,
        afterTokens: [createToken(afterLine, 'added')],
        beforeTokens: [],
        type: 'added',
      })
      lineNumbers.after += 1
    }
  }
}

function createResult(rows: TextDiffRow[], limited: boolean): TextDiffResult {
  let addedCount = 0
  let modifiedCount = 0
  let removedCount = 0
  for (const row of rows) {
    if (row.type === 'added') {
      addedCount += 1
    } else if (row.type === 'modified') {
      modifiedCount += 1
    } else if (row.type === 'removed') {
      removedCount += 1
    }
  }

  return {
    addedCount,
    hasChanges: addedCount + modifiedCount + removedCount > 0,
    limited,
    modifiedCount,
    removedCount,
    rows,
  }
}

function buildSimplifiedDiff(beforeText: string, afterText: string) {
  const beforeLines = splitLines(beforeText)
  const afterLines = splitLines(afterText)
  const rows: TextDiffRow[] = []
  const lineCount = Math.max(beforeLines.length, afterLines.length)

  for (let index = 0; index < lineCount; index += 1) {
    const beforeLine = beforeLines[index]
    const afterLine = afterLines[index]
    if (beforeLine === afterLine && beforeLine !== undefined) {
      rows.push({
        afterLineNumber: index + 1,
        afterTokens: [createToken(beforeLine, 'normal')],
        beforeLineNumber: index + 1,
        beforeTokens: [createToken(beforeLine, 'normal')],
        type: 'unchanged',
      })
    } else if (beforeLine !== undefined && afterLine !== undefined) {
      rows.push({
        afterLineNumber: index + 1,
        afterTokens: [createToken(afterLine, 'added')],
        beforeLineNumber: index + 1,
        beforeTokens: [createToken(beforeLine, 'removed')],
        type: 'modified',
      })
    } else if (beforeLine !== undefined) {
      rows.push({
        afterTokens: [],
        beforeLineNumber: index + 1,
        beforeTokens: [createToken(beforeLine, 'removed')],
        type: 'removed',
      })
    } else if (afterLine !== undefined) {
      rows.push({
        afterLineNumber: index + 1,
        afterTokens: [createToken(afterLine, 'added')],
        beforeTokens: [],
        type: 'added',
      })
    }
  }

  return createResult(rows, true)
}

export function buildTextDiff(beforeValue: string, afterValue: string): TextDiffResult {
  const beforeText = normalizeLineEndings(beforeValue)
  const afterText = normalizeLineEndings(afterValue)
  if (beforeText.length + afterText.length > maxDiffCharacters) {
    return buildSimplifiedDiff(beforeText, afterText)
  }

  const changes = diffLines(beforeText, afterText, { timeout: lineDiffTimeout })
  if (!changes) {
    return buildSimplifiedDiff(beforeText, afterText)
  }

  const rows: TextDiffRow[] = []
  const lineNumbers = { after: 1, before: 1 }
  let removedLines: string[] = []
  let addedLines: string[] = []

  const flushChangedLines = () => {
    appendChangedRows(rows, { added: addedLines, removed: removedLines }, lineNumbers)
    removedLines = []
    addedLines = []
  }

  for (const change of changes) {
    if (change.removed) {
      removedLines.push(...splitChangeLines(change.value))
    } else if (change.added) {
      addedLines.push(...splitChangeLines(change.value))
    } else {
      flushChangedLines()
      for (const line of splitChangeLines(change.value)) {
        rows.push({
          afterLineNumber: lineNumbers.after,
          afterTokens: [createToken(line, 'normal')],
          beforeLineNumber: lineNumbers.before,
          beforeTokens: [createToken(line, 'normal')],
          type: 'unchanged',
        })
        lineNumbers.before += 1
        lineNumbers.after += 1
      }
    }
  }
  flushChangedLines()

  return createResult(rows, false)
}
