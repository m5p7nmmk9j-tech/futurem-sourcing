<template>
  <div class="page">
    <div class="page-header">
      <div>
        <div class="page-title">客户订单 CO</div>
        <div class="subtitle">订单商品、价格、包装、进口商、标签和唛头在确认时形成锁定快照。</div>
      </div>
      <el-button type="primary" @click="openCreate">新增客户订单</el-button>
    </div>

    <div class="card">
      <div class="toolbar">
        <el-select v-model="customerId" placeholder="按客户筛选" clearable filterable style="width:260px" @change="load">
          <el-option v-for="item in customers" :key="item.id" :label="item.name" :value="item.id" />
        </el-select>
        <el-button @click="load">刷新</el-button>
      </div>

      <el-table :data="rows" border stripe>
        <el-table-column label="CO 单号" width="190"><template #default="scope"><el-button link type="primary" class="document-no" @click="openDocument(scope.row)">{{ scope.row.no }}</el-button></template></el-table-column>
        <el-table-column label="客户" min-width="180"><template #default="scope">{{ customerName(scope.row.customerId) }}</template></el-table-column>
        <el-table-column prop="orderDate" label="订单日期" width="130" />
        <el-table-column prop="expectedDeliveryDate" label="交货期" width="130" />
        <el-table-column prop="quantity" label="商品数量" width="100" />
        <el-table-column prop="cartons" label="箱数" width="90" />
        <el-table-column prop="totalCbm" label="总 CBM" width="100" />
        <el-table-column label="结算" width="90"><template #default>人民币</template></el-table-column>
        <el-table-column label="状态" width="145"><template #default="scope"><el-tag :type="statusType(scope.row.status)">{{ customerOrderStatusLabel(scope.row.status) }}</el-tag></template></el-table-column>
        <el-table-column prop="remark" label="备注" min-width="180" show-overflow-tooltip />
        <el-table-column label="操作" width="350" fixed="right">
          <template #default="scope">
            <el-button size="small" @click="openDocument(scope.row)">{{ isCustomerOrderEditable(scope.row.status) ? '编辑' : '查看' }}</el-button>
            <el-button v-if="scope.row.status === 'draft'" size="small" type="success" @click="confirmOrder(scope.row)">确认</el-button>
            <el-button v-if="['confirmed','partially_converted'].includes(scope.row.status)" size="small" type="primary" @click="openPoDialog(scope.row)">生成 PO</el-button>
            <el-button size="small" @click="copy(scope.row.id)">复制</el-button>
            <el-button v-if="scope.row.status === 'draft'" size="small" type="danger" @click="remove(scope.row.id)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>
    </div>

    <el-dialog v-model="dialogVisible" :title="form.id ? `客户订单：${form.no || ''}` : '新增客户订单'" width="94%" destroy-on-close>
      <el-alert
        :title="editable ? '先保存订单头，再新增或复制订单商品；确认后全部业务资料锁定。' : '该订单已经确认，当前展示确认时锁定的业务资料。'"
        :type="editable ? 'info' : 'success'"
        show-icon
        :closable="false"
        style="margin-bottom:14px"
      />
      <el-form label-width="110px">
        <el-row :gutter="16">
          <el-col :span="8"><el-form-item label="客户"><el-select v-model="form.customerId" :disabled="!editable || !!(form.id && formHasProducts)" filterable placeholder="选择客户" style="width:100%" @change="customerChanged"><el-option v-for="item in customers" :key="item.id" :label="item.name" :value="item.id" /></el-select></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="进口商资料"><CustomerImporterSelector v-model="form.importerProfileId" :customer-id="form.customerId" :disabled="!editable" /></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="来源 RFQ ID"><el-input-number v-model="form.rfqId" :disabled="!editable" :min="0" style="width:100%" /></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="订单日期"><el-date-picker v-model="form.orderDate" :disabled="!editable" type="date" value-format="YYYY-MM-DD" style="width:100%" /></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="预计交货期"><el-date-picker v-model="form.expectedDeliveryDate" :disabled="!editable" type="date" value-format="YYYY-MM-DD" style="width:100%" /></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="状态"><el-input :model-value="customerOrderStatusLabel(form.status)" disabled /></el-form-item></el-col>
          <el-col :span="12"><el-form-item label="商品标签 / 唛头"><div style="width:100%"><LabelMarkTemplateSelector v-model:label-template-id="form.labelTemplateId" v-model:mark-template-id="form.markTemplateId" :customer-id="form.customerId" :disabled="!editable" /></div></el-form-item></el-col>
          <el-col :span="6"><el-form-item label="交货条款"><el-input v-model="form.deliveryTerms" :disabled="!editable" /></el-form-item></el-col>
          <el-col :span="6"><el-form-item label="账期条款"><el-input v-model="form.paymentTerms" :disabled="!editable" /></el-form-item></el-col>
          <el-col :span="24"><el-form-item label="备注"><el-input v-model="form.remark" :disabled="!editable" type="textarea" /></el-form-item></el-col>
        </el-row>
      </el-form>

      <OrderProductEditor
        v-if="form.id"
        :customer-order-id="form.id"
        :customer-id="form.customerId"
        :locked="!editable"
        @changed="productChanged"
      />

      <template #footer>
        <el-button @click="dialogVisible=false">关闭</el-button>
        <el-button v-if="!editable && ['confirmed'].includes(form.status)" @click="reopenOrder">退回草稿</el-button>
        <el-button v-if="editable" type="primary" @click="save">保存订单头</el-button>
        <el-button v-if="form.id && editable" type="success" @click="confirmCurrent">确认订单</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="poDialogVisible" title="选择完整订单商品生成采购订单" width="900px" destroy-on-close>
      <el-alert title="同一商品必须以本订单全部数量进入一张有效 PO；一张 PO 只能属于一个商品供应商。" type="warning" show-icon :closable="false" style="margin-bottom:14px" />
      <el-form label-width="100px">
        <el-row :gutter="14">
          <el-col :span="12"><el-form-item label="商品供应商"><el-select v-model="poForm.supplierId" filterable style="width:100%" @change="clearPoSelection"><el-option v-for="item in suppliers" :key="item.id" :label="item.name" :value="item.id" /></el-select></el-form-item></el-col>
          <el-col :span="12"><el-form-item label="预计交期"><el-date-picker v-model="poForm.expectedDeliveryDate" type="date" value-format="YYYY-MM-DD" style="width:100%" /></el-form-item></el-col>
        </el-row>
      </el-form>
      <el-table ref="poTable" :data="poAvailableRows" border stripe @selection-change="handlePoSelection">
        <el-table-column type="selection" width="50" />
        <el-table-column prop="product.customerItemNo" label="客户货号" width="140" />
        <el-table-column prop="product.customerBarcode" label="客户条码" width="150" />
        <el-table-column prop="product.nameCn" label="商品名称" min-width="180" />
        <el-table-column prop="line.quantity" label="全部数量" width="100" />
        <el-table-column prop="line.cartons" label="箱数" width="90" />
        <el-table-column label="采购价" width="110"><template #default="scope">{{ formatRmb(scope.row.product.purchaseUnitPrice) }}</template></el-table-column>
      </el-table>
      <template #footer><el-button @click="poDialogVisible=false">取消</el-button><el-button type="primary" @click="generatePo">生成采购订单</el-button></template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { http } from '../api/http'
