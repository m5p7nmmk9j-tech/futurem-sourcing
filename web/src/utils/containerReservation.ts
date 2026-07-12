export type ContainerReservationState = 'unlocked' | 'active' | 'expired'

export function reservationState(expiresAt?: string | null, now = new Date()): ContainerReservationState {
  if (!expiresAt) return 'unlocked'
  return new Date(expiresAt).getTime() <= now.getTime() ? 'expired' : 'active'
}

export function remainingMilliseconds(expiresAt?: string | null, now = new Date()): number {
  if (!expiresAt) return 0
  return Math.max(0, new Date(expiresAt).getTime() - now.getTime())
}

export function formatReservationCountdown(expiresAt?: string | null, now = new Date()): string {
  const remaining = remainingMilliseconds(expiresAt, now)
  if (remaining <= 0) return expiresAt ? '库存锁定已过期' : '库存未锁定'
  const totalMinutes = Math.floor(remaining / 60000)
  const days = Math.floor(totalMinutes / 1440)
  const hours = Math.floor((totalMinutes % 1440) / 60)
  const minutes = totalMinutes % 60
  return `${days}天 ${hours}小时 ${minutes}分钟`
}
