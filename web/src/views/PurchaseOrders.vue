<template>
  <div class="page">
    <div class="page-header"><div class="page-title">PO 采购订单</div><el-button type="primary" @click="openCreate">新增 PO</el-button></div>
    <div class="card">
      <div class="toolbar">
        <el-select v-model="supplierId" placeholder="按供应商筛选" clearable filterable style="width:260px" @change="load"><el-option v-for="s in suppliers" :key="s.id" :label="s.name" :value="s.id" /></el-select>
        <el-select v-model="customerId" placeholder="按客户筛选" clearable filterable style="width:260px" @change="load"><el-option v-for="c in customers" :key="c.id" :label="c.name" :value="c.id" /></el-select>
        <el-button @click="load">刷新</el-button>
        <el-button type="primary" @click="openSoDialog">生成SO</el-button>
      </div>
      <el-table :data="rows" border stripe @selection-change="selectionChange">
        <el-table-column type="selection" width="50" />
        <el-table-column label="PO单号" width="190"><template #default="scope"><el-button link type="primary" class="document-no" @click="openDocument(scope.row)">{{ scope.row.no }}</el-button></template></el-table-column>
        <el-table-column prop="supplierId" label="供应商ID" width="100"/><el-table-column prop="customerId" label="客户ID" width="100"/><el-table-column prop="customerOrderId" label="来源CO ID" width="110"/><el-table-column prop="orderDate" label="下单日期" width="150"/><el-table-column prop="expectedDeliveryDate" label="交货期" width="150"/><el-table-column prop="deliveryTerms" label="交货条款" min-width="150"/><el-table-column prop="paymentTerms" label="账期条款" min-width="150"/><el-table-column prop="currency" label="币种" width="90"/><el-table-column prop="status" label="状态" width="110"/><el-table-column prop="payStatus" label="付款状态" width="110"/><el-table-column prop="remark" label="备注" min-width="220"/>
        <el-table-column label="操作" width="330" fixed="right"><template #default="scope"><el-button size="small" type="warning" @click="generatePayable(scope.row.id)">生成应付</el-button><el-button size="small" @click="openDocument(scope.row)">编辑</el-button><el-button size="small" @click="copy(scope.row.id)">复制</el-button><el-button size="small" type="danger" @click="remove(scope.row.id)">删除</el-button></template></el-table-column>
      </el-table>
    </div>

    <el-dialog v-model="dialogVisible" :title="form.id ? `编辑 PO：${form.no || ''}` : '新增 PO'" width="92%" destroy-on-close>
      <el-alert title="PO 是给供应商下单和付款的依据；采购价、包装、CBM、KG 都在明细里填写。保存主单后即可添加商品明细。" type="info" show-icon style="margin-bottom:12px" />
      <el-form label-width="110px">
        <el-row :gutter="16">
          <el-col :span="12"><el-form-item label="供应商"><div class="select-with-action"><el-select v-model="form.supplierId" filterable placeholder="选择供应商" style="width:100%"><el-option v-for="s in suppliers" :key="s.id" :label="s.name" :value="s.id" /></el-select><el-button @click="openSupplierDialog">新增</el-button></div></el-form-item></el-col>
          <el-col :span="12"><el-form-item label="客户"><div class="select-with-action"><el-select v-model="form.customerId" filterable clearable placeholder="选择客户" style="width:100%"><el-option v-for="c in customers" :key="c.id" :label="c.name" :value="c.id" /></el-select><el-button @click="openCustomerDialog">新增</el-button></div></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="来源CO ID"><el-input-number v-model="form.customerOrderId" :min="0" style="width:100%" /></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="下单日期"><el-date-picker v-model="form.orderDate" type="date" value-format="YYYY-MM-DD" style="width:100%" /></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="预计交货期"><el-date-picker v-model="form.expectedDeliveryDate" type="date" value-format="YYYY-MM-DD" style="width:100%" /></el-form-item></el-col>
          <el-col :span="12"><el-form-item label="交货条款"><el-input v-model="form.deliveryTerms" placeholder="例如：收到定金后 30 天交货" /></el-form-item></el-col>
          <el-col :span="12"><el-form-item label="账期条款"><el-input v-model="form.paymentTerms" placeholder="例如：30% 定金，70% 出货前付清" /></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="币种"><el-input v-model="form.currency" /></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="状态"><el-input v-model="form.status" /></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="付款状态"><el-input v-model="form.payStatus" /></el-form-item></el-col>
          <el-col :span="24"><el-form-item label="备注"><el-input v-model="form.remark" type="textarea" /></el-form-item></el-col>
        </el-row>
      </el-form>
      <DocumentLinesEditor v-if="form.id" document-type="PO" :document-id="form.id" />
      <template #footer><el-button @click="dialogVisible=false">关闭</el-button><el-button type="primary" @click="save">保存主单</el-button></template>
    </el-dialog>

    <el-dialog v-model="soDialogVisible" title="多个 PO 汇总生成 SO" width="560px">
      <el-alert :title="`已选择 ${selectedRows.length} 张 PO，生成 SO 后会复制全部 PO 明细。`" type="info" show-icon style="margin-bottom:12px" />
      <el-form label-width="100px"><el-form-item label="客户"><el-select v-model="soForm.customerId" filterable clearable style="width:100%"><el-option v-for="c in customers" :key="c.id" :label="c.name" :value="c.id" /></el-select></el-form-item><el-form-item label="币种"><el-input v-model="soForm.currency" /></el-form-item></el-form>
      <template #footer><el-button @click="soDialogVisible=false">取消</el-button><el-button type="primary" @click="generateSo">生成SO</el-button></template>
    </el-dialog>

    <el-dialog v-model="supplierDialogVisible" title="新增供应商" width="560px">
      <el-form label-width="100px"><el-form-item label="供应商名称"><el-input v-model="supplierForm.name" /></el-form-item><el-form-item label="店面号"><el-input v-model="supplierForm.shopNo" /></el-form-item><el-form-item label="主营产品"><el-input v-model="supplierForm.mainProducts" /></el-form-item><el-form-item label="联系人"><el-input v-model="supplierForm.contactName" /></el-form-item><el-form-item label="电话"><el-input v-model="supplierForm.phone" /></el-form-item><el-form-item label="WhatsApp"><el-input v-model="supplierForm.whatsapp" /></el-form-item><el-form-item label="备注"><el-input v-model="supplierForm.remark" type="textarea" /></el-form-item></el-form>
      <template #footer><el-button @click="supplierDialogVisible=false">取消</el-button><el-button type="primary" @click="saveSupplier">保存并选中</el-button></template>
    </el-dialog>

    <el-dialog v-model="customerDialogVisible" title="新增客户" width="560px">
      <el-form label-width="100px"><el-form-item label="客户名称"><el-input v-model="customerForm.name" /></el-form-item><el-form-item label="国家"><el-input v-model="customerForm.country" /></el-form-item><el-form-item label="目的港"><el-input v-model="customerForm.port" /></el-form-item><el-form-item label="联系人"><el-input v-model="customerForm.contactName" /></el-form-item><el-form-item label="电话"><el-input v-model="customerForm.phone" /></el-form-item><el-form-item label="WhatsApp"><el-input v-model="customerForm.whatsapp" /></el-form-item><el-form-item label="币种"><el-input v-model="customerForm.currency" /></el-form-item><el-form-item label="备注"><el-input v-model="customerForm.remark" type="textarea" /></el-form-item></el-form>
      <template #footer><el-button @click="customerDialogVisible=false">取消</el-button><el-button type="primary" @click="saveCustomer">保存并选中</el-button></template>
    </el-dialog>
  </div>
