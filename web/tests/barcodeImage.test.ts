import assert from 'node:assert/strict'
import { barcodeImageOptions, normalizeBarcodeValue } from '../src/utils/barcodeImage.ts'

assert.equal(normalizeBarcodeValue('  1234567890123  '), '1234567890123')
assert.equal(normalizeBarcodeValue(''), '')
assert.deepEqual(barcodeImageOptions('1234567890123'), {
  format: 'CODE128',
  width: 1.4,
  height: 34,
  margin: 2,
  displayValue: true,
  fontSize: 11,
  textMargin: 1,
})

console.log('barcode image behavior passed')
