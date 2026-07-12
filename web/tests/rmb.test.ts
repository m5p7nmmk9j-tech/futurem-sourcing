import assert from 'node:assert/strict'
import { formatRmb, roundRmb } from '../src/utils/rmb.ts'

assert.equal(formatRmb(1234.5), '¥1,234.50')
assert.equal(formatRmb('0'), '¥0.00')
assert.equal(formatRmb(null), '¥0.00')
assert.equal(roundRmb(12.345), 12.35)
assert.equal(roundRmb(-12.345), -12.35)

console.log('RMB formatting passed')
