import assert from 'node:assert/strict'
import { formatReservationCountdown, remainingMilliseconds, reservationState } from '../src/utils/containerReservation'

const now = new Date('2026-07-11T09:00:00.000Z')
const expires = '2026-07-14T09:00:00.000Z'

assert.equal(reservationState(expires, new Date('2026-07-14T08:59:00.000Z')), 'active')
assert.equal(reservationState(expires, new Date('2026-07-14T09:00:00.000Z')), 'expired')
assert.equal(remainingMilliseconds(expires, now), 72 * 60 * 60 * 1000)
assert.equal(formatReservationCountdown(expires, now), '3天 0小时 0分钟')
assert.equal(formatReservationCountdown(null, now), '库存未锁定')
assert.equal(formatReservationCountdown(expires, new Date('2026-07-14T09:00:00.000Z')), '库存锁定已过期')
