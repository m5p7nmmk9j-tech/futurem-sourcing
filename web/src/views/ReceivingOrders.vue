<template>
  <div class="page">
    <div class="page-header"><div class="page-title">Receiving</div><el-button type="primary" @click="openCreate">New</el-button></div>
    <div class="card">
      <div class="toolbar"><el-select v-model="purchaseOrderId" placeholder="PO" clearable filterable style="width:320px" @change="load"><el-option v-for="p in purchaseOrders" :key="p.id" :label="p.no" :value="p.id" /></el-select><el-button @click="load">Refresh</el-button></div>
      <el-table :data="rows" border stripe>
        <el-table-column prop="no" label="No" width="190"/><el-table-column prop="purchaseOrderId" label="PO ID" width="100"/><el-table-column prop="receiveDate" label="Date" width="150"/><el-table-column prop="warehouseLocation" label="Location" width="180"/><el-table-column prop="status" label="Status" width="110"/><el-table-column prop="remark" label="Remark" min-width="220"/>
        <el-table-column label="Action" width="320" fixed="right"><template #default="scope"><el-button size="small" type="success" @click="selectRow(scope.row)">Lines</el-button><el-button size="small" @click="openEdit(scope.row)">Edit</el-button><el-button size="small" @click="copy(scope.row.id)">Copy</el-button><el-button size="small" type="danger" @click="remove(scope.row.id)">Delete</el-button></template></el-table-column>
      </el-table>
      <DocumentLinesEditor v-if="selectedId" document-type="RCV" :document-id="selectedId" />
    </div>
    <el-dialog v-model="dialogVisible" :title="form.id ? 'Edit Receiving' : 'New Receiving'" width="600px">
      <el-form label-width="110px"><el-form-item label="PO"><el-select v-model="form.purchaseOrderId" filterable placeholder="PO" style="width:100%"><el-option v-for="p in purchaseOrders" :key="p.id" :label="p.no" :value="p.id" /></el-select></el-form-item><el-form-item label="Date"><el-date-picker v-model="form.receiveDate" type="date" value-format="YYYY-MM-DD" style="width:100%" /></el-form-item><el-form-item label="Location"><el-input v-model="form.warehouseLocation" /></el-form-item><el-form-item label="Status"><el-input v-model="form.status" /></el-form-item><el-form-item label="Remark"><el-input v-model="form.remark" type="textarea" /></el-form-item></el-form>
      <template #footer><el-button @click="dialogVisible=false">Cancel</el-button><el-button type="primary" @click="save">Save</el-button></template>
    </el-dialog>
  </div>
</template>
<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { http } from '../api/http'
import DocumentLinesEditor from '../components/DocumentLinesEditor.vue'
const rows=ref<any[]>([]), purchaseOrders=ref<any[]>([]), purchaseOrderId=ref<number|null>(null), dialogVisible=ref(false), selectedId=ref<number|null>(null)
const form=reactive<any>({id:0,purchaseOrderId:null,receiveDate:'',warehouseLocation:'',status:'draft',remark:''})
async function loadPurchaseOrders(){purchaseOrders.value=(await http.get('/purchase-orders')).data}
async function load(){const params:any={}; if(purchaseOrderId.value)params.purchaseOrderId=purchaseOrderId.value; rows.value=(await http.get('/receiving-orders',{params})).data; if(!selectedId.value&&rows.value.length)selectedId.value=rows.value[0].id}
function reset(){Object.assign(form,{id:0,purchaseOrderId:null,receiveDate:'',warehouseLocation:'',status:'draft',remark:''})}
function openCreate(){reset();dialogVisible.value=true} function openEdit(row:any){Object.assign(form,row);dialogVisible.value=true} function selectRow(row:any){selectedId.value=row.id}
async function save(){if(!form.purchaseOrderId)return ElMessage.warning('PO required'); const res=form.id?await http.put(`/receiving-orders/${form.id}`,form):await http.post('/receiving-orders',form); dialogVisible.value=false; ElMessage.success('Saved'); await load(); selectedId.value=res.data?.id||form.id||selectedId.value}
async function copy(id:number){const res=await http.post(`/receiving-orders/${id}/copy`); ElMessage.success('Copied'); await load(); selectedId.value=res.data?.id||selectedId.value}
async function remove(id:number){await ElMessageBox.confirm('Delete?','Tip'); await http.delete(`/receiving-orders/${id}`); if(selectedId.value===id)selectedId.value=null; ElMessage.success('Deleted'); await load()}
onMounted(async()=>{await loadPurchaseOrders();await load()})
</script>
