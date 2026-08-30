/** 将输入字符串解析为正整数，非正整数返回 undefined。 */
export function parsePositiveInt(value: string) {
  const parsed = Number(value)
  if (Number.isInteger(parsed) && parsed > 0) {
    return parsed
  }
  return undefined
}
