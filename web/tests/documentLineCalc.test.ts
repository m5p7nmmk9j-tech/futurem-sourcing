import assert from 'node:assert/strict'
import {
  calculateDocumentLine,
  calculateDocumentLineSummary,
} from '../src/utils/documentLineCalc.ts'

const line = calculateDocumentLine({
  quantity: 0,
  unitPrice: 10,
  cartonQty: 100,
  cartons: 10,
  cartonLengthCm: 50,
  cartonWidthCm: 40,
  cartonHeightCm: 5,
  cartonGwKg: 10,
  cartonNwKg: 8,
})

assert.equal(line.quantity, 1000)
assert.equal(line.amount, 10000)
assert.equal(line.cartonCbm, 0.01)
assert.equal(line.totalCbm, 0.1)
assert.equal(line.totalGwKg, 100)
assert.equal(line.totalNwKg, 80)

const manualQuantity = calculateDocumentLine({
  quantity: 1200,
  unitPrice: 10,
  cartonQty: 100,
  cartons: 10,
  cartonLengthCm: 50,
  cartonWidthCm: 40,
  cartonHeightCm: 5,
})

assert.equal(manualQuantity.quantity, 1200)
assert.equal(manualQuantity.totalCbm, 0.1)

const summary = calculateDocumentLineSummary([
  line,
  calculateDocumentLine({
    quantity: 0,
    unitPrice: 5,
    cartonQty: 50,
    cartons: 4,
    cartonLengthCm: 40,
    cartonWidthCm: 30,
    cartonHeightCm: 20,
    cartonGwKg: 6,
    cartonNwKg: 5,
  }),
])

assert.equal(summary.quantity, 1200)
assert.equal(summary.cartons, 14)
assert.equal(summary.cbm, 0.196)
assert.equal(summary.gwKg, 124)
assert.equal(summary.nwKg, 100)

console.log('document line calculations passed')
