import assert from 'node:assert/strict'
import { expenseOutstanding, normalizeExpenseName, round2, shipmentStatusLabel } from '../src/utils/shipmentFinance.ts'

assert.equal(round2(1.005), 1.01)
assert.equal(normalizeExpenseName('  ocean   freight  '), 'OCEAN FREIGHT')
assert.equal(expenseOutstanding({ amount: 1000, paidAmount: 400, prepaymentAppliedAmount: 300, overpaymentTransferredAmount: 0 }), 300)
assert.equal(expenseOutstanding({ amount: 700, paidAmount: 800, prepaymentAppliedAmount: 0, overpaymentTransferredAmount: 100 }), 0)
assert.equal(shipmentStatusLabel('confirmed'), '已确认')

console.log('shipment finance calculations passed')
