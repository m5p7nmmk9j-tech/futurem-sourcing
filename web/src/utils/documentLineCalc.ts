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

function round(value: number, precision = 4) {
  const factor = 10 ** precision
  return Math.round((value + Number.EPSILON) * factor) / factor
}

export function calculateDocumentLine(input: DocumentLineCalcInput): DocumentLineCalcResult {
  const cartonQty = num(input.cartonQty)
  const quantity = num(input.quantity) > 0 ? num(input.quantity) : cartonQty * num(input.cartons)
  const cartons = num(input.cartons) > 0 ? num(input.cartons) : (cartonQty > 0 && quantity > 0 ? Math.ceil(quantity / cartonQty) : 0)
  const unitPrice = num(input.unitPrice)
  const cartonLengthCm = num(input.cartonLengthCm)
  const cartonWidthCm = num(input.cartonWidthCm)
  const cartonHeightCm = num(input.cartonHeightCm)
  const cartonGwKg = num(input.cartonGwKg)
  const cartonNwKg = num(input.cartonNwKg)
  const cartonCbm = cartonLengthCm * cartonWidthCm * cartonHeightCm / 1000000

  return {
    quantity: round(quantity),
    unitPrice,
    cartonQty,
    cartons,
    cartonLengthCm,
    cartonWidthCm,
    cartonHeightCm,
    cartonGwKg,
    cartonNwKg,
    amount: round(quantity * unitPrice),
    cartonCbm: round(cartonCbm, 6),
    totalCbm: round(cartonCbm * cartons, 6),
    totalGwKg: round(cartonGwKg * cartons),
    totalNwKg: round(cartonNwKg * cartons),
  }
}

export function calculateDocumentLineSummary(lines: DocumentLineCalcResult[]): DocumentLineSummary {
  return lines.reduce<DocumentLineSummary>((total, line) => ({
    quantity: round(total.quantity + num(line.quantity)),
    amount: round(total.amount + num(line.amount)),
    cartons: round(total.cartons + num(line.cartons)),
    cbm: round(total.cbm + num(line.totalCbm), 6),
    gwKg: round(total.gwKg + num(line.totalGwKg)),
    nwKg: round(total.nwKg + num(line.totalNwKg)),
  }), { quantity: 0, amount: 0, cartons: 0, cbm: 0, gwKg: 0, nwKg: 0 })
}

export function applyProductDefaultsToLine<T extends Record<string, any>>(line: T, product: ProductDefaults): T {
  return {
    ...line,
    productName: product.nameCn || line.productName || '',
    unit: product.unit || line.unit || 'PCS',
    imageUrl: product.imageUrl || line.imageUrl || '',
    unitPrice: num(product.purchasePrice) || num(line.unitPrice),
    cartonQty: num(product.cartonQty) || num(line.cartonQty),
    cartonLengthCm: num(product.cartonLengthCm) || num(line.cartonLengthCm),
    cartonWidthCm: num(product.cartonWidthCm) || num(line.cartonWidthCm),
    cartonHeightCm: num(product.cartonHeightCm) || num(line.cartonHeightCm),
    cartonGwKg: num(product.cartonGwKg) || num(line.cartonGwKg),
    cartonNwKg: num(product.cartonNwKg) || num(line.cartonNwKg),
  }
}
