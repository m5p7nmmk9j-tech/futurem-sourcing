<template>
  <div class="page">
    <div class="page-header">
      <div>
        <div class="page-title">装柜单</div>
        <div class="page-subtitle">从同一客户、同一仓库的实际库存选货；草稿库存锁定固定72小时。</div>
      </div>
      <el-button type="primary" @click="openCreate">新增装柜单</el-button>
    </div>

    <div class="card">
      <div class="toolbar">
        <el-select v-model="filters.customerId" clearable filterable placeholder="客户" style="width: 220px" @change="load">
          <el-option v-for="customer in customers" :key="customer.id" :label="customer.name" :value="customer.id" />
        </el-select>
        <el-select v-model="filters.warehouseId" clearable filterable placeholder="仓库" style="width: 200px" @change="load">
          <el-option v-for="row in warehouses" :key="row.warehouse.id" :label="row.warehouse.name" :value="row.warehouse.id" />
        </el-select>
        <el-button @click="load">刷新</el-button>
      </div>

      <el-table :data="rows" border stripe>
        <el-table-column label="装柜单号" width="180">
          <template #default="scope"><el-button link type="primary" class="document-no" @click="openDocument(scope.row)">{{ scope.row.no }}</el-button></template>
        </el-table-column>
        <el-table-column label="客户" min-width="150"><template #default="scope">{{ customerName(scope.row.customerId) }}</template></el-table-column>
        <el-table-column label="仓库" min-width="130"><template #default="scope">{{ warehouseName(scope.row.warehouseId) }}</template></el-table-column>
        <el-table-column prop="containerType" label="柜型" width="100" />
        <el-table-column prop="containerNo" label="柜号" width="150" />
        <el-table-column prop="sealNo" label="封条号" width="140" />
        <el-table-column label="装柜日期" width="125"><template #default="scope">{{ dateText(scope.row.loadDate) }}</template></el-table-column>
        <el-table-column label="箱数" width="90" align="right"><template #default="scope">{{ numberText(scope.row.totalCartons) }}</template></el-table-column>
        <el-table-column label="CBM" width="90" align="right"><template #default="scope">{{ numberText(scope.row.totalCbm) }}</template></el-table-column>
        <el-table-column label="毛重 KG" width="110" align="right"><template #default="scope">{{ numberText(scope.row.totalGwKg) }}</template></el-table-column>
        <el-table-column label="状态" width="120"><template #default="scope"><el-tag :type="statusType(scope.row.status)">{{ statusLabel(scope.row.status) }}</el-tag></template></el-table-column>
        <el-table-column label="锁定到期" width="170"><template #default="scope">{{ dateTimeText(scope.row.inventoryLockExpiresAt) }}</template></el-table-column>
        <el-table-column label="操作" width="260" fixed="right">
          <template #default="scope">
            <el-button size="small" @click="openDocument(scope.row)">查看</el-button>
            <el-button size="small" type="primary" @click="loadUtilization(scope.row)">容量</el-button>
            <el-button v-if="scope.row.status === 'draft' || scope.row.status === 'lock_expired'" size="small" @click="copy(scope.row.id)">复制</el-button>
            <el-button v-if="scope.row.status === 'draft' || scope.row.status === 'lock_expired' || scope.row.status === 'inventory_locked'" size="small" type="danger" @click="remove(scope.row.id)">取消</el-button>
          </template>
        </el-table-column>
      </el-table>

      <el-alert v-if="utilization.message" :type="utilization.level === 'danger' ? 'error' : utilization.level === 'warning' ? 'warning' : 'success'" :title="utilization.message" show-icon style="margin-top: 12px" />
    </div>

    <el-dialog v-model="dialogVisible" :title="form.id ? `装柜单：${form.no || ''}` : '新增装柜单'" width="96%" destroy-on-close>
      <el-form label-width="105px" :disabled="form.status === 'confirmed' || form.status === 'completed'">
        <el-row :gutter="16">
          <el-col :span="6">
            <el-form-item label="客户">
              <el-select v-model="form.customerId" filterable placeholder="选择客户" style="width: 100%" :disabled="sourceLocked">
                <el-option v-for="customer in customers" :key="customer.id" :label="customer.name" :value="customer.id" />
              </el-select>
            </el-form-item>
          </el-col>
          <el-col :span="6">
            <el-form-item label="仓库">
              <el-select v-model="form.warehouseId" filterable placeholder="选择仓库" style="width: 100%" :disabled="sourceLocked">
                <el-option v-for="row in warehouses" :key="row.warehouse.id" :label="row.warehouse.name" :value="row.warehouse.id" />
              </el-select>
            </el-form-item>
          </el-col>
          <el-col :span="4"><el-form-item label="柜型"><el-select v-model="form.containerType" style="width: 100%"><el-option label="20GP" value="20GP" /><el-option label="40GP" value="40GP" /><el-option label="40HQ" value="40HQ" /><el-option label="45HQ" value="45HQ" /></el-select></el-form-item></el-col>
          <el-col :span="4"><el-form-item label="装柜日期"><el-date-picker v-model="form.loadDate" type="date" value-format="YYYY-MM-DD" style="width: 100%" /></el-form-item></el-col>
          <el-col :span="4"><el-form-item label="状态"><el-input :model-value="statusLabel(form.status)" disabled /></el-form-item></el-col>
          <el-col :span="6"><el-form-item label="柜号"><el-input v-model="form.containerNo" /></el-form-item></el-col>
          <el-col :span="6"><el-form-item label="封条号"><el-input v-model="form.sealNo" /></el-form-item></el-col>
          <el-col :span="12"><el-form-item label="备注"><el-input v-model="form.remark" /></el-form-item></el-col>
        </el-row>
      </el-form>

      <el-row v-if="form.id" :gutter="12" class="summary-stats">
        <el-col :span="6"><el-statistic title="锁定箱数" :value="Number(form.totalCartons || 0)" /></el-col>
        <el-col :span="6"><el-statistic title="总体积 CBM" :value="Number(form.totalCbm || 0)" /></el-col>
        <el-col :span="6"><el-statistic title="总毛重 KG" :value="Number(form.totalGwKg || 0)" /></el-col>
        <el-col :span="6"><el-statistic title="锁定批次数" :value="activeReservations.length" /></el-col>
      </el-row>

      <ContainerReservationStatus
        v-if="form.id"
        :status="form.status"
        :expires-at="form.inventoryLockExpiresAt"
        @release="releaseInventory"
        @relock="relockInventory"
      />

      <InventoryPicker
        v-if="form.id && (form.status === 'draft' || form.status === 'lock_expired')"
        ref="inventoryPicker"
        :customer-id="form.customerId"
        :warehouse-id="form.warehouseId"
        @change="selectedInventory = $event"
      />

      <div v-if="activeReservations.length" class="locked-items">
        <div class="section-title">已锁定库存</div>
        <el-table :data="activeReservations" border stripe size="small" max-height="360">
          <el-table-column prop="lot.lotNo" label="批次" width="150" />
          <el-table-column prop="product.customerItemNo" label="客户货号" width="130" />
          <el-table-column prop="product.customerBarcode" label="客户条码" width="150" />
          <el-table-column prop="product.nameCn" label="商品名称" min-width="180" />
          <el-table-column label="锁定数量" width="110" align="right"><template #default="scope">{{ numberText(scope.row.reservation.reservedQuantity) }}</template></el-table-column>
          <el-table-column label="锁定箱数" width="110" align="right"><template #default="scope">{{ numberText(scope.row.reservation.reservedCartons) }}</template></el-table-column>
          <el-table-column label="锁定时间" width="170"><template #default="scope">{{ dateTimeText(scope.row.reservation.lockedAt) }}</template></el-table-column>
          <el-table-column label="到期时间" width="170"><template #default="scope">{{ dateTimeText(scope.row.reservation.expiresAt) }}</template></el-table-column>
        </el-table>
      </div>

      <template #footer>
        <el-button @click="dialogVisible = false">关闭</el-button>
        <el-button v-if="form.status === 'draft' || form.status === 'lock_expired' || form.status === 'inventory_locked'" @click="save">保存主单</el-button>
        <el-button v-if="form.id && form.status === 'draft'" type="primary" :disabled="selectedInventory.length === 0" @click="lockInventory">锁定库存72小时</el-button>
        <el-button v-if="form.id && form.status === 'lock_expired'" type="primary" :disabled="selectedInventory.length === 0" @click="relockInventory">重新锁定库存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { http } from '../api/http'
