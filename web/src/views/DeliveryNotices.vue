<template>
  <div class="page">
    <div class="page-header">
      <div>
        <div class="page-title">供应商送货通知</div>
        <div class="page-subtitle">同一供应商、汇总单、计划日期和仓库自动合并；支持发布、供应商确认和分批收货。</div>
      </div>
      <el-button @click="load">刷新</el-button>
    </div>

    <div class="card">
      <div class="toolbar">
        <el-select v-model="supplierId" placeholder="按供应商筛选" clearable filterable style="width: 260px" @change="load">
          <el-option v-for="supplier in suppliers" :key="supplier.id" :label="supplier.name" :value="supplier.id" />
        </el-select>
        <el-select v-model="status" placeholder="按状态筛选" clearable style="width: 170px" @change="load">
          <el-option label="草稿" value="draft" />
          <el-option label="已发布" value="published" />
          <el-option label="供应商已确认" value="supplier_confirmed" />
          <el-option label="部分收货" value="partially_received" />
          <el-option label="已收齐" value="received" />
        </el-select>
      </div>

      <el-table :data="rows" border stripe>
        <el-table-column label="通知单号" width="170">
          <template #default="scope">
            <el-button link type="primary" @click="open(scope.row)">{{ scope.row.notice.no }}</el-button>
          </template>
        </el-table-column>
        <el-table-column label="汇总单 ID" width="105">
          <template #default="scope">{{ scope.row.notice.summaryOrderId }}</template>
        </el-table-column>
        <el-table-column label="供应商" min-width="170">
          <template #default="scope">{{ supplierName(scope.row.notice.supplierId) }}</template>
        </el-table-column>
        <el-table-column label="仓库 ID" width="90">
          <template #default="scope">{{ scope.row.notice.warehouseId }}</template>
        </el-table-column>
        <el-table-column label="计划日期" width="120">
          <template #default="scope">{{ dateText(scope.row.notice.plannedDeliveryDate) }}</template>
        </el-table-column>
        <el-table-column prop="lineCount" label="商品数" width="85" />
        <el-table-column label="计划箱数" width="100" align="right">
          <template #default="scope">{{ numberText(scope.row.notice.totalCartons) }}</template>
        </el-table-column>
        <el-table-column label="已收箱数" width="100" align="right">
          <template #default="scope">{{ numberText(scope.row.notice.receivedCartons) }}</template>
        </el-table-column>
        <el-table-column label="计划数量" width="105" align="right">
          <template #default="scope">{{ numberText(scope.row.notice.totalQuantity) }}</template>
        </el-table-column>
        <el-table-column label="已收数量" width="105" align="right">
          <template #default="scope">{{ numberText(scope.row.notice.receivedQuantity) }}</template>
        </el-table-column>
        <el-table-column label="状态" width="120">
          <template #default="scope"><el-tag :type="statusType(scope.row.notice.status)">{{ statusLabel(scope.row.notice.status) }}</el-tag></template>
        </el-table-column>
        <el-table-column label="操作" width="250" fixed="right">
          <template #default="scope">
            <el-button size="small" @click="open(scope.row)">查看/收货</el-button>
            <el-button v-if="scope.row.notice.status === 'draft'" size="small" type="primary" @click="publish(scope.row.notice)">发布</el-button>
            <el-button v-if="scope.row.notice.status === 'published'" size="small" type="success" @click="supplierConfirm(scope.row.notice)">供应商确认</el-button>
          </template>
        </el-table-column>
      </el-table>
    </div>

    <el-dialog v-model="visible" :title="`送货通知：${current.no || ''}`" width="94%" destroy-on-close>
      <el-descriptions :column="4" border>
        <el-descriptions-item label="汇总单 ID">{{ current.summaryOrderId }}</el-descriptions-item>
        <el-descriptions-item label="供应商">{{ supplierName(current.supplierId) }}</el-descriptions-item>
        <el-descriptions-item label="仓库 ID">{{ current.warehouseId }}</el-descriptions-item>
        <el-descriptions-item label="计划日期">{{ dateText(current.plannedDeliveryDate) }}</el-descriptions-item>
        <el-descriptions-item label="计划箱数">{{ numberText(current.totalCartons) }}</el-descriptions-item>
        <el-descriptions-item label="已收箱数">{{ numberText(current.receivedCartons) }}</el-descriptions-item>
        <el-descriptions-item label="计划数量">{{ numberText(current.totalQuantity) }}</el-descriptions-item>
        <el-descriptions-item label="已收数量">{{ numberText(current.receivedQuantity) }}</el-descriptions-item>
        <el-descriptions-item label="状态">{{ statusLabel(current.status) }}</el-descriptions-item>
        <el-descriptions-item label="发布时间">{{ dateTimeText(current.publishedAt) }}</el-descriptions-item>
        <el-descriptions-item label="供应商确认">{{ dateTimeText(current.supplierConfirmedAt) }}</el-descriptions-item>
      </el-descriptions>

      <DeliveryNoticeLines
        v-if="current.id"
        :notice-id="current.id"
        :status="current.status"
        @changed="refreshCurrent"
      />

      <template #footer>
        <el-button @click="visible = false">关闭</el-button>
        <el-button v-if="current.status === 'draft'" type="primary" @click="publish(current)">发布通知</el-button>
        <el-button v-if="current.status === 'published'" type="success" @click="supplierConfirm(current)">供应商确认</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { http } from '../api/http'
