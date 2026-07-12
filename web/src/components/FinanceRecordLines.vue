<template>
  <div>
    <el-row :gutter="12" class="totals">
      <el-col :span="6"><el-statistic title="应收/应付总额" :value="Number(record.amount || 0)" prefix="¥" /></el-col>
      <el-col :span="6"><el-statistic title="已收/已付" :value="Number(record.paidAmount || 0)" prefix="¥" /></el-col>
      <el-col :span="6"><el-statistic title="预收/预付抵扣" :value="Number(record.prepaymentAppliedAmount || 0)" prefix="¥" /></el-col>
      <el-col :span="6"><el-statistic title="未收/未付" :value="Number(outstandingAmount || 0)" prefix="¥" /></el-col>
    </el-row>

    <el-table :data="lines" border stripe size="small">
      <el-table-column label="明细分类" width="160">
        <template #default="scope">{{ lineTypeLabel(scope.row.lineType) }}</template>
      </el-table-column>
      <el-table-column prop="description" label="说明" min-width="230" />
      <el-table-column prop="sourceType" label="来源类型" width="150" />
      <el-table-column prop="sourceId" label="来源 ID" width="100" />
      <el-table-column label="数量" width="100" align="right"><template #default="scope">{{ numberText(scope.row.quantity) }}</template></el-table-column>
      <el-table-column label="单价" width="110" align="right"><template #default="scope">¥{{ money(scope.row.unitPrice) }}</template></el-table-column>
      <el-table-column label="金额" width="120" align="right"><template #default="scope">¥{{ money(scope.row.amount) }}</template></el-table-column>
      <el-table-column label="已结算" width="110" align="right"><template #default="scope">¥{{ money(scope.row.paidAmount) }}</template></el-table-column>
      <el-table-column label="状态" width="100"><template #default="scope">{{ statusLabel(scope.row.status) }}</template></el-table-column>
    </el-table>
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import { http } from '../api/http'

const props = defineProps<{ financeRecordId: number }>()
const record = ref<any>({})
const lines = ref<any[]>([])
const outstandingAmount = ref(0)

async function load() {
  if (!props.financeRecordId) return
  const response = await http.get(`/finance-records/${props.financeRecordId}/lines`)
  record.value = response.data.record || {}
  lines.value = response.data.lines || []
  outstandingAmount.value = Number(response.data.outstandingAmount || 0)
}
function lineTypeLabel(value: string) {
  return ({
    goods: '商品货款',
    logistics_customer_charge: '客户物流费用',
    logistics_provider_cost: '物流服务商成本',
    supplier_goods: '商品供应商货款',
    ocean_freight: '海运费',
    customs: '报关费',
    trucking: '拖车费',
    warehouse: '仓储费',
    courier: '快递费',
    adjustment: '调整'
  } as Record<string, string>)[value] || value
}
function statusLabel(value: string) { return ({ pending: '未结清', partial: '部分结清', done: '已结清' } as Record<string, string>)[value] || value }
function money(value: unknown) { return Number(value || 0).toFixed(2) }
function numberText(value: unknown) { return Number(value || 0).toLocaleString('zh-CN', { maximumFractionDigits: 2 }) }
watch(() => props.financeRecordId, load)
onMounted(load)
defineExpose({ load })
</script>

<style scoped>
.totals { margin-bottom: 16px; padding: 12px; border: 1px solid #e5e7eb; border-radius: 10px; background: #f8fafc; }
</style>
