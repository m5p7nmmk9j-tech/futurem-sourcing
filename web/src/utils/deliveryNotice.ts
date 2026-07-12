export type DeliveryLineBalance = {
  plannedQuantity?: number | string | null
  plannedCartons?: number | string | null
  receivedQuantity?: number | string | null
  receivedCartons?: number | string | null
}

export type ReceivingDraftLine = {
  deliveryNoticeLineId?: number | null
  quantity?: number | string | null
  cartons?: number | string | null
  availableQuantity?: number | string | null
  availableCartons?: number | string | null
}

export function deliveryNoticeStatusLabel(status: string | null | undefined): string {
  return ({
    draft: '草稿',
    published: '已发布',
    supplier_confirmed: '供应商已确认',
    partially_received: '部分收货',
    received: '已收齐',
    closed: '已关闭',
    cancelled: '已取消',
  } as Record<string, string>)[status || ''] || status || '草稿'
}

export function remainingDeliveryLine(line: DeliveryLineBalance): { quantity: number; cartons: number } {
  return {
    quantity: Math.max(0, Number(line.plannedQuantity || 0) - Number(line.receivedQuantity || 0)),
    cartons: Math.max(0, Number(line.plannedCartons || 0) - Number(line.receivedCartons || 0)),
  }
}

export function validateReceivingDraft(lines: ReceivingDraftLine[]): string[] {
  const errors: string[] = []
  if (!lines.length) return ['请至少填写一条实际到货商品']

  lines.forEach((line, index) => {
    const row = index + 1
    const quantity = Number(line.quantity || 0)
    const cartons = Number(line.cartons || 0)
    const availableQuantity = Number(line.availableQuantity || 0)
    const availableCartons = Number(line.availableCartons || 0)

    if (!line.deliveryNoticeLineId) errors.push(`第 ${row} 行缺少送货通知商品`)
    else if (quantity <= 0 || cartons <= 0) errors.push(`第 ${row} 行实际到货数量和箱数必须大于 0`)
    else if (quantity > availableQuantity || cartons > availableCartons) errors.push(`第 ${row} 行实际到货数量超过剩余计划`)
  })

  return errors
}
