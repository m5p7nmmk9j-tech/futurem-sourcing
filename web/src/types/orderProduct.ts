export type OrderProductImage = {
  id?: number
  orderProductId?: number
  imageUrl: string
  imageType: 'main' | 'detail' | 'package' | 'reference' | string
  sortNo: number
  fileName?: string | null
  contentType?: string | null
}

export type OrderProduct = {
  id: number
  customerId: number
  supplierId: number
  sourceOrderProductId?: number | null
  sourceCustomerOrderId: number
  systemSku: string
  customerItemNo?: string | null
  customerBarcode: string
  supplierItemNo?: string | null
  nameCn: string
  nameEn?: string | null
  nameEs?: string | null
  specification?: string | null
  color?: string | null
  unit: string
  purchaseUnitPrice: number
  salesUnitPrice: number
  cartonQty: number
  cartonLengthCm: number
  cartonWidthCm: number
  cartonHeightCm: number
  cartonCbm: number
  cartonGwKg: number
  cartonNwKg: number
  importerProfileId: number
  importerSnapshotJson: string
  labelTemplateId: number
  labelTemplateSnapshotJson: string
  markTemplateId: number
  markTemplateSnapshotJson: string
  batchCode: string
  status: string
  lockedAt?: string | null
  needsReview?: boolean
  remark?: string | null
}

export type OrderProductLine = {
  id?: number
  quantity: number
  cartons: number
  cartonQty: number
  totalCbm?: number
  totalGwKg?: number
  totalNwKg?: number
}

export type OrderProductDraft = Omit<Partial<OrderProduct>,
  'supplierId' |
  'purchaseUnitPrice' |
  'salesUnitPrice' |
  'cartonQty' |
  'cartonLengthCm' |
  'cartonWidthCm' |
  'cartonHeightCm' |
  'cartonGwKg' |
  'cartonNwKg'> & {
  customerOrderId: number
  supplierId: number | null
  nameCn: string
  purchaseUnitPrice: number
  salesUnitPrice: number
  quantity: number
  cartons: number
  cartonQty: number
  cartonLengthCm: number
  cartonWidthCm: number
  cartonHeightCm: number
  cartonGwKg: number
  cartonNwKg: number
  images: OrderProductImage[]
}

export type CustomerImporterProfile = {
  id: number
  customerId: number
  name: string
  companyName: string
  taxIdOrRfc?: string | null
  address: string
  contactName?: string | null
  phone?: string | null
  email?: string | null
  logoUrl?: string | null
  defaultOriginText: string
  defaultLabelTemplateId?: number | null
  defaultMarkTemplateId?: number | null
  isDefault: boolean
  status: string
  remark?: string | null
}

export type LabelMarkTemplate = {
  id: number
  code: string
  name: string
  documentType: string
  templateType: 'product_label' | 'carton_mark' | string
  customerId?: number | null
  importerProfileId?: number | null
  designerMode: 'fixed' | 'visual' | string
  language: string
  paperSize: string
  paperWidthMm?: number | null
  paperHeightMm?: number | null
  orientation: string
  layoutJson: string
  body: string
  isDefault: boolean
  status: string
  remark?: string | null
}
