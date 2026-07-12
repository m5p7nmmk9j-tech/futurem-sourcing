<template>
  <div class="page">
    <div class="page-header">
      <div>
        <div class="page-title">财务调整单</div>
        <div class="page-subtitle">已确认或已收付款业务不直接覆盖，通过审批后的调整单保留完整审计轨迹。</div>
      </div>
      <el-button type="primary" @click="openCreate">新增调整单</el-button>
    </div>

    <div class="card">
      <div class="toolbar">
        <el-select v-model="filters.status" clearable placeholder="状态" style="width: 150px">
          <el-option label="草稿" value="draft" />
          <el-option label="已审批" value="approved" />
          <el-option label="已应用" value="applied" />
          <el-option label="已取消" value="cancelled" />
        </el-select>
        <el-select v-model="filters.adjustmentType" clearable placeholder="调整类型" style="width: 220px">
          <el-option v-for="item in adjustmentTypes" :key="item.value" :label="item.label" :value="item.value" />
        </el-select>
        <el-input-number v-model="filters.financeRecordId" :min="1" placeholder="财务单 ID" style="width: 150px" />
        <el-button type="primary" @click="load">查询</el-button>
        <el-button @click="resetFilters">重置</el-button>
      </div>

      <el-table :data="rows" border stripe>
        <el-table-column prop="adjustment.id" label="ID" width="80" />
        <el-table-column label="财务单" width="150">
          <template #default="scope">{{ scope.row.financeRecord?.no || `#${scope.row.adjustment.financeRecordId}` }}</template>
        </el-table-column>
        <el-table-column label="类型" min-width="180">
          <template #default="scope">{{ typeLabel(scope.row.adjustment.adjustmentType) }}</template>
        </el-table-column>
        <el-table-column label="原金额" width="120" align="right">
          <template #default="scope">{{ formatRmb(scope.row.adjustment.originalAmount) }}</template>
        </el-table-column>
        <el-table-column label="调整金额" width="120" align="right">
          <template #default="scope"><span :class="Number(scope.row.adjustment.amount) < 0 ? 'negative' : 'positive'">{{ formatSigned(scope.row.adjustment.amount) }}</span></template>
        </el-table-column>
        <el-table-column label="调整后" width="120" align="right">
          <template #default="scope">{{ formatRmb(scope.row.adjustment.resultAmount) }}</template>
        </el-table-column>
        <el-table-column prop="adjustment.reason" label="原因" min-width="220" />
        <el-table-column label="状态" width="100">
          <template #default="scope"><el-tag :type="statusType(scope.row.adjustment.status)">{{ statusLabel(scope.row.adjustment.status) }}</el-tag></template>
        </el-table-column>
        <el-table-column label="时间" width="170">
          <template #default="scope">{{ scope.row.adjustment.createdAt }}</template>
        </el-table-column>
        <el-table-column label="操作" width="220" fixed="right">
          <template #default="scope">
            <el-button v-if="scope.row.adjustment.status === 'draft'" size="small" type="success" @click="approve(scope.row.adjustment)">审批</el-button>
            <el-button v-if="scope.row.adjustment.status === 'approved'" size="small" type="primary" @click="applyAdjustment(scope.row.adjustment)">应用</el-button>
            <el-button v-if="['draft','approved'].includes(scope.row.adjustment.status)" size="small" type="danger" @click="cancelAdjustment(scope.row.adjustment)">取消</el-button>
          </template>
        </el-table-column>
      </el-table>
    </div>

    <el-dialog v-model="createVisible" title="新增财务调整单" width="660px">
      <el-alert title="负数表示冲减，正数表示补充。调整应用后不能直接删除。" type="warning" show-icon style="margin-bottom: 16px" />
      <el-form label-width="120px">
        <el-form-item label="财务单 ID"><el-input-number v-model="form.financeRecordId" :min="1" style="width: 100%" /></el-form-item>
        <el-form-item label="调整类型"><el-select v-model="form.adjustmentType" style="width: 100%"><el-option v-for="item in adjustmentTypes" :key="item.value" :label="item.label" :value="item.value" /></el-select></el-form-item>
        <el-form-item label="调整金额"><el-input-number v-model="form.amount" :precision="2" style="width: 100%" /></el-form-item>
        <el-form-item label="来源类型"><el-input v-model="form.sourceType" placeholder="例如 QC_ORDER / SHIPMENT / MANUAL" /></el-form-item>
        <el-form-item label="来源 ID"><el-input-number v-model="form.sourceId" :min="1" style="width: 100%" /></el-form-item>
        <el-form-item label="调整原因"><el-input v-model="form.reason" type="textarea" :rows="4" /></el-form-item>
      </el-form>
      <template #footer><el-button @click="createVisible = false">取消</el-button><el-button type="primary" @click="create">保存草稿</el-button></template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { http } from '../api/http'
