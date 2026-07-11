<template>
  <div class="qc-editor">
    <div class="header-row">
      <div>
        <strong>验货结果</strong>
        <span class="hint">到货 = 合格 + 不合格 + 退回 + 待处理；供应商应付只按最终接受数量计算。</span>
      </div>
      <el-button size="small" @click="load">刷新</el-button>
    </div>

    <el-table :data="rows" border stripe size="small">
      <el-table-column prop="product.customerItemNo" label="客户货号" width="130" />
      <el-table-column prop="product.customerBarcode" label="客户条码" width="145" />
      <el-table-column prop="product.nameCn" label="商品名称" min-width="170" />
      <el-table-column label="到货数量" width="125">
        <template #default="scope">
          <el-input-number v-model="scope.row.arrivedQuantity" :min="0" :precision="2" disabled style="width: 105px" />
        </template>
      </el-table-column>
      <el-table-column label="合格" width="125">
        <template #default="scope">
          <el-input-number v-model="scope.row.qualifiedQuantity" :min="0" :precision="2" :disabled="!editable" style="width: 105px" />
        </template>
      </el-table-column>
      <el-table-column label="不合格" width="125">
        <template #default="scope">
          <el-input-number v-model="scope.row.unqualifiedQuantity" :min="0" :precision="2" :disabled="!editable" style="width: 105px" />
        </template>
      </el-table-column>
      <el-table-column label="退回" width="125">
        <template #default="scope">
          <el-input-number v-model="scope.row.returnedQuantity" :min="0" :precision="2" :disabled="!editable" style="width: 105px" />
        </template>
      </el-table-column>
      <el-table-column label="待处理" width="125">
        <template #default="scope">
          <el-input-number v-model="scope.row.pendingQuantity" :min="0" :precision="2" :disabled="!editable" style="width: 105px" />
        </template>
      </el-table-column>
      <el-table-column label="最终接受" width="135">
        <template #default="scope">
          <el-input-number
            v-model="scope.row.acceptedQuantity"
            :min="0"
            :max="Number(scope.row.arrivedQuantity || 0)"
            :precision="2"
            :disabled="!editable"
            style="width: 115px"
          />
        </template>
      </el-table-column>
      <el-table-column label="数量校验" width="115">
        <template #default="scope">
          <el-tag :type="equationValid(scope.row) ? 'success' : 'danger'">
            {{ equationValid(scope.row) ? '正确' : `相差 ${difference(scope.row)}` }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column label="采购单价" width="105" align="right">
        <template #default="scope">{{ formatRmb(scope.row.receivingLine.purchaseUnitPriceSnapshot) }}</template>
      </el-table-column>
      <el-table-column label="预计应付" width="115" align="right">
        <template #default="scope">{{ formatRmb(payable(scope.row)) }}</template>
      </el-table-column>
    </el-table>

    <div class="footer-row">
      <div>
        最终接受 {{ numberText(acceptedTotal) }} 件，预计供应商应付 {{ formatRmb(payableTotal) }}
      </div>
      <div class="actions">
        <el-button v-if="status === 'confirmed'" type="warning" @click="unlock">解锁修改</el-button>
        <el-button v-if="editable" type="primary" :disabled="!allValid" @click="confirm">确认验货并生成应付</el-button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { http } from '../api/http'
import { formatRmb } from '../utils/rmb'

const props = defineProps<{ qcId: number; status: string }>()
const emit = defineEmits<{ (event: 'changed'): void }>()
const rows = ref<any[]>([])
const editable = computed(() => ['draft', 'unlocked'].includes(props.status))
const acceptedTotal = computed(() => rows.value.reduce((sum, row) => sum + Number(row.acceptedQuantity || 0), 0))
const payableTotal = computed(() => rows.value.reduce((sum, row) => sum + payable(row), 0))
const allValid = computed(() => rows.value.length > 0 && rows.value.every(equationValid))

async function load() {
  if (!props.qcId) return
  const response = await http.get(`/qc-orders/${props.qcId}`)
  rows.value = (response.data.lines || []).map((row: any) => {
    const arrived = Number(row.receivingLine.quantity || 0)
    return {
      ...row,
      arrivedQuantity: Number(row.qcLine?.arrivedQuantity ?? arrived),
      qualifiedQuantity: Number(row.qcLine?.qualifiedQuantity ?? arrived),
      unqualifiedQuantity: Number(row.qcLine?.unqualifiedQuantity ?? 0),
      returnedQuantity: Number(row.qcLine?.returnedQuantity ?? 0),
      pendingQuantity: Number(row.qcLine?.pendingQuantity ?? 0),
      acceptedQuantity: Number(row.qcLine?.acceptedQuantity ?? arrived),
    }
  })
}

function classified(row: any) {
  return Number(row.qualifiedQuantity || 0) +
    Number(row.unqualifiedQuantity || 0) +
    Number(row.returnedQuantity || 0) +
    Number(row.pendingQuantity || 0)
}

function difference(row: any) {
  return numberText(Number(row.arrivedQuantity || 0) - classified(row))
}

function equationValid(row: any) {
  const equation = Math.abs(Number(row.arrivedQuantity || 0) - classified(row)) < 0.005
  const accepted = Number(row.acceptedQuantity || 0)
  return equation && accepted >= 0 && accepted <= Number(row.arrivedQuantity || 0)
}

function payable(row: any) {
  return Number(row.acceptedQuantity || 0) * Number(row.receivingLine.purchaseUnitPriceSnapshot || 0)
}

async function confirm() {
  if (!allValid.value) return ElMessage.warning('请先修正数量恒等式和最终接受数量')
  await ElMessageBox.confirm(
    `确认最终接受 ${numberText(acceptedTotal.value)} 件，并生成应付 ${formatRmb(payableTotal.value)}？`,
    '确认验货',
  )
  await http.post(`/qc-orders/${props.qcId}/confirm`, {
    lines: rows.value.map(row => ({
      receivingLineId: row.receivingLine.id,
      arrivedQuantity: Number(row.arrivedQuantity),
      qualifiedQuantity: Number(row.qualifiedQuantity),
      unqualifiedQuantity: Number(row.unqualifiedQuantity),
      returnedQuantity: Number(row.returnedQuantity),
      pendingQuantity: Number(row.pendingQuantity),
      acceptedQuantity: Number(row.acceptedQuantity),
    })),
  })
  ElMessage.success('验货已确认，供应商应付已按最终接受数量生成')
  emit('changed')
}

async function unlock() {
  const result = await ElMessageBox.prompt('填写解锁原因；系统将保留修改前后记录', '解锁验货单', {
    inputValidator: value => !!value?.trim() || '解锁原因不能为空',
  })
  await http.post(`/qc-orders/${props.qcId}/unlock`, { reason: result.value })
  ElMessage.success('验货单已解锁，可重新确认')
  emit('changed')
}

function numberText(value: number | string | null | undefined) {
  return Number(value || 0).toLocaleString('zh-CN', { maximumFractionDigits: 2 })
}

watch(() => props.qcId, load)
watch(() => props.status, load)
onMounted(load)
</script>

<style scoped>
.qc-editor { margin-top: 18px; }
.header-row, .footer-row { display: flex; justify-content: space-between; align-items: center; gap: 12px; }
.header-row { margin-bottom: 12px; }
.footer-row { margin-top: 14px; font-weight: 600; }
.actions { display: flex; gap: 8px; }
.hint { margin-left: 12px; color: #64748b; font-size: 12px; font-weight: 400; }
</style>