import DeliveryNoticeLines from '../components/DeliveryNoticeLines.vue'

const rows = ref<any[]>([])
const suppliers = ref<any[]>([])
const supplierId = ref<number | null>(null)
const status = ref('')
const visible = ref(false)
const current = reactive<any>({})

async function loadSuppliers() {
  suppliers.value = (await http.get('/suppliers')).data || []
}

async function load() {
  const params: Record<string, any> = {}
  if (supplierId.value) params.supplierId = supplierId.value
  if (status.value) params.status = status.value
  rows.value = (await http.get('/delivery-notices', { params })).data || []
}

async function open(row: any) {
  const response = await http.get(`/delivery-notices/${row.notice.id}`)
  Object.assign(current, response.data.notice)
  visible.value = true
}

async function refreshCurrent() {
  if (!current.id) return
  const response = await http.get(`/delivery-notices/${current.id}`)
  Object.assign(current, response.data.notice)
  await load()
}

async function publish(notice: any) {
  await ElMessageBox.confirm('发布后供应商可查看并确认送货资料，是否继续？', '发布送货通知')
  await http.post(`/delivery-notices/${notice.id}/publish`)
  ElMessage.success('送货通知已发布')
  if (current.id === notice.id) await refreshCurrent()
  else await load()
}

async function supplierConfirm(notice: any) {
  await ElMessageBox.confirm('确认供应商已接受本次送货安排？', '供应商确认')
  await http.post(`/delivery-notices/${notice.id}/supplier-confirm`)
  ElMessage.success('供应商已确认送货通知')
  if (current.id === notice.id) await refreshCurrent()
  else await load()
}

function supplierName(id: number) {
  return suppliers.value.find(supplier => supplier.id === id)?.name || `供应商 ${id}`
}

function statusLabel(value: string) {
  return ({
    draft: '草稿',
    published: '已发布',
    supplier_confirmed: '供应商已确认',
    partially_received: '部分收货',
    received: '已收齐',
    cancelled: '已取消',
  } as Record<string, string>)[value] || value
}

function statusType(value: string) {
  if (value === 'received') return 'success'
  if (value === 'partially_received') return 'warning'
  if (value === 'published' || value === 'supplier_confirmed') return 'primary'
  if (value === 'cancelled') return 'info'
  return ''
}

function dateText(value: string | null | undefined) {
  return value ? String(value).slice(0, 10) : ''
}

function dateTimeText(value: string | null | undefined) {
  return value ? new Date(value).toLocaleString('zh-CN') : '-'
}

function numberText(value: number | string | null | undefined) {
  return Number(value || 0).toLocaleString('zh-CN', { maximumFractionDigits: 2 })
}

onMounted(async () => {
  await Promise.all([loadSuppliers(), load()])
})
</script>

<style scoped>
.page-subtitle { margin-top: 4px; color: #64748b; font-size: 13px; }
</style>