import CustomerImporterSelector from '../components/CustomerImporterSelector.vue'
import LabelMarkTemplateSelector from '../components/LabelMarkTemplateSelector.vue'
import OrderProductEditor from '../components/OrderProductEditor.vue'
import { customerOrderStatusLabel, isCustomerOrderEditable } from '../utils/orderProduct'
import { formatRmb } from '../utils/rmb'

const rows = ref<any[]>([])
const customers = ref<any[]>([])
const suppliers = ref<any[]>([])
const customerId = ref<number | null>(null)
const dialogVisible = ref(false)
const poDialogVisible = ref(false)
const formHasProducts = ref(false)
const poRows = ref<any[]>([])
const poSelectedRows = ref<any[]>([])
const poTable = ref<any>()

const emptyForm = () => ({ id: 0, no: '', customerId: null as number | null, rfqId: null, orderDate: new Date().toISOString().slice(0,10), expectedDeliveryDate: '', deliveryTerms: '', paymentTerms: '', importerProfileId: null as number | null, labelTemplateId: null as number | null, markTemplateId: null as number | null, status: 'draft', confirmedAt: null, remark: '' })
const form = reactive<any>(emptyForm())
const poForm = reactive({ orderId: 0, supplierId: null as number | null, expectedDeliveryDate: '' })
const editable = computed(() => isCustomerOrderEditable(form.status))
const poAvailableRows = computed(() => poRows.value.filter(row => !poForm.supplierId || row.product.supplierId === poForm.supplierId))

