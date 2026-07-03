<template>
  <div class="page">
    <div class="page-header">
      <div class="page-title">PO 采购订单</div>
      <el-button type="primary" @click="openCreate">新增 PO</el-button>
    </div>

    <div class="card">
      <div class="toolbar">
        <el-select v-model="supplierId" placeholder="按供应商筛选" clearable filterable style="width: 260px" @change="load">
          <el-option v-for="s in suppliers" :key="s.id" :label="s.name" :value="s.id" />
        </el-select>
        <el-select v-model="customerId" placeholder="按客户筛选" clearable filterable style="width: 260px" @change="load">
          <el-option v-for="c in customers" :key="c.id" :label="c.name" :value="c.id" />
        </el-select>
        <el-button @click="load">刷新</el-button>
      </div>

      <el-table :data="rows" border stripe>
        <el-table-column prop="no" label="PO单号" width="190" />
        <el-table-column prop="supplierId" label="供应商ID" width="100" />
        <el-table-column prop="customerId" label="客户ID" width="100" />
        <el-table-column prop="customerOrderId" label="来源CO ID" width="110" />
        <el-table-column prop="orderDate" label="下单日期" width="150" />
        <el-table-column prop="expectedDeliveryDate" label="交货期" width="150" />
        <el-table-column prop="currency" label="币种" width="90" />
        <el-table-column prop="status" label="状态" width="110" />
        <el-table-column prop="payStatus" label="付款状态" width="110" />
        <el-table-column prop="remark" label="备注" min-width="220" />
        <el-table-column label="操作" width="240" fixed="right">
          <template #default="scope">
            <el-button size="small" @click="openEdit(scope.row)">编辑</el-button>
            <el-button size="small" @click="copy(scope.row.id)">复制</el-button>
            <el-button size="small" type="danger" @click="remove(scope.row.id)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>
    </div>

    <el-dialog v-model="dialogVisible" :title="form.id ? '编辑 PO' : '新增 PO'" width="620px">
      <el-alert title="PO 是给供应商下单和付款的依据；采购价、包装、CBM、KG 后续放在 PO 明细。" type="info" show-icon style="margin-bottom: 12px" />
      <el-form label-width="110px">
        <el-form-item label="供应商">
          <el-select v-model="form.supplierId" filterable placeholder="选择供应商" style="width: 100%">
            <el-option v-for="s in suppliers" :key="s.id" :label="s.name" :value="s.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="客户">
          <el-select v-model="form.customerId" filterable clearable placeholder="选择客户" style="width: 100%">
            <el-option v-for="c in customers" :key="c.id" :label="c.name" :value="c.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="来源CO ID"><el-input-number v-model="form.customerOrderId" :min="0" style="width: 100%" /></el-form-item>
        <el-form-item label="下单日期"><el-date-picker v-model="form.orderDate" type="date" value-format="YYYY-MM-DD" style="width: 100%" /></el-form-item>
        <el-form-item label="预计交货期"><el-date-picker v-model="form.expectedDeliveryDate" type="date" value-format="YYYY-MM-DD" style="width: 100%" /></el-form-item>
        <el-form-item label="币种"><el-input v-model="form.currency" /></el-form-item>
        <el-form-item label="状态"><el-input v-model="form.status" /></el-form-item>
        <el-form-item label="付款状态"><el-input v-model="form.payStatus" /></el-form-item>
        <el-form-item label="备注"><el-input v-model="form.remark" type="textarea" /></el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible=false">取消</el-button>
        <el-button type="primary" @click="save">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { http } from '../api/http'

const rows = ref<any[]>([])
const suppliers = ref<any[]>([])
const customers = ref<any[]>([])
const supplierId = ref<number | null>(null)
const customerId = ref<number | null>(null)
const dialogVisible = ref(false)
const form = reactive<any>({ id: 0, supplierId: null, customerId: null, customerOrderId: null, orderDate: '', expectedDeliveryDate: '', currency: 'CNY', status: 'draft', payStatus: 'unpaid', remark: '' })

async function loadSuppliers() { const res = await http.get('/suppliers'); suppliers.value = res.data }
async function loadCustomers() { const res = await http.get('/customers'); customers.value = res.data }
async function load() {
  const params: any = {}
  if (supplierId.value) params.supplierId = supplierId.value
  if (customerId.value) params.customerId = customerId.value
  const res = await http.get('/purchase-orders', { params })
  rows.value = res.data
}
function reset() { Object.assign(form, { id: 0, supplierId: null, customerId: null, customerOrderId: null, orderDate: '', expectedDeliveryDate: '', currency: 'CNY', status: 'draft', payStatus: 'unpaid', remark: '' }) }
function openCreate() { reset(); dialogVisible.value = true }
function openEdit(row: any) { Object.assign(form, row); dialogVisible.value = true }
async function save() {
  if (!form.supplierId) return ElMessage.warning('请选择供应商')
  if (form.id) await http.put(`/purchase-orders/${form.id}`, form)
  else await http.post('/purchase-orders', form)
  dialogVisible.value = false
  ElMessage.success('保存成功')
  await load()
}
async function copy(id: number) { await http.post(`/purchase-orders/${id}/copy`); ElMessage.success('复制成功'); await load() }
async function remove(id: number) { await ElMessageBox.confirm('确认删除该 PO？', '提示'); await http.delete(`/purchase-orders/${id}`); ElMessage.success('已删除'); await load() }

onMounted(async () => { await loadSuppliers(); await loadCustomers(); await load() })
</script>
