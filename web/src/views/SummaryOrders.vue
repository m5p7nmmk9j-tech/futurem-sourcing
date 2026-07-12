<template>
  <div class="page">
    <div class="page-header">
      <div>
        <div class="page-title">客户汇总单</div>
        <div class="page-subtitle">按同一客户汇总不同 PO 和供应商的整箱商品，核对箱数、体积、重量与预计利润。</div>
      </div>
      <el-button type="primary" @click="openCreate">新增汇总单</el-button>
    </div>

    <div class="card">
      <div class="toolbar">
        <el-select
          v-model="customerId"
          placeholder="按客户筛选"
          clearable
          filterable
          style="width: 280px"
          @change="load"
        >
          <el-option v-for="customer in customers" :key="customer.id" :label="customer.name" :value="customer.id" />
        </el-select>
        <el-button @click="load">刷新</el-button>
      </div>

      <el-table :data="rows" border stripe>
        <el-table-column label="汇总单号" width="170">
          <template #default="scope">
            <el-button link type="primary" class="document-no" @click="openDocument(scope.row)">
              {{ scope.row.summary.no }}
            </el-button>
          </template>
        </el-table-column>
        <el-table-column label="客户" min-width="170">
          <template #default="scope">{{ customerName(scope.row.summary.customerId) }}</template>
        </el-table-column>
        <el-table-column label="汇总日期" width="125">
          <template #default="scope">{{ dateText(scope.row.summary.orderDate) }}</template>
        </el-table-column>
        <el-table-column label="状态" width="105">
          <template #default="scope">
            <el-tag :type="statusTagType(scope.row.summary.status)">
              {{ statusLabel(scope.row.summary.status) }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="itemCount" label="商品数" width="85" />
        <el-table-column prop="supplierCount" label="供应商数" width="95" />
        <el-table-column label="箱数" width="90" align="right">
          <template #default="scope">{{ numberText(scope.row.summary.totalCartons) }}</template>
        </el-table-column>
        <el-table-column label="数量" width="100" align="right">
          <template #default="scope">{{ numberText(scope.row.summary.totalQuantity) }}</template>
        </el-table-column>
        <el-table-column label="CBM" width="95" align="right">
          <template #default="scope">{{ numberText(scope.row.summary.totalCbm) }}</template>
        </el-table-column>
        <el-table-column label="毛重 KG" width="105" align="right">
          <template #default="scope">{{ numberText(scope.row.summary.totalGrossWeightKg) }}</template>
        </el-table-column>
        <el-table-column label="采购金额" width="125" align="right">
          <template #default="scope">{{ formatRmb(scope.row.summary.purchaseAmount) }}</template>
        </el-table-column>
        <el-table-column label="客户金额" width="125" align="right">
          <template #default="scope">{{ formatRmb(scope.row.summary.salesAmount) }}</template>
        </el-table-column>
        <el-table-column label="预计利润" width="125" align="right">
          <template #default="scope">{{ formatRmb(scope.row.summary.expectedProfit) }}</template>
        </el-table-column>
        <el-table-column label="操作" width="220" fixed="right">
          <template #default="scope">
            <el-button size="small" @click="openDocument(scope.row)">查看</el-button>
            <el-button
              v-if="scope.row.summary.status === 'draft'"
              size="small"
              type="success"
              @click="confirmSummary(scope.row.summary)"
            >确认</el-button>
            <el-button
              v-if="scope.row.summary.status === 'draft'"
              size="small"
              type="danger"
              @click="cancelSummary(scope.row.summary)"
            >取消</el-button>
          </template>
        </el-table-column>
      </el-table>
    </div>

    <el-dialog
      v-model="dialogVisible"
      :title="form.id ? `客户汇总单：${form.no || ''}` : '新增客户汇总单'"
      width="94%"
      destroy-on-close
    >
      <el-alert
        title="商品按整箱从已确认采购订单中加入；草稿加入后会立即占用可汇总箱数。"
        type="info"
        show-icon
        style="margin-bottom: 14px"
      />

      <el-form label-width="110px" :disabled="!editable">
        <el-row :gutter="16">
          <el-col :span="8">
            <el-form-item label="客户">
              <el-select v-model="form.customerId" filterable placeholder="选择客户" style="width: 100%">
                <el-option v-for="customer in customers" :key="customer.id" :label="customer.name" :value="customer.id" />
              </el-select>
            </el-form-item>
          </el-col>
          <el-col :span="8">
            <el-form-item label="汇总日期">
              <el-date-picker v-model="form.orderDate" type="date" value-format="YYYY-MM-DD" style="width: 100%" />
            </el-form-item>
          </el-col>
          <el-col :span="8">
            <el-form-item label="计划柜型">
              <el-select v-model="form.containerType" clearable placeholder="暂不确定" style="width: 100%">
                <el-option label="20GP" value="20GP" />
                <el-option label="40GP" value="40GP" />
                <el-option label="40HQ" value="40HQ" />
                <el-option label="45HQ" value="45HQ" />
              </el-select>
            </el-form-item>
          </el-col>
          <el-col :span="8">
            <el-form-item label="计划送货日">
              <el-date-picker v-model="form.plannedDeliveryDate" type="date" value-format="YYYY-MM-DD" style="width: 100%" />
            </el-form-item>
          </el-col>
          <el-col :span="8">
            <el-form-item label="收货仓库 ID">
              <el-input-number v-model="form.warehouseId" :min="1" style="width: 100%" />
            </el-form-item>
          </el-col>
          <el-col :span="8">
            <el-form-item label="状态">
              <el-input :model-value="statusLabel(form.status)" disabled />
            </el-form-item>
          </el-col>
          <el-col :span="24">
            <el-form-item label="备注">
              <el-input v-model="form.remark" type="textarea" :rows="2" />
            </el-form-item>
          </el-col>
        </el-row>
      </el-form>

      <el-row v-if="form.id" :gutter="12" class="summary-stats">
        <el-col :span="3"><el-statistic title="商品数量" :value="Number(form.totalQuantity || 0)" /></el-col>
        <el-col :span="3"><el-statistic title="箱数" :value="Number(form.totalCartons || 0)" /></el-col>
        <el-col :span="3"><el-statistic title="总 CBM" :value="Number(form.totalCbm || 0)" /></el-col>
        <el-col :span="3"><el-statistic title="毛重 KG" :value="Number(form.totalGrossWeightKg || 0)" /></el-col>
        <el-col :span="4"><el-statistic title="采购金额" :value="Number(form.purchaseAmount || 0)" prefix="¥" /></el-col>
        <el-col :span="4"><el-statistic title="客户金额" :value="Number(form.salesAmount || 0)" prefix="¥" /></el-col>
        <el-col :span="4"><el-statistic title="预计利润" :value="Number(form.expectedProfit || 0)" prefix="¥" /></el-col>
      </el-row>

      <SummaryAllocationEditor
        v-if="form.id"
        :summary-id="form.id"
        :status="form.status || 'draft'"
        @changed="refreshCurrent"
      />

      <template #footer>
        <el-button @click="dialogVisible = false">关闭</el-button>
        <el-button v-if="editable" type="primary" @click="save">保存主单</el-button>
        <el-button v-if="form.id && editable" type="success" @click="confirmSummary(form)">确认汇总单</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { http } from '../api/http'
import SummaryAllocationEditor from '../components/SummaryAllocationEditor.vue'
import { formatRmb } from '../utils/rmb'

const rows = ref<any[]>([])
const customers = ref<any[]>([])
const customerId = ref<number | null>(null)
const dialogVisible = ref(false)
const form = reactive<any>({})
const editable = computed(() => !form.id || form.status === 'draft')

function reset() {
  Object.assign(form, {
    id: 0,
    no: '',
    customerId: null,
    orderDate: new Date().toISOString().slice(0, 10),
    containerType: '40HQ',
    warehouseId: null,
    plannedDeliveryDate: '',
    status: 'draft',
    totalQuantity: 0,
    totalCartons: 0,
    totalCbm: 0,
    totalGrossWeightKg: 0,
    totalNetWeightKg: 0,
    purchaseAmount: 0,
    salesAmount: 0,
    expectedProfit: 0,
    remark: '',
  })
}

async function loadCustomers() {
  customers.value = (await http.get('/customers')).data || []
}

async function load() {
  const params: Record<string, number> = {}
  if (customerId.value) params.customerId = customerId.value
  rows.value = (await http.get('/customer-summaries', { params })).data || []
}

function openCreate() {
  reset()
  dialogVisible.value = true
}

async function openDocument(row: any) {
  const id = row.summary?.id || row.id
  const response = await http.get(`/customer-summaries/${id}`)
  Object.assign(form, response.data.summary)
  dialogVisible.value = true
}

async function refreshCurrent() {
  if (!form.id) return
  const response = await http.get(`/customer-summaries/${form.id}`)
  Object.assign(form, response.data.summary)
  await load()
}

async function save() {
  if (!form.customerId) return ElMessage.warning('请选择客户')
  const payload = {
    customerId: form.customerId,
    orderDate: form.orderDate || null,
    containerType: form.containerType || null,
    warehouseId: form.warehouseId || null,
    plannedDeliveryDate: form.plannedDeliveryDate || null,
    remark: form.remark || null,
  }
  const response = form.id
    ? await http.put(`/customer-summaries/${form.id}`, payload)
    : await http.post('/customer-summaries', payload)
  Object.assign(form, response.data)
  ElMessage.success('汇总单主信息已保存')
  await load()
}

async function confirmSummary(summary: any) {
  if (!summary.id) return
  await ElMessageBox.confirm('确认后已有汇总商品将锁定，是否继续？', '确认客户汇总单')
  await http.post(`/customer-summaries/${summary.id}/confirm`)
  ElMessage.success('客户汇总单已确认')
  if (form.id === summary.id) await refreshCurrent()
  else await load()
}

async function cancelSummary(summary: any) {
  await ElMessageBox.confirm('取消后将释放该草稿占用的全部箱数，是否继续？', '取消客户汇总单')
  await http.delete(`/customer-summaries/${summary.id}`)
  ElMessage.success('客户汇总单已取消，预留数量已释放')
  if (form.id === summary.id) dialogVisible.value = false
  await load()
}

function customerName(id: number) {
  return customers.value.find(customer => customer.id === id)?.name || `客户 ${id}`
}

function statusLabel(status: string) {
  return ({
    draft: '草稿',
    confirmed: '已确认',
    receiving: '收货中',
    qc_in_progress: '验货中',
    ready_to_load: '待装柜',
    loaded: '已装柜',
    completed: '已完成',
    cancelled: '已取消',
  } as Record<string, string>)[status] || status || '草稿'
}

function statusTagType(status: string) {
  if (status === 'confirmed') return 'success'
  if (status === 'cancelled') return 'info'
  if (status === 'loaded' || status === 'completed') return 'warning'
  return ''
}

function dateText(value: string | null | undefined) {
  return value ? String(value).slice(0, 10) : ''
}

function numberText(value: number | string | null | undefined) {
  return Number(value || 0).toLocaleString('zh-CN', { maximumFractionDigits: 2 })
}

onMounted(async () => {
  reset()
  await Promise.all([loadCustomers(), load()])
})
</script>

<style scoped>
.page-subtitle { margin-top: 4px; color: #64748b; font-size: 13px; }
.document-no { font-weight: 700; }
.summary-stats { margin: 4px 0 18px; padding: 14px 8px; border: 1px solid #e5e7eb; border-radius: 10px; background: #f8fafc; }
</style>