import { formatRmb } from '../utils/rmb'

const rows = ref<any[]>([])
const createVisible = ref(false)
const filters = reactive<any>({ status: '', adjustmentType: '', financeRecordId: null })
const form = reactive<any>({ financeRecordId: null, adjustmentType: 'supplier_refund_or_credit', amount: 0, reason: '', sourceType: 'MANUAL', sourceId: null })
const adjustmentTypes = [
  { value: 'supplier_refund_or_credit', label: '供应商退款 / 应付冲减' },
  { value: 'supplemental_payable', label: '补充应付' },
  { value: 'customer_receivable_adjustment', label: '客户应收调整' },
  { value: 'logistics_cost_adjustment', label: '物流成本调整' },
]

async function load() {
  const params: Record<string, any> = {}
  Object.entries(filters).forEach(([key, value]) => { if (value !== '' && value !== null && value !== undefined) params[key] = value })
  rows.value = (await http.get('/financial-adjustments', { params })).data || []
}
function resetFilters() { Object.assign(filters, { status: '', adjustmentType: '', financeRecordId: null }); load() }
function openCreate() { Object.assign(form, { financeRecordId: null, adjustmentType: 'supplier_refund_or_credit', amount: 0, reason: '', sourceType: 'MANUAL', sourceId: null }); createVisible.value = true }
async function create() {
  if (!form.financeRecordId) return ElMessage.warning('请输入财务单 ID')
  if (!form.amount) return ElMessage.warning('调整金额不能为0')
  if (!form.reason.trim()) return ElMessage.warning('请填写调整原因')
  await http.post('/financial-adjustments', form)
  ElMessage.success('调整单草稿已创建')
  createVisible.value = false
  await load()
}
async function approve(row: any) { await ElMessageBox.confirm('确认审批该调整单？', '审批'); await http.post(`/financial-adjustments/${row.id}/approve`); ElMessage.success('已审批'); await load() }
async function applyAdjustment(row: any) { await ElMessageBox.confirm('应用后将写入财务明细且不能直接删除，是否继续？', '应用调整'); await http.post(`/financial-adjustments/${row.id}/apply`); ElMessage.success('调整已应用'); await load() }
async function cancelAdjustment(row: any) { const result = await ElMessageBox.prompt('请输入取消原因', '取消调整单', { inputValidator: value => Boolean(value?.trim()) || '请输入取消原因' }); await http.post(`/financial-adjustments/${row.id}/cancel`, { reason: result.value }); ElMessage.success('已取消'); await load() }
function typeLabel(value: string) { return adjustmentTypes.find(item => item.value === value)?.label || value }
function statusLabel(value: string) { return ({ draft: '草稿', approved: '已审批', applied: '已应用', cancelled: '已取消' } as Record<string,string>)[value] || value }
function statusType(value: string) { return value === 'applied' ? 'success' : value === 'approved' ? 'warning' : value === 'cancelled' ? 'info' : '' }
function formatSigned(value: unknown) { const n = Number(value || 0); return `${n >= 0 ? '+' : '-'}${formatRmb(Math.abs(n))}` }

onMounted(load)
</script>

<style scoped>
.page-subtitle { margin-top: 4px; color: #64748b; font-size: 13px; }
.toolbar { display: flex; gap: 10px; margin-bottom: 14px; flex-wrap: wrap; }
.negative { color: #dc2626; font-weight: 700; }
.positive { color: #059669; font-weight: 700; }
</style>