async function load() {
  const response = await http.get('/customer-orders', { params: { customerId: customerId.value || undefined } })
  rows.value = response.data || []
}
function customerName(id: number) { return customers.value.find(item => item.id === id)?.name || `客户 ${id}` }
function statusType(status: string) { return status === 'draft' ? 'info' : status === 'confirmed' ? 'success' : status === 'cancelled' ? 'danger' : 'warning' }
function reset() { Object.assign(form, emptyForm()); formHasProducts.value = false }
function openCreate() { reset(); dialogVisible.value = true }
async function openDocument(row: any) {
  const detail = await http.get(`/customer-orders/${row.id}`)
  Object.assign(form, emptyForm(), detail.data)
  const products = await http.get(`/customer-orders/${row.id}/products`)
  formHasProducts.value = (products.data || []).length > 0
  dialogVisible.value = true
}
function customerChanged() {
  form.importerProfileId = null
  form.labelTemplateId = null
  form.markTemplateId = null
}
async function save() {
  if (!form.customerId) return ElMessage.warning('请选择客户')
  if (!form.importerProfileId) return ElMessage.warning('请选择进口商资料')
  if (!form.labelTemplateId || !form.markTemplateId) return ElMessage.warning('请选择商品标签和外箱唛头模板')
  const response = form.id ? await http.put(`/customer-orders/${form.id}`, form) : await http.post('/customer-orders', form)
  Object.assign(form, response.data)
  ElMessage.success('订单头保存成功')
  await load()
}
async function productChanged() { formHasProducts.value = true; await load() }
async function confirmOrder(row: any) {
  await ElMessageBox.confirm(`确认客户订单 ${row.no}？确认后商品资料将锁定。`, '确认订单')
  await http.post(`/customer-orders/${row.id}/confirm`, { reason: '客户订单确认' })
  ElMessage.success('客户订单已确认')
  await load()
}
async function confirmCurrent() {
  if (!form.id) return ElMessage.warning('请先保存订单头')
  await confirmOrder(form)
  const response = await http.get(`/customer-orders/${form.id}`)
  Object.assign(form, response.data)
}
async function reopenOrder() {
  const result = await ElMessageBox.prompt('填写退回草稿原因', '退回草稿', { inputValidator: value => !!value?.trim() || '原因不能为空' })
  await http.post(`/customer-orders/${form.id}/reopen`, { reason: result.value })
  ElMessage.success('已退回草稿')
  const response = await http.get(`/customer-orders/${form.id}`)
  Object.assign(form, response.data)
  await load()
}
async function copy(id: number) { await http.post(`/customer-orders/${id}/copy`); ElMessage.success('已复制为新的草稿订单'); await load() }
async function remove(id: number) { await ElMessageBox.confirm('确认删除该草稿订单？', '提示'); await http.delete(`/customer-orders/${id}`); ElMessage.success('已删除'); await load() }
async function openPoDialog(row: any) {
  poForm.orderId = row.id
  poForm.supplierId = null
  poForm.expectedDeliveryDate = row.expectedDeliveryDate || ''
  poSelectedRows.value = []
  poRows.value = (await http.get(`/customer-orders/${row.id}/products`)).data || []
  poDialogVisible.value = true
}
function clearPoSelection() { poSelectedRows.value = []; poTable.value?.clearSelection() }
function handlePoSelection(selection: any[]) { poSelectedRows.value = selection }
async function generatePo() {
  if (!poForm.supplierId) return ElMessage.warning('请选择商品供应商')
  if (!poSelectedRows.value.length) return ElMessage.warning('请选择至少一个订单商品')
  const response = await http.post(`/customer-orders/${poForm.orderId}/generate-pos`, {
    supplierId: poForm.supplierId,
    expectedDeliveryDate: poForm.expectedDeliveryDate || null,
    items: poSelectedRows.value.map(row => ({ orderProductId: row.product.id, quantity: row.line.quantity })),
  })
  poDialogVisible.value = false
  ElMessage.success(`采购订单 ${response.data?.no || ''} 已生成`)
  await load()
}

onMounted(async () => {
  const [customerResponse, supplierResponse] = await Promise.all([http.get('/customers'), http.get('/suppliers')])
  customers.value = customerResponse.data || []
  suppliers.value = supplierResponse.data || []
  await load()
})
</script>

<style scoped>
.subtitle { color:#64748b; margin-top:4px; font-size:13px; }
.toolbar { display:flex; gap:10px; margin-bottom:14px; }
.document-no { font-weight:700; }
</style>