</template>
<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { http } from '../api/http'
import DocumentLinesEditor from '../components/DocumentLinesEditor.vue'
const rows=ref<any[]>([]), suppliers=ref<any[]>([]), customers=ref<any[]>([]), selectedRows=ref<any[]>([])
const supplierId=ref<number|null>(null), customerId=ref<number|null>(null), dialogVisible=ref(false), soDialogVisible=ref(false), supplierDialogVisible=ref(false), customerDialogVisible=ref(false)
const form=reactive<any>({id:0,no:'',supplierId:null,customerId:null,customerOrderId:null,orderDate:'',expectedDeliveryDate:'',deliveryTerms:'',paymentTerms:'',currency:'RMB',status:'draft',payStatus:'unpaid',remark:''})
const soForm=reactive<any>({customerId:null,currency:'RMB'})
const supplierForm=reactive<any>({name:'',shopNo:'',mainProducts:'',contactName:'',phone:'',whatsapp:'',remark:''})
const customerForm=reactive<any>({name:'',country:'',port:'',contactName:'',phone:'',whatsapp:'',currency:'RMB',remark:''})
async function loadSuppliers(){suppliers.value=(await http.get('/suppliers')).data} async function loadCustomers(){customers.value=(await http.get('/customers')).data}
async function load(){const params:any={}; if(supplierId.value)params.supplierId=supplierId.value; if(customerId.value)params.customerId=customerId.value; rows.value=(await http.get('/purchase-orders',{params})).data}
function reset(){Object.assign(form,{id:0,no:'',supplierId:null,customerId:null,customerOrderId:null,orderDate:'',expectedDeliveryDate:'',deliveryTerms:'',paymentTerms:'',currency:'RMB',status:'draft',payStatus:'unpaid',remark:''})}
function openCreate(){reset();dialogVisible.value=true} function openDocument(row:any){Object.assign(form,row);dialogVisible.value=true} function selectionChange(rows:any[]){selectedRows.value=rows}
function openSoDialog(){if(!selectedRows.value.length)return ElMessage.warning('请先勾选 PO'); const first=selectedRows.value.find(x=>x.customerId); Object.assign(soForm,{customerId:first?.customerId||null,currency:'RMB'}); soDialogVisible.value=true}
function openSupplierDialog(){Object.assign(supplierForm,{name:'',shopNo:'',mainProducts:'',contactName:'',phone:'',whatsapp:'',remark:''}); supplierDialogVisible.value=true}
function openCustomerDialog(){Object.assign(customerForm,{name:'',country:'',port:'',contactName:'',phone:'',whatsapp:'',currency:'RMB',remark:''}); customerDialogVisible.value=true}
async function save(){if(!form.supplierId)return ElMessage.warning('请选择供应商'); const res=form.id?await http.put(`/purchase-orders/${form.id}`,form):await http.post('/purchase-orders',form); if(res.data)Object.assign(form,res.data); ElMessage.success('主单保存成功'); await load()}
async function saveSupplier(){if(!supplierForm.name)return ElMessage.warning('请输入供应商名称'); const res=await http.post('/suppliers',supplierForm); await loadSuppliers(); form.supplierId=res.data.id; supplierDialogVisible.value=false; ElMessage.success('供应商已新增')}
async function saveCustomer(){if(!customerForm.name)return ElMessage.warning('请输入客户名称'); const res=await http.post('/customers',customerForm); await loadCustomers(); form.customerId=res.data.id; customerDialogVisible.value=false; ElMessage.success('客户已新增')}
async function copy(id:number){await http.post(`/purchase-orders/${id}/copy`); ElMessage.success('复制成功'); await load()}
async function generateSo(){if(!selectedRows.value.length)return; const res=await http.post('/summary-orders/generate-from-pos',{purchaseOrderIds:selectedRows.value.map(x=>x.id),customerId:soForm.customerId,currency:soForm.currency}); soDialogVisible.value=false; ElMessage.success(`已生成 SO：${res.data?.no||''}`); await load()}
async function generatePayable(id:number){const res=await http.post(`/purchase-orders/${id}/generate-payable`); ElMessage.success(`已生成应付：${res.data?.no||''}`); await load()}
async function remove(id:number){await ElMessageBox.confirm('确认删除该 PO？','提示'); await http.delete(`/purchase-orders/${id}`); ElMessage.success('已删除'); await load()}
onMounted(async()=>{await loadSuppliers();await loadCustomers();await load()})
</script>
<style scoped>
.select-with-action { display: flex; gap: 8px; width: 100%; }
.document-no { font-weight: 700; }
</style>