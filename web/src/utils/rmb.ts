export function roundRmb(value: number | string | null | undefined): number {
  const numberValue = Number(value || 0)
  const absolute = Math.round((Math.abs(numberValue) + Number.EPSILON) * 100) / 100
  return numberValue < 0 ? -absolute : absolute
}

export function formatRmb(value: number | string | null | undefined): string {
  const numberValue = roundRmb(value)
  return `¥${numberValue.toLocaleString('zh-CN', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })}`
}
