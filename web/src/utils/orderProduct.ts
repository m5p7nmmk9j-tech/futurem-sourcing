export type OrderProductDraftLike = {
  supplierId?: number | null
  systemSku?: string | null
  customerBarcode?: string | null
  nameCn?: string | null
  purchaseUnitPrice?: number | null
  salesUnitPrice?: number | null
  quantity?: number | null
  cartons?: number | null
  cartonQty?: number | null
  cartonLengthCm?: number | null
  cartonWidthCm?: number | null
  cartonHeightCm?: number | null
}

export function validateOrderProductDraft(product: OrderProductDraftLike): string[] {
  const errors: string[] = []
  if (!Number(product.supplierId || 0)) errors.push('请选择商品供应商')
  if (!String(product.customerBarcode || '').trim()) errors.push('客户条码不能为空')
  if (!String(product.nameCn || '').trim()) errors.push('商品名称不能为空')
  if (Number(product.purchaseUnitPrice || 0) <= 0) errors.push('采购单价必须大于 0')
  if (Number(product.salesUnitPrice || 0) <= 0) errors.push('客户销售单价必须大于 0')
  if (Number(product.quantity || 0) <= 0) errors.push('数量必须大于 0')
  if (Number(product.cartons || 0) <= 0) errors.push('箱数必须大于 0')
  if (Number(product.cartonQty || 0) <= 0) errors.push('单箱数量必须大于 0')
  if (
    Number(product.cartonLengthCm || 0) <= 0 ||
    Number(product.cartonWidthCm || 0) <= 0 ||
    Number(product.cartonHeightCm || 0) <= 0
  ) {
    errors.push('外箱尺寸必须完整')
  }
  return errors
}

export function isCustomerOrderEditable(status?: string | null): boolean {
  return String(status || '').toLowerCase() === 'draft'
}

export function customerOrderStatusLabel(status?: string | null): string {
  const labels: Record<string, string> = {
    draft: '草稿',
    confirmed: '已确认',
    partially_converted: '部分已生成采购单',
    converted: '已生成采购单',
    cancelled: '已取消',
  }
  return labels[String(status || '').toLowerCase()] || String(status || '-')
}
