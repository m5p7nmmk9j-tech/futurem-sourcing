<template>
  <div class="page">
    <div class="page-header"><div class="page-title">Shipment</div><el-button type="primary" @click="openCreate">New</el-button></div>
    <div class="card">
      <div class="toolbar"><el-select v-model="containerLoadId" placeholder="CL" clearable filterable style="width:300px" @change="load"><el-option v-for="c in containerLoads" :key="c.id" :label="c.no" :value="c.id" /></el-select><el-select v-model="summaryOrderId" placeholder="SO" clearable filterable style="width:300px" @change="load"><el-option v-for="s in summaryOrders" :key="s.id" :label="s.no" :value="s.id" /></el-select><el-button @click="load">Refresh</el-button></div>
      <el-table :data="rows" border stripe>
        <el-table-column prop="no" label="No" width="190"/><el-table-column prop="shipmentMode" label="Mode" width="100"/><el-table-column prop="containerLoadId" label="CL ID" width="100"/><el-table-column prop="summaryOrderId" label="SO ID" width="100"/><el-table-column prop="carrier" label="Carrier" width="150"/><el-table-column prop="vesselVoyage" label="Vessel" width="150"/><el-table-column prop="billOfLadingNo" label="BL" width="160"/><el-table-column prop="etd" label="ETD" width="130"/><el-table-column prop="eta" label="ETA" width="130"/><el-table-column prop="status" label="Status" width="110"/>
        <el-table-column label="Action" width="320" fixed="right"><template #default="scope"><el-button size="small" type="success" @click="selectRow(scope.row)">Lines</el-button><el-button size="small" @click="openEdit(scope.row)">Edit</el-button><el-button size="small" @click="copy(scope.row.id)">Copy</el-button><el-button size="small" type="danger" @click="remove(scope.row.id)">Delete</el-button></template></el-table-column>
      </el-table>
      <DocumentLinesEditor v-if="selectedId" document-type="SHP" :document-id="selectedId" />
    </div>
    <el-dialog v-model="dialogVisible" :title="form.id ? 'Edit' : 'New'" width="700px"><el-form label-width="110px"><el-form-item label="CL"><el-select v-model="form.containerLoadId" clearable filterable style="width:100%"><el-option v-for="c in containerLoads" :key="c.id" :label="c.no" :value="c.id" /></el-select></el-form-item><el-form-item label="SO"><el-select v-model="form.summaryOrderId" clearable filterable style="width:100%"><el-option v-for="s in summaryOrders" :key="s.id" :label="s.no" :value="s.id" /></el-select></el-form-item><el-form-item label="Mode"><el-select v-model="form.shipmentMode" style="width:100%"><el-option label="SEA" value="SEA"/><el-option label="AIR" value="AIR"/><el-option label="EXPRESS" value="EXPRESS"/></el-select></el-form-item><el-form-item label="Carrier"><el-input v-model="form.carrier" /></el-form-item><el-form-item label="Vessel"><el-input v-model="form.vesselVoyage" /></el-form-item><el-form-item label="BL"><el-input v-model="form.billOfLadingNo" /></el-form-item><el-form-item label="ETD"><el-date-picker v-model="form.etd" type="date" value-format="YYYY-MM-DD" style="width:100%" /></el-form-item><el-form-item label="ETA"><el-date-picker v-model="form.eta" type="date" value-format="YYYY-MM-DD" style="width:100%" /></el-form-item><el-form-item label="Status"><el-input v-model="form.status" /></el-form-item><el-form-item label="Remark"><el-input v-model="form.remark" type="textarea" /></el-form-item></el-form><template #footer><el-button @click="dialogVisible=false">Cancel</el-button><el-button type="primary" @click="save">Save</el-button></template></el-dialog>
  </div>
</template>
<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { http } from '../api/http'
import DocumentLinesEditor from '../components/DocumentLinesEditor.vue'
const rows=ref<any[]>([]), containerLoads=ref<any[]>([]), summaryOrders=ref<any[]>([])
const containerLoadId=ref<number|null>(null), summaryOrderId=ref<number|null>(null), dialogVisible=ref(false), selectedId=ref<number|null>(null)
const form=reactive<any>({id:0,containerLoadId:null,summaryOrderId:null,shipmentMode:'SEA',carrier:'',vesselVoyage:'',billOfLadingNo:'',departurePort:'',destinationPort:'',etd:'',eta:'',status:'draft',remark:''})
async function loadContainerLoads(){containerLoads.value=(await http.get('/container-loads')).data} async function loadSummaryOrders(){summaryOrders.value=(await http.get('/summary-orders')).data}
async function load(){const params:any={}; if(containerLoadId.value)params.containerLoadId=containerLoadId.value; if(summaryOrderId.value)params.summaryOrderId=summaryOrderId.value; rows.value=(await http.get('/shipments',{params})).data; if(!selectedId.value&&rows.value.length)selectedId.value=rows.value[0].id}
function reset(){Object.assign(form,{id:0,containerLoadId:null,summaryOrderId:null,shipmentMode:'SEA',carrier:'',vesselVoyage:'',billOfLadingNo:'',departurePort:'',destinationPort:'',etd:'',eta:'',status:'draft',remark:''})}
function openCreate(){reset();dialogVisible.value=true} function openEdit(row:any){Object.assign(form,row);dialogVisible.value=true} function selectRow(row:any){selectedId.value=row.id}
async function save(){if(!form.shipmentMode)return ElMessage.warning('Mode required'); const res=form.id?await http.put(`/shipments/${form.id}`,form):await http.post('/shipments',form); dialogVisible.value=false; ElMessage.success('Saved'); await load(); selectedId.value=res.data?.id||form.id||selectedId.value}
async function copy(id:number){const res=await http.post(`/shipments/${id}/copy`); ElMessage.success('Copied'); await load(); selectedId.value=res.data?.id||selectedId.value}
async function remove(id:number){await ElMessageBox.confirm('Delete?','Tip'); await http.delete(`/shipments/${id}`); if(selectedId.value===id)selectedId.value=null; ElMessage.success('Deleted'); await load()}
onMounted(async()=>{await loadContainerLoads();await loadSummaryOrders();await load()})
</script>
