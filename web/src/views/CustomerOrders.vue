<template>
  <div class="page">
    <div class="page-header">
      <div class="page-title">CO 客户订单</div>
      <el-button type="primary" @click="openCreate">新增 CO</el-button>
    </div>

    <div class="card">
      <div class="toolbar">
        <el-select v-model="customerId" placeholder="按客户筛选" clearable filterable style="width: 260px" @change="load">
          <el-option v-for="c in customers" :key="c.id" :label="c.name" :value="c.id" />
        </el-select>
        <el-button @click="load">刷新</el-button>
      </div>

      <el-table :data="rows" border stripe>
        <el-table-column label="CO单号" width="190">
          <template #default="scope"><el-button link type="primary" class="document-no" @click="openDocument(scope.row)">{{ scope.row.no }}</el-button></template>
        </el-table-column>
        <el-table-column prop="customerId" label="客户ID" width="100" />
        <el-table-column prop="orderDate" label="订单日期" width="150" />
        <el-table-column prop="expectedDeliveryDate" label="交货期" width="150" />
        <el-table-column prop="totalCbm" label="总CBM" width="110" />
        <el-table-column prop="cartons" label="箱数" width="100" />
        <el-table-column prop="deliveryTerms" label="交货条款" min-width="150" />
        <el-table-column prop="paymentTerms" label="账期条款" min-width="150" />
        <el-table-column prop="currency" label="币种" width="90" />
        <el-table-column prop="status" label="状态" width="110" />
        <el-table-column prop="remark" label="备注" min-width="220" />
        <el-table-column label="操作" width="330" fixed="right">
          <template #default="scope">
            <el-button size="small" type="primary" @click="openPoDialog(scope.row)">生成PO</el-button>
            <el-button size="small" @click="openDocument(scope.row)">编辑</el-button>
            <el-button size="small" @click="copy(scope.row.id)">复制</el-button>
            <el-button size="small" type="danger" @click="remove(scope.row.id)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>
    </div>

    <el-dialog v-model="dialogVisible" :title="form.id ? `编辑 CO：${form.no || ''}` : '新增 CO'" width="92%" destroy-on-close>
      <el-alert title="CO 是给客户看的订单，销售价和箱规在明细里填写。保存主单后即可添加商品明细。" type="info" show-icon style="margin-bottom: 12px" />
      <el-form label-width="100px">
        <el-row :gutter="16">
          <el-col :span="12"><el-form-item label="客户"><el-select v-model="form.customerId" filterable placeholder="选择客户" style="width: 100%"><el-option v-for="c in customers" :key="c.id" :label="c.name" :value="c.id" /></el-select></el-form-item></el-col>
          <el-col :span="12"><el-form-item label="来源RFQ ID"><el-input-number v-model="form.rfqId" :min="0" style="width: 100%" /></el-form-item></el-col>
          <el-col :span="12"><el-form-item label="订单日期"><el-date-picker v-model="form.orderDate" type="date" value-format="YYYY-MM-DD" style="width: 100%" /></el-form-item></el-col>
          <el-col :span="12"><el-form-item label="交货期"><el-date-picker v-model="form.expectedDeliveryDate" type="date" value-format="YYYY-MM-DD" style="width: 100%" /></el-form-item></el-col>
          <el-col :span="12"><el-form-item label="交货条款"><el-input v-model="form.deliveryTerms" placeholder="例如：收到定金后 30 天交货" /></el-form-item></el-col>
          <el-col :span="12"><el-form-item label="账期条款"><el-input v-model="form.paymentTerms" placeholder="例如：30% 定金，70% 出货前付清" /></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="币种"><el-input v-model="form.currency" /></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="状态"><el-input v-model="form.status" /></el-form-item></el-col>
          <el-col :span="24"><el-form-item label="备注"><el-input v-model="form.remark" type="textarea" /></el-form-item></el-col>
        </el-row>
      </el-form>
      <DocumentLinesEditor v-if="form.id" document-type="CO" :document-id="form.id" />
      <template #footer>
        <el-button @click="dialogVisible=false">关闭</el-button>
        <el-button type="primary" @click="save">保存主单</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="poDialogVisible" title="生成采购订单 PO" width="560px">
      <el-alert title="从 CO 生成 PO，会复制该 CO 的全部商品明细。" type="info" show-icon style="margin-bottom: 12px" />
      <el-form label-width="110px">
        <el-form-item label="供应商"><el-select v-model="poForm.supplierId" filterable placeholder="选择供应商" style="width: 100%"><el-option v-for="s in suppliers" :key="s.id" :label="s.name" :value="s.id" /></el-select></el-form-item>
        <el-form-item label="预计交期"><el-date-picker v-model="poForm.expectedDeliveryDate" type="date" value-format="YYYY-MM-DD" style="width: 100%" /></el-form-item>
        <el-form-item label="交货条款"><el-input v-model="poForm.deliveryTerms" /></el-form-item>
        <el-form-item label="账期条款"><el-input v-model="poForm.paymentTerms" /></el-form-item>
        <el-form-item label="币种"><el-input v-model="poForm.currency" /></el-form-item>
      </el-form>
      <template #footer><el-button @click="poDialogVisible=false">取消</el-button><el-button type="primary" @click="generatePo">生成PO</el-button></template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { http } from '../api/http'
