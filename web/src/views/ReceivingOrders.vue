<template>
  <div class="page">
    <div class="page-header"><div class="page-title">Receiving</div><el-button type="primary" @click="openCreate">New</el-button></div>
    <div class="card">
      <div class="toolbar"><el-select v-model="purchaseOrderId" placeholder="PO" clearable filterable style="width:320px" @change="load"><el-option v-for="p in purchaseOrders" :key="p.id" :label="p.no" :value="p.id" /></el-select><el-button @click="load">Refresh</el-button></div>
      <el-table :data="rows" border stripe>
        <el-table-column label="No" width="190"><template #default="scope"><el-button link type="primary" class="document-no" @click="openDocument(scope.row)">{{ scope.row.no }}</el-button></template></el-table-column>
        <el-table-column prop="purchaseOrderId" label="PO ID" width="100"/><el-table-column prop="receiveDate" label="Date" width="150"/><el-table-column prop="warehouseLocation" label="Location" width="180"/><el-table-column prop="status" label="Status" width="110"/><el-table-column prop="remark" label="Remark" min-width="220"/>
        <el-table-column label="Action" width="250" fixed="right"><template #default="scope"><el-button size="small" @click="openDocument(scope.row)">Edit</el-button><el-button size="small" @click="copy(scope.row.id)">Copy</el-button><el-button size="small" type="danger" @click="remove(scope.row.id)">Delete</el-button></template></el-table-column>
      </el-table>
    </div>
    <el-dialog v-model="dialogVisible" :title="form.id ? `Edit Receiving: ${form.no || ''}` : 'New Receiving'" width="92%" destroy-on-close>
      <el-alert title="Save the document header first, then add or edit item lines below." type="info" show-icon style="margin-bottom:12px" />
      <el-form label-width="110px">
        <el-row :gutter="16">
          <el-col :span="12"><el-form-item label="PO"><el-select v-model="form.purchaseOrderId" filterable placeholder="PO" style="width:100%"><el-option v-for="p in purchaseOrders" :key="p.id" :label="p.no" :value="p.id" /></el-select></el-form-item></el-col>
          <el-col :span="12"><el-form-item label="Date"><el-date-picker v-model="form.receiveDate" type="date" value-format="YYYY-MM-DD" style="width:100%" /></el-form-item></el-col>
          <el-col :span="12"><el-form-item label="Location"><el-input v-model="form.warehouseLocation" /></el-form-item></el-col>
          <el-col :span="12"><el-form-item label="Status"><el-input v-model="form.status" /></el-form-item></el-col>
          <el-col :span="24"><el-form-item label="Remark"><el-input v-model="form.remark" type="textarea" /></el-form-item></el-col>
        </el-row>
      </el-form>
      <DocumentLinesEditor v-if="form.id" document-type="RCV" :document-id="form.id" />
      <template #footer><el-button @click="dialogVisible=false">Close</el-button><el-button type="primary" @click="save">Save Header</el-button></template>
    </el-dialog>
  </div>
</template>
<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { http } from '../api/http'
import DocumentLinesEditor from '../components/DocumentLinesEditor.vue'
const rows=ref<any[]>([]), purchaseOrders=ref<any[]>([]), purchaseOrderId=ref<number|null>(null), dialogVisible=ref(false)
const form=reactive<any>({id:0,no:'',purchaseOrderId:null,receiveDate:'',warehouseLocation:'',status:'draft',remark:''})
async function loadPurchaseOrders(){purchaseOrders.value=(await http.get('/purchase-orders')).data}
async function load(){const params:any={}; if(purchaseOrderId.value)params.purchaseOrderId=purchaseOrderId.value; rows.value=(await http.get('/receiving-orders',{params})).data}
function reset(){Object.assign(form,{id:0,no:'',purchaseOrderId:null,receiveDate:'',warehouseLocation:'',status:'draft',remark:''})}
function openCreate(){reset();dialogVisible.value=true} function openDocument(row:any){Object.assign(form,row);dialogVisible.value=true}
async function save(){if(!form.purchaseOrderId)return ElMessage.warning('PO required'); const res=form.id?await http.put(`/receiving-orders/${form.id}`,form):await http.post('/receiving-orders',form); if(res.data)Object.assign(form,res.data); ElMessage.success('Header saved'); await load()}
async function copy(id:number){await http.post(`/receiving-orders/${id}/copy`); ElMessage.success('Copied'); await load()}
async function remove(id:number){await ElMessageBox.confirm('Delete?','Tip'); await http.delete(`/receiving-orders/${id}`); ElMessage.success('Deleted'); await load()}
onMounted(async()=>{await loadPurchaseOrders();await load()})
</script>
<style scoped>
.document-no { font-weight: 700; }
</style>