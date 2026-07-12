<template>
  <div class="reservation-status">
    <el-alert
      v-if="confirmed"
      type="success"
      show-icon
      :closable="false"
      title="实际装柜已经确认"
      description="库存已按实际装柜数量扣减，商品货款应收和对应出运单草稿已经生成。"
    />
    <el-alert
      v-else-if="state === 'active'"
      type="success"
      show-icon
      :closable="false"
      :title="`库存已锁定，剩余 ${countdown}`"
      description="普通保存装柜草稿不会延长锁定时间。"
    />
    <el-alert
      v-else-if="state === 'expired' || status === 'lock_expired'"
      type="warning"
      show-icon
      :closable="false"
      title="库存锁定已过期"
      description="草稿仍然保留，但库存已经释放。请重新选择库存并重新锁定。"
    />
    <el-alert
      v-else
      type="info"
      show-icon
      :closable="false"
      title="库存尚未锁定"
      description="商品加入装柜草稿并锁定后，锁定有效期固定为72小时。"
    />

    <div class="status-actions">
      <el-button v-if="!confirmed && state === 'active'" type="warning" @click="$emit('release')">释放库存锁定</el-button>
      <el-button v-if="!confirmed && (state === 'expired' || status === 'lock_expired')" type="primary" @click="$emit('relock')">重新锁定库存</el-button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { formatReservationCountdown, reservationState } from '../utils/containerReservation'

const props = defineProps<{
  status?: string | null
  expiresAt?: string | null
}>()
defineEmits<{ release: []; relock: [] }>()
const now = ref(new Date())
let timer: ReturnType<typeof setInterval> | null = null
const confirmed = computed(() => ['confirmed', 'shipment_created', 'completed'].includes(props.status || ''))
const state = computed(() => confirmed.value ? 'unlocked' : reservationState(props.expiresAt, now.value))
const countdown = computed(() => formatReservationCountdown(props.expiresAt, now.value))

onMounted(() => { timer = setInterval(() => { now.value = new Date() }, 60_000) })
onBeforeUnmount(() => { if (timer) clearInterval(timer) })
</script>

<style scoped>
.reservation-status { margin: 14px 0; }
.status-actions { margin-top: 10px; display: flex; justify-content: flex-end; }
</style>
