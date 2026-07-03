<template>
  <div class="page">
    <div class="page-header"><div class="page-title">Container</div><el-button type="primary" @click="openCreate">New</el-button></div>
    <div class="card">
      <div class="toolbar"><el-select v-model="summaryOrderId" placeholder="SO" clearable filterable style="width:300px" @change="load"><el-option v-for="s in summaryOrders" :key="s.id" :label="s.no" :value="s.id" /></el-select><el-button @click="load">Refresh</el-button></div>
      <el-table :data="rows" border stripe>
        <el-table-column prop="no" label="No" width="190"/><el-table-column prop="summaryOrderId" label="SO ID" width="100"/><el-table-column prop="containerType" label="Type" width="120"/><el-table-column prop="containerNo" label="Container No" width="160"/><el-table-column prop="sealNo" label="Seal No" width="160"/><el-table-column prop="loadDate" label="Date" width="150"/><el-table-column prop="totalCartons" label="CTN" width="100"/><el-table-column prop="totalCbm" label="CBM" width="100"/><el-table-column prop="totalGwKg" label="KG" width="100"/><el-table-column prop="status" label="Status" width="110"/>
        <el-table-column label="Action" width="320" fixed="right"><template #default="scope"><el-button size="small" type="success" @click="selectRow(scope.row)">Lines</el-button><el-button size="small" @click="openEdit(scope.row)">Edit</el-button><el-button size="small" @click="copy(scope.row.id)">Copy</el-button><el-button size="small" type="danger" @click="remove(scope.row.id)">Delete</el-button></template></el-table-column>
      </el-table>
      <DocumentLinesEditor v-if="selectedId" document-type="CL" :document-id="selectedId" />
    </div>
    <el-dialog v-model="dialogVisible" :title="form.id ? 'Edit Container' : 'New Container'" width="660px"><el-form label-width="110px"><el-form-item label="SO"><el-select v-model="form.summaryOrderId" filterable clearable style="width:100%"><el-option v-for="s in summaryOrders" :key="s.id" :label="s.no" :value="s.id" /></el-select></el-form-item><el-form-item label="Type"><el-select v-model="form.containerType" style="width:100%"><el-option label="20GP" value="20GP"/><el-option label="40GP" value="40GP"/><el-option label="40HQ" value="40HQ"/><el-option label="45HQ" value="45HQ"/></el-select></el-form-item><el-form-item label="Container No"><el-input v-model="form.containerNo" /></el-form-item><el-form-item label="Seal No"><el-input v-model="form.sealNo" /></el-form-item><el-form-item label="Date"><el-date-picker v-model="form.loadDate" type="date" value-format="YYYY-MM-DD" style="width:100%" /></el-form-item><el-form-item label="CTN"><el-input-number v-model="form.totalCartons" :min="0" style="width:100%" /></el-form-item><el-form-item label="CBM"><el-input-number v-model="form.totalCbm" :min="0" :precision="3" style="width:100%" /></el-form-item><el-form-item label="KG"><el-input-number v-model="form.totalGwKg" :min="0" :precision="2" style="width:100%" /></el-form-item><el-form-item label="Status"><el-input v-model="form.status" /></el-form-item><el-form-item label="Remark"><el-input v-model="form.remark" type="textarea" /></el-form-item></el-form><template #footer><el-button @click="dialogVisible=false">Cancel</el-button><el-button type="primary" @click="save">Save</el-button></template></el-dialog>
  </div>
</template>
<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { http } from '../api/http'
import DocumentLinesEditor from '../components/DocumentLinesEditor.vue'
const rows=ref<any[]>([]), summaryOrders=ref<any[]>([]), summaryOrderId=ref<number|null>(null), dialogVisible=ref(false), selectedId=ref<number|null>(null)
const form=reactive<any>({id:0,summaryOrderId:null,containerType:'40HQ',containerNo:'',sealNo:'',loadDate:'',status:'draft',totalCartons:0,totalCbm:0,totalGwKg:0,remark:''})
async function loadSummaryOrders(){summaryOrders.value=(await http.get('/summary-orders')).data}
async function load(){const params:any={}; if(summaryOrderId.value)params.summaryOrderId=summaryOrderId.value; rows.value=(await http.get('/container-loads',{params})).data; if(!selectedId.value&&rows.value.length)selectedId.value=rows.value[0].id}
function reset(){Object.assign(form,{id:0,summaryOrderId:null,containerType:'40HQ',containerNo:'',sealNo:'',loadDate:'',status:'draft',totalCartons:0,totalCbm:0,totalGwKg:0,remark:''})}
function openCreate(){reset();dialogVisible.value=true} function openEdit(row:any){Object.assign(form,row);dialogVisible.value=true} function selectRow(row:any){selectedId.value=row.id}
async function save(){if(!form.containerType)return ElMessage.warning('Type required'); const res=form.id?await http.put(`/container-loads/${form.id}`,form):await http.post('/container-loads',form); dialogVisible.value=false; ElMessage.success('Saved'); await load(); selectedId.value=res.data?.id||form.id||selectedId.value}
async function copy(id:number){const res=await http.post(`/container-loads/${id}/copy`); ElMessage.success('Copied'); await load(); selectedId.value=res.data?.id||selectedId.value}
async function remove(id:number){await ElMessageBox.confirm('Delete?','Tip'); await http.delete(`/container-loads/${id}`); if(selectedId.value===id)selectedId.value=null; ElMessage.success('Deleted'); await load()}
onMounted(async()=>{await loadSummaryOrders();await load()})
</script>
