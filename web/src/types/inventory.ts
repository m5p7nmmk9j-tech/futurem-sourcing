export interface Warehouse {
  id: number
  code: string
  name: string
  address?: string | null
  contactName?: string | null
  contactPhone?: string | null
  status: string
}

export interface WarehouseLocation {
  id: number
  warehouseId: number
  code: string
  name: string
  zone?: string | null
  aisle?: string | null
  rack?: string | null
  bin?: string | null
  status: string
}

export interface InventoryLot {
  id: number
  lotNo: string
  customerId: number
  orderProductId: number
  purchaseOrderId: number
  summaryOrderId?: number | null
  receivingOrderId: number
  supplierId: number
  warehouseId: number
  warehouseLocationId?: number | null
  status: string
  onHandQuantity: number
  lockedQuantity: number
  availableQuantity: number
  onHandCartons: number
  lockedCartons: number
  availableCartons: number
  cartonQty: number
  cartonCbm: number
  cartonGwKg: number
  cartonNwKg: number
}

export interface InventoryTransaction {
  id: number
  inventoryLotId: number
  transactionType: string
  sourceType: string
  sourceId?: number | null
  reason: string
  quantityDelta: number
  cartonsDelta: number
  quantityBalance: number
  cartonsBalance: number
  lockedQuantityBalance: number
  lockedCartonsBalance: number
  createdAt: string
}
