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

function num(value: number | null | undefined) {
  return Number(value || 0)
}

function round(value: number, precision = 4) {
  const factor = 10 ** precision
  return Math.round((value + Number.EPSILON) * factor) / factor
}

export function calculateDocumentLine(input: DocumentLineCalcInput): DocumentLineCalcResult {
  const cartonQty = num(input.cartonQty)
  const cartons = num(input.cartons)
  const quantity = num(input.quantity) > 0 ? num(input.quantity) : cartonQty * cartons
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
