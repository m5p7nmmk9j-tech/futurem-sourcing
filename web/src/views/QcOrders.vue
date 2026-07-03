<template>
  <div class="page">
    <div class="page-header">
      <div class="page-title">QC 验货</div>
      <el-button type="primary" @click="openCreate">新增 QC</el-button>
    </div>

    <div class="card">
      <div class="toolbar">
        <el-select v-model="purchaseOrderId" placeholder="按PO筛选" clearable filterable style="width: 280px" @change="load">
          <el-option v-for="p in purchaseOrders" :key="p.id" :label="p.no" :value="p.id" />
        </el-select>
        <el-select v-model="receivingOrderId" placeholder="按收货单筛选" clearable filterable style="width: 280px" @change="load">
          <el-option v-for="r in receivingOrders" :key="r.id" :label="r.no" :value="r.id" />
        </el-select>
        <el-button @click="load">刷新</el-button>
      </div>

      <el-table :data="rows" border stripe>
        <el-table-column prop="no" label="QC单号" width="190" />
        <el-table-column prop="purchaseOrderId" label="PO ID" width="100" />
        <el-table-column prop="receivingOrderId" label="收货单ID" width="110" />
        <el-table-column prop="qcDate" label="验货日期" width="150" />
        <el-table-column prop="result" label="结果" width="120" />
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

    <el-dialog v-model="dialogVisible" :title="form.id ? '编辑 QC' : '新增 QC'" width="620px">
      <el-alert title="QC 记录验货结果，后续会增加验货明细、问题类型、照片、处理结果和赔偿/补货。" type="info" show-icon style="margin-bottom: 12px" />
      <el-form label-width="110px">
        <el-form-item label="采购订单">
          <el-select v-model="form.purchaseOrderId" filterable clearable placeholder="选择 PO" style="width: 100%">
            <el-option v-for="p in purchaseOrders" :key="p.id" :label="p.no" :value="p.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="收货单">
          <el-select v-model="form.receivingOrderId" filterable clearable placeholder="选择收货单" style="width: 100%">
            <el-option v-for="r in receivingOrders" :key="r.id" :label="r.no" :value="r.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="验货日期"><el-date-picker v-model="form.qcDate" type="date" value-format="YYYY-MM-DD" style="width: 100%" /></el-form-item>
        <el-form-item label="验货结果"><el-select v-model="form.result" style="width:100%"><el-option label="待验" value="pending"/><el-option label="合格" value="passed"/><el-option label="不合格" value="failed"/><el-option label="部分合格" value="partial"/></el-select></el-form-item>
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
const receivingOrders = ref<any[]>([])
const purchaseOrderId = ref<number | null>(null)
const receivingOrderId = ref<number | null>(null)
const dialogVisible = ref(false)
const form = reactive<any>({ id: 0, purchaseOrderId: null, receivingOrderId: null, qcDate: '', status: 'draft', result: 'pending', remark: '' })

async function loadPurchaseOrders() { const res = await http.get('/purchase-orders'); purchaseOrders.value = res.data }
async function loadReceivingOrders() { const res = await http.get('/receiving-orders'); receivingOrders.value = res.data }
async function load() {
  const params: any = {}
  if (purchaseOrderId.value) params.purchaseOrderId = purchaseOrderId.value
  if (receivingOrderId.value) params.receivingOrderId = receivingOrderId.value
  const res = await http.get('/qc-orders', { params })
  rows.value = res.data
}
function reset() { Object.assign(form, { id: 0, purchaseOrderId: null, receivingOrderId: null, qcDate: '', status: 'draft', result: 'pending', remark: '' }) }
function openCreate() { reset(); dialogVisible.value = true }
function openEdit(row: any) { Object.assign(form, row); dialogVisible.value = true }
async function save() {
  if (!form.purchaseOrderId && !form.receivingOrderId) return ElMessage.warning('请选择 PO 或收货单')
  if (form.id) await http.put(`/qc-orders/${form.id}`, form)
  else await http.post('/qc-orders', form)
  dialogVisible.value = false
  ElMessage.success('保存成功')
  await load()
}
async function remove(id: number) {
  await ElMessageBox.confirm('确认删除该 QC？', '提示')
  await http.delete(`/qc-orders/${id}`)
  ElMessage.success('已删除')
  await load()
}

onMounted(async () => { await loadPurchaseOrders(); await loadReceivingOrders(); await load() })
</script>
