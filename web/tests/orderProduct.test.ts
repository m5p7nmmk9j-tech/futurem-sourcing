import assert from 'node:assert/strict'
import {
  customerOrderStatusLabel,
  isCustomerOrderEditable,
  validateOrderProductDraft,
} from '../src/utils/orderProduct.ts'

const valid = {
  supplierId: 1,
  systemSku: 'SKU-001',
  customerBarcode: 'CUS-001',
  nameCn: '测试商品',
  purchaseUnitPrice: 10,
  salesUnitPrice: 15,
  quantity: 100,
  cartons: 10,
  cartonQty: 10,
  cartonLengthCm: 50,
  cartonWidthCm: 40,
  cartonHeightCm: 30,
}

assert.deepEqual(validateOrderProductDraft(valid), [])
assert.deepEqual(
  validateOrderProductDraft({ ...valid, supplierId: null, customerBarcode: '', purchaseUnitPrice: 0 }),
  ['请选择商品供应商', '客户条码不能为空', '采购单价必须大于 0'],
)
assert.equal(isCustomerOrderEditable('draft'), true)
assert.equal(isCustomerOrderEditable('confirmed'), false)
assert.equal(customerOrderStatusLabel('partially_converted'), '部分已生成采购单')
assert.equal(customerOrderStatusLabel('converted'), '已生成采购单')

console.log('order product validation passed')
