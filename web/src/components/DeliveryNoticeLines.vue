<template>
  <div class="notice-lines">
    <div class="header-row">
      <div>
        <strong>计划送货商品</strong>
        <span class="hint">可按实际到货分多次创建收货单，暂不生成供应商应付。</span>
      </div>
      <el-button size="small" @click="load">刷新</el-button>
    </div>

    <el-table :data="rows" border stripe size="small">
      <el-table-column prop="purchaseOrder.no" label="PO 单号" width="145" />
      <el-table-column prop="product.customerItemNo" label="客户货号" width="130" />
      <el-table-column prop="product.customerBarcode" label="客户条码" width="145" />
      <el-table-column prop="product.nameCn" label="商品名称" min-width="170" />
      <el-table-column label="计划箱数" width="90" align="right">
        <template #default="scope">{{ numberText(scope.row.line.plannedCartons) }}</template>
      </el-table-column>
      <el-table-column label="已收箱数" width="90" align="right">
        <template #default="scope">{{ numberText(scope.row.line.receivedCartons) }}</template>
      </el-table-column>
      <el-table-column label="剩余箱数" width="90" align="right">
        <template #default="scope">{{ numberText(remainingCartons(scope.row)) }}</template>
      </el-table-column>
      <el-table-column label="本次箱数" width="145">
        <template #default="scope">
          <el-input-number
            v-model="scope.row.receiveCartons"
            :min="0"
            :max="remainingCartons(scope.row)"
            :precision="2"
            controls-position="right"
            style="width: 125px"
            @change="syncQuantity(scope.row)"
          />
        </template>
      </el-table-column>
      <el-table-column label="本次数量" width="145">
        <template #default="scope">
          <el-input-number
            v-model="scope.row.receiveQuantity"
            :min="0"
            :max="remainingQuantity(scope.row)"
            :precision="2"
            controls-position="right"
            style="width: 125px"
          />
        </template>
      </el-table-column>
      <el-table-column label="备注" min-width="150">
        <template #default="scope"><el-input v-model="scope.row.receiveRemark" /></template>
      </el-table-column>
    </el-table>

    <div class="footer-row">
      <span>本次合计：{{ receiveCartonsTotal }} 箱 / {{ receiveQuantityTotal }} 件</span>
      <el-button type="primary" :disabled="!canReceive" @click="createReceiving">创建分批收货单</el-button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { http } from '../api/http'

const props = defineProps<{ noticeId: number; status: string }>()
const emit = defineEmits<{ (event: 'changed'): void }>()
const rows = ref<any[]>([])

const receiveRows = computed(() => rows.value.filter(row => Number(row.receiveQuantity || 0) > 0 && Number(row.receiveCartons || 0) > 0))
const receiveCartonsTotal = computed(() => numberText(receiveRows.value.reduce((sum, row) => sum + Number(row.receiveCartons || 0), 0)))
const receiveQuantityTotal = computed(() => numberText(receiveRows.value.reduce((sum, row) => sum + Number(row.receiveQuantity || 0), 0)))
const canReceive = computed(() => !['cancelled', 'closed', 'received'].includes(props.status) && receiveRows.value.length > 0)

async function load() {
  if (!props.noticeId) return
  const response = await http.get(`/delivery-notices/${props.noticeId}`)
  rows.value = (response.data.lines || []).map((row: any) => ({
    ...row,
    receiveCartons: 0,
    receiveQuantity: 0,
    receiveRemark: '',
  }))
}

function remainingCartons(row: any) {
  return Math.max(0, Number(row.line.plannedCartons || 0) - Number(row.line.receivedCartons || 0))
}

function remainingQuantity(row: any) {
  return Math.max(0, Number(row.line.plannedQuantity || 0) - Number(row.line.receivedQuantity || 0))
}

function syncQuantity(row: any) {
  const plannedCartons = Number(row.line.plannedCartons || 0)
  const plannedQuantity = Number(row.line.plannedQuantity || 0)
  const cartonQty = plannedCartons > 0 ? plannedQuantity / plannedCartons : 0
  row.receiveQuantity = Math.min(remainingQuantity(row), Number(row.receiveCartons || 0) * cartonQty)
}

async function createReceiving() {
  await ElMessageBox.confirm(
    `确认按本次 ${receiveCartonsTotal.value} 箱、${receiveQuantityTotal.value} 件创建收货单？`,
    '创建分批收货单',
  )
  await http.post(`/delivery-notices/${props.noticeId}/receivings`, {
    lines: receiveRows.value.map(row => ({
      deliveryNoticeLineId: row.line.id,
      quantity: Number(row.receiveQuantity),
      cartons: Number(row.receiveCartons),
      remark: row.receiveRemark || null,
    })),
  })
  ElMessage.success('分批收货单已创建，等待验货确认最终接受数量')
  await load()
  emit('changed')
}

function numberText(value: number | string | null | undefined) {
  return Number(value || 0).toLocaleString('zh-CN', { maximumFractionDigits: 2 })
}

watch(() => props.noticeId, load)
onMounted(load)
</script>

<style scoped>
.notice-lines { margin-top: 16px; }
.header-row, .footer-row { display: flex; align-items: center; justify-content: space-between; gap: 12px; }
.header-row { margin-bottom: 12px; }
.footer-row { margin-top: 14px; font-weight: 600; }
.hint { margin-left: 12px; color: #64748b; font-size: 12px; font-weight: 400; }
</style>
