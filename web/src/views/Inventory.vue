<template>
  <div class="page">
    <div class="page-header">
      <div>
        <div class="page-title">仓库库存</div>
        <div class="page-subtitle">查看在库、锁定、可用、箱数、体积、重量和完整来源链。</div>
      </div>
      <el-button @click="load">刷新</el-button>
    </div>

    <div class="card">
      <div class="toolbar">
        <el-input v-model="filters.keyword" clearable placeholder="货号 / 条码 / 商品 / PO / 收货单 / 汇总单 / 批次" style="width: 360px" @keyup.enter="load" />
        <el-select v-model="filters.warehouseId" clearable filterable placeholder="仓库" style="width: 200px">
          <el-option v-for="row in warehouses" :key="row.warehouse.id" :label="row.warehouse.name" :value="row.warehouse.id" />
        </el-select>
        <el-select v-model="filters.status" clearable placeholder="库存状态" style="width: 150px">
          <el-option label="可用" value="available" />
          <el-option label="已耗尽" value="depleted" />
        </el-select>
        <el-button type="primary" @click="load">查询</el-button>
        <el-button @click="resetFilters">重置</el-button>
      </div>

      <el-table :data="rows" border stripe>
        <el-table-column prop="lot.lotNo" label="批次号" width="165" />
        <el-table-column prop="customer.name" label="客户" min-width="150" />
        <el-table-column prop="product.customerItemNo" label="客户货号" width="130" />
        <el-table-column prop="product.customerBarcode" label="客户条码" width="150" />
        <el-table-column prop="product.nameCn" label="商品名称" min-width="180" />
        <el-table-column prop="purchaseOrder.no" label="PO" width="140" />
        <el-table-column prop="summary.no" label="原汇总单" width="140" />
        <el-table-column prop="receiving.no" label="收货单" width="140" />
        <el-table-column prop="supplier.name" label="供应商" min-width="150" />
        <el-table-column prop="warehouse.name" label="仓库" width="120" />
        <el-table-column prop="location.code" label="库位" width="100" />
        <el-table-column label="在库数量" width="105" align="right"><template #default="scope"><el-button link type="primary" @click="openTransactions(scope.row)">{{ numberText(scope.row.lot.onHandQuantity) }}</el-button></template></el-table-column>
        <el-table-column label="锁定数量" width="105" align="right"><template #default="scope">{{ numberText(scope.row.lot.lockedQuantity) }}</template></el-table-column>
        <el-table-column label="可用数量" width="105" align="right"><template #default="scope">{{ numberText(scope.row.lot.availableQuantity) }}</template></el-table-column>
        <el-table-column label="在库箱数" width="100" align="right"><template #default="scope">{{ numberText(scope.row.lot.onHandCartons) }}</template></el-table-column>
        <el-table-column label="可用箱数" width="100" align="right"><template #default="scope">{{ numberText(scope.row.lot.availableCartons) }}</template></el-table-column>
        <el-table-column label="CBM" width="90" align="right"><template #default="scope">{{ numberText(scope.row.lot.onHandCartons * scope.row.lot.cartonCbm) }}</template></el-table-column>
        <el-table-column label="毛重 KG" width="105" align="right"><template #default="scope">{{ numberText(scope.row.lot.onHandCartons * scope.row.lot.cartonGwKg) }}</template></el-table-column>
        <el-table-column label="操作" width="150" fixed="right"><template #default="scope"><el-button size="small" @click="openTransactions(scope.row)">流水</el-button><el-button size="small" type="warning" @click="openAdjust(scope.row)">调整</el-button></template></el-table-column>
      </el-table>
    </div>

    <el-dialog v-model="transactionsVisible" :title="`库存流水：${currentRow?.lot?.lotNo || ''}`" width="920px">
      <el-table :data="transactions" border stripe size="small">
        <el-table-column prop="createdAt" label="时间" width="170" />
        <el-table-column prop="transactionType" label="类型" width="130" />
        <el-table-column prop="sourceType" label="来源" width="120" />
        <el-table-column prop="sourceId" label="来源ID" width="100" />
        <el-table-column prop="quantityDelta" label="数量变化" width="105" align="right" />
        <el-table-column prop="cartonsDelta" label="箱数变化" width="100" align="right" />
        <el-table-column prop="quantityBalance" label="数量余额" width="105" align="right" />
        <el-table-column prop="cartonsBalance" label="箱数余额" width="100" align="right" />
        <el-table-column prop="reason" label="原因" min-width="180" />
      </el-table>
    </el-dialog>

    <el-dialog v-model="adjustVisible" title="库存调整" width="520px">
      <el-alert title="调整后库存不能为负数，也不能低于已锁定数量。" type="warning" show-icon style="margin-bottom: 14px" />
      <el-form label-width="110px">
        <el-form-item label="数量变化"><el-input-number v-model="adjustForm.quantityDelta" :precision="2" style="width: 100%" /></el-form-item>
        <el-form-item label="箱数变化"><el-input-number v-model="adjustForm.cartonsDelta" :precision="2" style="width: 100%" /></el-form-item>
        <el-form-item label="调整原因"><el-input v-model="adjustForm.reason" type="textarea" :rows="3" /></el-form-item>
      </el-form>
      <template #footer><el-button @click="adjustVisible = false">取消</el-button><el-button type="primary" @click="submitAdjust">确认调整</el-button></template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { http } from '../api/http'

const rows = ref<any[]>([])
const warehouses = ref<any[]>([])
const transactions = ref<any[]>([])
const currentRow = ref<any>(null)
const transactionsVisible = ref(false)
const adjustVisible = ref(false)
const filters = reactive<any>({ keyword: '', warehouseId: null, status: '' })
const adjustForm = reactive<any>({ quantityDelta: 0, cartonsDelta: 0, reason: '' })

async function loadWarehouses() { warehouses.value = (await http.get('/warehouses')).data || [] }
async function load() {
  const params: Record<string, any> = {}
  Object.entries(filters).forEach(([key, value]) => { if (value !== '' && value !== null && value !== undefined) params[key] = value })
  rows.value = (await http.get('/inventory', { params })).data || []
}
function resetFilters() { Object.assign(filters, { keyword: '', warehouseId: null, status: '' }); load() }
async function openTransactions(row: any) {
  currentRow.value = row
  const response = await http.get(`/inventory/${row.lot.id}`)
  transactions.value = response.data.transactions || []
  transactionsVisible.value = true
}
function openAdjust(row: any) {
  currentRow.value = row
  Object.assign(adjustForm, { quantityDelta: 0, cartonsDelta: 0, reason: '' })
  adjustVisible.value = true
}
async function submitAdjust() {
  if (!adjustForm.reason.trim()) return ElMessage.warning('请填写调整原因')
  if (Number(adjustForm.quantityDelta) === 0 && Number(adjustForm.cartonsDelta) === 0) return ElMessage.warning('数量和箱数不能同时为 0')
  await http.post(`/inventory/${currentRow.value.lot.id}/adjust`, adjustForm)
  ElMessage.success('库存已调整')
  adjustVisible.value = false
  await load()
}
function numberText(value: unknown) { return Number(value || 0).toLocaleString('zh-CN', { maximumFractionDigits: 2 }) }

onMounted(async () => { await Promise.all([loadWarehouses(), load()]) })
</script>

<style scoped>
.page-subtitle { margin-top: 4px; color: #64748b; font-size: 13px; }
.toolbar { display: flex; gap: 10px; align-items: center; margin-bottom: 14px; flex-wrap: wrap; }
</style>
