<template>
  <div class="page">
    <div class="page-header">
      <div class="page-title">收货 Receiving</div>
      <el-button type="primary" @click="openCreate">新增收货单</el-button>
    </div>

    <div class="card">
      <div class="toolbar">
        <el-select v-model="purchaseOrderId" placeholder="按PO筛选" clearable filterable style="width: 320px" @change="load">
          <el-option v-for="p in purchaseOrders" :key="p.id" :label="p.no" :value="p.id" />
        </el-select>
        <el-button @click="load">刷新</el-button>
      </div>

      <el-table :data="rows" border stripe>
        <el-table-column prop="no" label="收货单号" width="190" />
        <el-table-column prop="purchaseOrderId" label="PO ID" width="100" />
        <el-table-column prop="receiveDate" label="收货日期" width="150" />
        <el-table-column prop="warehouseLocation" label="仓库/库位" width="180" />
        <el-table-column prop="status" label="状态" width="110" />
        <el-table-column prop="remark" label="备注" min-width="220" />
        <el-table-column label="操作" width="180" fixed="right">
          <template #default="scope">
            <el-button size="small" @click="openEdit(scope.row)">编辑</el-button>
            <el-button size="small" type="danger" @click="remove(scope.row.id)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>
    </div>

    <el-dialog v-model="dialogVisible" :title="form.id ? '编辑收货单' : '新增收货单'" width="600px">
      <el-alert title="收货按 PO 执行，后续会增加收货明细、扫码收货、部分收货和异常记录。" type="info" show-icon style="margin-bottom: 12px" />
      <el-form label-width="110px">
        <el-form-item label="采购订单">
          <el-select v-model="form.purchaseOrderId" filterable placeholder="选择 PO" style="width: 100%">
            <el-option v-for="p in purchaseOrders" :key="p.id" :label="p.no" :value="p.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="收货日期"><el-date-picker v-model="form.receiveDate" type="date" value-format="YYYY-MM-DD" style="width: 100%" /></el-form-item>
        <el-form-item label="仓库/库位"><el-input v-model="form.warehouseLocation" /></el-form-item>
        <el-form-item label="状态"><el-input v-model="form.status" /></el-form-item>
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
const purchaseOrders = ref<any[]>([])
const purchaseOrderId = ref<number | null>(null)
const dialogVisible = ref(false)
const form = reactive<any>({ id: 0, purchaseOrderId: null, receiveDate: '', warehouseLocation: '', status: 'draft', remark: '' })

async function loadPurchaseOrders() { const res = await http.get('/purchase-orders'); purchaseOrders.value = res.data }
async function load() {
  const params: any = {}
  if (purchaseOrderId.value) params.purchaseOrderId = purchaseOrderId.value
  const res = await http.get('/receiving-orders', { params })
  rows.value = res.data
}
function reset() { Object.assign(form, { id: 0, purchaseOrderId: null, receiveDate: '', warehouseLocation: '', status: 'draft', remark: '' }) }
function openCreate() { reset(); dialogVisible.value = true }
function openEdit(row: any) { Object.assign(form, row); dialogVisible.value = true }
async function save() {
  if (!form.purchaseOrderId) return ElMessage.warning('请选择 PO')
  if (form.id) await http.put(`/receiving-orders/${form.id}`, form)
  else await http.post('/receiving-orders', form)
  dialogVisible.value = false
  ElMessage.success('保存成功')
  await load()
}
async function remove(id: number) {
  await ElMessageBox.confirm('确认删除该收货单？', '提示')
  await http.delete(`/receiving-orders/${id}`)
  ElMessage.success('已删除')
  await load()
}

onMounted(async () => { await loadPurchaseOrders(); await load() })
</script>