import InventoryPicker from '../components/InventoryPicker.vue'
import ContainerReservationStatus from '../components/ContainerReservationStatus.vue'

const rows = ref<any[]>([])
const customers = ref<any[]>([])
const warehouses = ref<any[]>([])
const dialogVisible = ref(false)
const inventoryPicker = ref<any>(null)
const selectedInventory = ref<any[]>([])
const reservationRows = ref<any[]>([])
const utilization = reactive<any>({})
const filters = reactive<any>({ customerId: null, warehouseId: null })
const form = reactive<any>({})

const activeReservations = computed(() => reservationRows.value.filter(row => row.reservation?.status === 'active'))
const sourceLocked = computed(() => form.status === 'inventory_locked' || activeReservations.value.length > 0)

function reset() {
  Object.assign(form, {
    id: 0,
    no: '',
    customerId: null,
    warehouseId: null,
    summaryOrderId: null,
    containerType: '40HQ',
    containerNo: '',
    sealNo: '',
    loadDate: new Date().toISOString().slice(0, 10),
    status: 'draft',
    inventoryLockedAt: null,
    inventoryLockExpiresAt: null,
    totalCartons: 0,
    totalCbm: 0,
    totalGwKg: 0,
    remark: ''
  })
  selectedInventory.value = []
  reservationRows.value = []
}

