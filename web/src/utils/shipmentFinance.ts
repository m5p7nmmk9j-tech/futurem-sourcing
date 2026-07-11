export function round2(value: number): number {
  return Math.round((Number(value || 0) + Number.EPSILON) * 100) / 100
}

export function normalizeExpenseName(value: string): string {
  return String(value || '').trim().toUpperCase().split(/\s+/).filter(Boolean).join(' ')
}

export function expenseOutstanding(row: any): number {
  return Math.max(
    0,
    round2(
      Number(row.amount || 0) -
      Number(row.paidAmount || 0) -
      Number(row.prepaymentAppliedAmount || 0) +
      Number(row.overpaymentTransferredAmount || 0),
    ),
  )
}

export function shipmentStatusLabel(status: string): string {
  return ({
    draft: '草稿',
    confirmed: '已确认',
    shipped: '已出运',
    completed: '已完成',
    cancelled: '已取消',
  } as Record<string, string>)[status] || status
}

export function financeStatusLabel(status: string): string {
  return ({
    not_generated: '未生成',
    pending: '待付款',
    partial: '部分付款',
    done: '已付款',
    synced: '已同步',
    error: '同步异常',
  } as Record<string, string>)[status] || status
}
