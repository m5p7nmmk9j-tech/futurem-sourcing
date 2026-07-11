export type DocumentLineCalcInput = {
  quantity?: number | null
  unitPrice?: number | null
  cartonQty?: number | null
  cartons?: number | null
  cartonLengthCm?: number | null
  cartonWidthCm?: number | null
  cartonHeightCm?: number | null
  cartonGwKg?: number | null
  cartonNwKg?: number | null
}

export type DocumentLineCalcResult = Required<DocumentLineCalcInput> & {
  amount: number
  cartonCbm: number
  totalCbm: number
  totalGwKg: number
  totalNwKg: number
}

export type DocumentLineSummary = {
  quantity: number
  amount: number
  cartons: number
  cbm: number
  gwKg: number
  nwKg: number
}

export type ProductDefaults = {
  nameCn?: string | null
  unit?: string | null
  imageUrl?: string | null
  purchasePrice?: number | null
  cartonQty?: number | null
  cartonLengthCm?: number | null
  cartonWidthCm?: number | null
  cartonHeightCm?: number | null
  cartonGwKg?: number | null
  cartonNwKg?: number | null
}

function num(value: number | null | undefined) {
  return Number(value || 0)
}

function round2(value: number) {
  return Math.round((value + Number.EPSILON) * 100) / 100
}

export function calculateDocumentLine(input: DocumentLineCalcInput): DocumentLineCalcResult {
  const cartonQty = round2(num(input.cartonQty))
  const quantity = round2(num(input.quantity) > 0 ? num(input.quantity) : cartonQty * num(input.cartons))
  const cartons = round2(num(input.cartons) > 0 ? num(input.cartons) : (cartonQty > 0 && quantity > 0 ? Math.ceil(quantity / cartonQty) : 0))
  const unitPrice = round2(num(input.unitPrice))
  const cartonLengthCm = round2(num(input.cartonLengthCm))
  const cartonWidthCm = round2(num(input.cartonWidthCm))
  const cartonHeightCm = round2(num(input.cartonHeightCm))
  const cartonGwKg = round2(num(input.cartonGwKg))
  const cartonNwKg = round2(num(input.cartonNwKg))
  const cartonCbm = round2(cartonLengthCm * cartonWidthCm * cartonHeightCm / 1000000)

  return {
    quantity,
    unitPrice,
    cartonQty,
    cartons,
    cartonLengthCm,
    cartonWidthCm,
    cartonHeightCm,
    cartonGwKg,
    cartonNwKg,
    amount: round2(quantity * unitPrice),
    cartonCbm,
    totalCbm: round2(cartonCbm * cartons),
    totalGwKg: round2(cartonGwKg * cartons),
    totalNwKg: round2(cartonNwKg * cartons),
  }
}

export function calculateDocumentLineSummary(lines: DocumentLineCalcResult[]): DocumentLineSummary {
  return lines.reduce<DocumentLineSummary>((total, line) => ({
    quantity: round2(total.quantity + num(line.quantity)),
    amount: round2(total.amount + num(line.amount)),
    cartons: round2(total.cartons + num(line.cartons)),
    cbm: round2(total.cbm + num(line.totalCbm)),
    gwKg: round2(total.gwKg + num(line.totalGwKg)),
    nwKg: round2(total.nwKg + num(line.totalNwKg)),
  }), { quantity: 0, amount: 0, cartons: 0, cbm: 0, gwKg: 0, nwKg: 0 })
}

export function applyProductDefaultsToLine<T extends Record<string, any>>(line: T, product: ProductDefaults): T {
  return {
    ...line,
    productName: product.nameCn || line.productName || '',
    unit: product.unit || line.unit || 'PCS',
    imageUrl: product.imageUrl || line.imageUrl || '',
    unitPrice: round2(num(product.purchasePrice) || num(line.unitPrice)),
    cartonQty: round2(num(product.cartonQty) || num(line.cartonQty)),
    cartonLengthCm: round2(num(product.cartonLengthCm) || num(line.cartonLengthCm)),
    cartonWidthCm: round2(num(product.cartonWidthCm) || num(line.cartonWidthCm)),
    cartonHeightCm: round2(num(product.cartonHeightCm) || num(line.cartonHeightCm)),
    cartonGwKg: round2(num(product.cartonGwKg) || num(line.cartonGwKg)),
    cartonNwKg: round2(num(product.cartonNwKg) || num(line.cartonNwKg)),
  }
}
