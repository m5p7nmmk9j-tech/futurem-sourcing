<template>
  <div class="page">
    <div class="page-header"><div class="page-title">Shipment 出运</div><el-button type="primary" @click="openCreate">新增出运单</el-button></div>
    <div class="card">
      <div class="toolbar">
        <el-select v-model="containerLoadId" placeholder="按装柜单筛选" clearable filterable style="width:300px" @change="load"><el-option v-for="c in containerLoads" :key="c.id" :label="c.no" :value="c.id" /></el-select>
        <el-select v-model="summaryOrderId" placeholder="按SO筛选" clearable filterable style="width:300px" @change="load"><el-option v-for="s in summaryOrders" :key="s.id" :label="s.no" :value="s.id" /></el-select>
        <el-button @click="load">刷新</el-button>
      </div>
      <el-table :data="rows" border stripe>
        <el-table-column prop="no" label="出运单号" width="190"/><el-table-column prop="shipmentMode" label="方式" width="100"/><el-table-column prop="containerLoadId" label="装柜ID" width="100"/><el-table-column prop="summaryOrderId" label="SO ID" width="100"/><el-table-column prop="carrier" label="承运人" width="150"/><el-table-column prop="vesselVoyage" label="船名航次" width="150"/><el-table-column prop="billOfLadingNo" label="提单号" width="160"/><el-table-column prop="departurePort" label="起运港" width="120"/><el-table-column prop="destinationPort" label="目的港" width="120"/><el-table-column prop="etd" label="ETD" width="130"/><el-table-column prop="eta" label="ETA" width="130"/><el-table-column prop="status" label="状态" width="110"/>
        <el-table-column label="操作" width="260" fixed="right"><template #default="scope"><el-button size="small" type="success" @click="selectRow(scope.row)">明细</el-button><el-button size="small" @click="openEdit(scope.row)">编辑</el-button><el-button size="small" type="danger" @click="remove(scope.row.id)">删除</el-button></template></el-table-column>
      </el-table>
      <DocumentLinesEditor v-if="selectedId" document-type="SHP" :document-id="selectedId" />
    </div>
    <el-dialog v-model="dialogVisible" :title="form.id ? '编辑出运单' : '新增出运单'" width="700px">
      <el-alert title="出运单记录物流信息，出运商品、箱数、CBM、重量在明细里填写。" type="info" show-icon style="margin-bottom:12px" />
      <el-form label-width="110px"><el-form-item label="装柜单"><el-select v-model="form.containerLoadId" clearable filterable style="width:100%"><el-option v-for="c in containerLoads" :key="c.id" :label="c.no" :value="c.id" /></el-select></el-form-item><el-form-item label="SO汇总单"><el-select v-model="form.summaryOrderId" clearable filterable style="width:100%"><el-option v-for="s in summaryOrders" :key="s.id" :label="s.no" :value="s.id" /></el-select></el-form-item><el-form-item label="运输方式"><el-select v-model="form.shipmentMode" style="width:100%"><el-option label="海运" value="SEA"/><el-option label="空运" value="AIR"/><el-option label="快递" value="EXPRESS"/><el-option label="直发" value="DIRECT"/></el-select></el-form-item><el-form-item label="承运人"><el-input v-model="form.carrier" /></el-form-item><el-form-item label="船名航次"><el-input v-model="form.vesselVoyage" /></el-form-item><el-form-item label="提单号"><el-input v-model="form.billOfLadingNo" /></el-form-item><el-form-item label="起运港"><el-input v-model="form.departurePort" /></el-form-item><el-form-item label="目的港"><el-input v-model="form.destinationPort" /></el-form-item><el-form-item label="ETD"><el-date-picker v-model="form.etd" type="date" value-format="YYYY-MM-DD" style="width:100%" /></el-form-item><el-form-item label="ETA"><el-date-picker v-model="form.eta" type="date" value-format="YYYY-MM-DD" style="width:100%" /></el-form-item><el-form-item label="状态"><el-input v-model="form.status" /></el-form-item><el-form-item label="备注"><el-input v-model="form.remark" type="textarea" /></el-form-item></el-form>
      <template #footer><el-button @click="dialogVisible=false">取消</el-button><el-button type="primary" @click="save">保存</el-button></template>
    </el-dialog>
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
async function save(){if(!form.shipmentMode)return ElMessage.warning('请选择运输方式'); const res=form.id?await http.put(`/shipments/${form.id}`,form):await http.post('/shipments',form); dialogVisible.value=false; ElMessage.success('保存成功'); await load(); selectedId.value=res.data?.id||form.id||selectedId.value}
async function remove(id:number){await ElMessageBox.confirm('确认删除该出运单？','提示'); await http.delete(`/shipments/${id}`); if(selectedId.value===id)selectedId.value=null; ElMessage.success('已删除'); await load()}
onMounted(async()=>{await loadContainerLoads();await loadSummaryOrders();await load()})
</script>