async function loadMasterData() {
  const [customerResponse, warehouseResponse] = await Promise.all([http.get('/customers'), http.get('/warehouses')])
  customers.value = customerResponse.data || []
  warehouses.value = warehouseResponse.data || []
}
async function load() {
  const params: Record<string, number> = {}
  if (filters.customerId) params.customerId = filters.customerId
  if (filters.warehouseId) params.warehouseId = filters.warehouseId
  rows.value = (await http.get('/container-loads', { params })).data || []
}
function openCreate() { reset(); dialogVisible.value = true }
async function openDocument(row: any) {
  const response = await http.get(`/container-loads/${row.id}`)
  Object.assign(form, response.data)
  selectedInventory.value = []
  await loadReservations()
  dialogVisible.value = true
}
async function loadReservations() {
  if (!form.id) return
  const response = await http.get(`/container-loads/${form.id}/reservations`)
  Object.assign(form, response.data.container)
  reservationRows.value = response.data.items || []
}
async function save() {
  if (!form.customerId) return ElMessage.warning('请选择客户')
  if (!form.warehouseId) return ElMessage.warning('请选择仓库')
  if (!form.containerType) return ElMessage.warning('请选择柜型')
  const payload = {
    customerId: form.customerId,
    warehouseId: form.warehouseId,
    summaryOrderId: form.summaryOrderId,
    containerType: form.containerType,
    containerNo: form.containerNo,
    sealNo: form.sealNo,
    loadDate: form.loadDate || null,
    remark: form.remark
  }
  const response = form.id
    ? await http.put(`/container-loads/${form.id}`, payload)
    : await http.post('/container-loads', payload)
  Object.assign(form, response.data)
  ElMessage.success('装柜主单已保存；普通保存不会延长库存锁定时间')
  await load()
}
async function lockInventory() {
  if (!selectedInventory.value.length) return ElMessage.warning('请选择要锁定的库存')
  await http.post(`/container-loads/${form.id}/lock-inventory`, { items: selectedInventory.value })
  ElMessage.success('库存已锁定72小时')
  await Promise.all([loadReservations(), load()])
}
async function relockInventory() {
  if (!selectedInventory.value.length) return ElMessage.warning('请重新选择要锁定的库存')
  await http.post(`/container-loads/${form.id}/relock-inventory`, { items: selectedInventory.value })
  ElMessage.success('库存已重新锁定72小时')
  await Promise.all([loadReservations(), load()])
}
async function releaseInventory() {
  const result = await ElMessageBox.prompt('请输入释放原因', '释放库存锁定', { inputPattern: /\S+/, inputErrorMessage: '必须填写原因' })
  await http.post(`/container-loads/${form.id}/release-inventory`, { reason: result.value })
  ElMessage.success('库存锁定已释放')
  await Promise.all([loadReservations(), load()])
  await inventoryPicker.value?.load?.()
}
async function copy(id: number) { await http.post(`/container-loads/${id}/copy`); ElMessage.success('装柜主单已复制，库存需要重新选择'); await load() }
async function remove(id: number) { await ElMessageBox.confirm('取消装柜草稿将立即释放已锁定库存，是否继续？', '取消装柜单'); await http.delete(`/container-loads/${id}`); ElMessage.success('装柜草稿已取消'); if (form.id === id) dialogVisible.value = false; await load() }
async function loadUtilization(row: any) { const response = await http.get(`/container-loads/${row.id}/utilization`); Object.assign(utilization, response.data) }
function customerName(id?: number | null) { return customers.value.find(x => x.id === id)?.name || '' }
function warehouseName(id?: number | null) { return warehouses.value.find(x => x.warehouse.id === id)?.warehouse.name || '' }
function numberText(value: unknown) { return Number(value || 0).toLocaleString('zh-CN', { maximumFractionDigits: 2 }) }
function dateText(value?: string | null) { return value ? String(value).slice(0, 10) : '' }
function dateTimeText(value?: string | null) { return value ? new Date(value).toLocaleString('zh-CN', { hour12: false }) : '' }
function statusLabel(status?: string | null) { return ({ draft: '草稿', inventory_locked: '库存已锁定', lock_expired: '锁定已过期', confirmed: '已装柜', shipment_created: '已生成出运', completed: '已完成', cancelled: '已取消' } as Record<string, string>)[status || ''] || status || '草稿' }
function statusType(status?: string | null) { if (status === 'inventory_locked') return 'success'; if (status === 'lock_expired') return 'warning'; if (status === 'cancelled') return 'info'; if (status === 'confirmed' || status === 'completed') return 'primary'; return '' }

onMounted(async () => { reset(); await Promise.all([loadMasterData(), load()]) })
</script>

<style scoped>
.page-subtitle { margin-top: 4px; color: #64748b; font-size: 13px; }
.toolbar { display: flex; gap: 10px; margin-bottom: 14px; }
.document-no { font-weight: 700; }
.summary-stats { margin: 8px 0 14px; padding: 12px; border-radius: 10px; background: #f8fafc; border: 1px solid #e5e7eb; }
.locked-items { margin-top: 16px; }
.section-title { margin-bottom: 10px; font-weight: 700; color: #1e293b; }
</style>
