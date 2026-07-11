import assert from 'node:assert/strict'
import {
  deliveryNoticeStatusLabel,
  remainingDeliveryLine,
  validateReceivingDraft,
} from '../src/utils/deliveryNotice.ts'

assert.equal(deliveryNoticeStatusLabel('draft'), '草稿')
assert.equal(deliveryNoticeStatusLabel('supplier_confirmed'), '供应商已确认')
assert.equal(deliveryNoticeStatusLabel('partially_received'), '部分收货')

assert.deepEqual(
  remainingDeliveryLine({
    plannedQuantity: 100,
    plannedCartons: 5,
    receivedQuantity: 40,
    receivedCartons: 2,
  }),
  { quantity: 60, cartons: 3 },
)

assert.deepEqual(
  validateReceivingDraft([
    {
      deliveryNoticeLineId: 1,
      quantity: 60,
      cartons: 3,
      availableQuantity: 60,
      availableCartons: 3,
    },
  ]),
  [],
)

assert.deepEqual(
  validateReceivingDraft([
    {
      deliveryNoticeLineId: 1,
      quantity: 61,
      cartons: 3,
      availableQuantity: 60,
      availableCartons: 3,
    },
  ]),
  ['第 1 行实际到货数量超过剩余计划'],
)

console.log('delivery notice calculations passed')