import DocumentLinesEditor from '../components/DocumentLinesEditor.vue'

const rows = ref<any[]>([])
const customers = ref<any[]>([])
const suppliers = ref<any[]>([])
const customerId = ref<number | null>(null)
const dialogVisible = ref(false)
const poDialogVisible = ref(false)
const selectedCoId = ref<number | null>(null)
const form = reactive<any>({ id: 0, no: '', customerId: null, rfqId: null, orderDate: '', expectedDeliveryDate: '', deliveryTerms: '', paymentTerms: '', currency: 'RMB', status: 'draft', remark: '' })
const poForm = reactive<any>({ supplierId: null, expectedDeliveryDate: '', deliveryTerms: '', paymentTerms: '', currency: 'RMB' })

async function loadCustomers() { const res = await http.get('/customers'); customers.value = res.data }
async function loadSuppliers() { const res = await http.get('/suppliers'); suppliers.value = res.data }
async function load() { const params: any = {}; if (customerId.value) params.customerId = customerId.value; const res = await http.get('/customer-orders', { params }); rows.value = res.data }
function reset() { Object.assign(form, { id: 0, no: '', customerId: null, rfqId: null, orderDate: '', expectedDeliveryDate: '', deliveryTerms: '', paymentTerms: '', currency: 'RMB', status: 'draft', remark: '' }) }
function openCreate() { reset(); dialogVisible.value = true }
function openDocument(row: any) { Object.assign(form, row); dialogVisible.value = true }
function openPoDialog(row: any) { selectedCoId.value = row.id; Object.assign(poForm, { supplierId: null, expectedDeliveryDate: row.expectedDeliveryDate || '', deliveryTerms: row.deliveryTerms || '', paymentTerms: row.paymentTerms || '', currency: row.currency || 'RMB' }); poDialogVisible.value = true }
async function save() {
  if (!form.customerId) return ElMessage.warning('请选择客户')
  const res = form.id ? await http.put(`/customer-orders/${form.id}`, form) : await http.post('/customer-orders', form)
  if (res.data) Object.assign(form, res.data)
  ElMessage.success('主单保存成功')
  await load()
}
async function copy(id: number) { await http.post(`/customer-orders/${id}/copy`); ElMessage.success('复制成功'); await load() }
async function generatePo() { if (!selectedCoId.value) return; if (!poForm.supplierId) return ElMessage.warning('请选择供应商'); const res = await http.post(`/customer-orders/${selectedCoId.value}/generate-po`, poForm); poDialogVisible.value = false; ElMessage.success(`已生成 PO：${res.data?.no || ''}`); await load() }
async function remove(id: number) { await ElMessageBox.confirm('确认删除该 CO？', '提示'); await http.delete(`/customer-orders/${id}`); ElMessage.success('已删除'); await load() }

onMounted(async () => { await loadCustomers(); await loadSuppliers(); await load() })
</script>

<style scoped>
.document-no { font-weight: 700; }
</style>