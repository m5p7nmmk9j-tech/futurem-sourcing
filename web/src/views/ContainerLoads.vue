<template>
  <div class="page">
    <div class="page-header"><div class="page-title">Container 装柜</div><el-button type="primary" @click="openCreate">新增装柜单</el-button></div>
    <div class="card">
      <div class="toolbar"><el-select v-model="summaryOrderId" placeholder="按SO筛选" clearable filterable style="width:300px" @change="load"><el-option v-for="s in summaryOrders" :key="s.id" :label="s.no" :value="s.id" /></el-select><el-button @click="load">刷新</el-button><el-button type="primary" @click="recommendBySo">按SO推荐柜型</el-button></div>
      <el-table :data="rows" border stripe>
        <el-table-column label="装柜单号" width="190"><template #default="scope"><el-button link type="primary" class="document-no" @click="openDocument(scope.row)">{{ scope.row.no }}</el-button></template></el-table-column>
        <el-table-column prop="summaryOrderId" label="SO ID" width="100"/><el-table-column prop="containerType" label="柜型" width="120"/><el-table-column prop="containerNo" label="柜号" width="160"/><el-table-column prop="sealNo" label="封条号" width="160"/><el-table-column prop="loadDate" label="装柜日期" width="150"/><el-table-column prop="totalCartons" label="箱数" width="100"/><el-table-column prop="totalCbm" label="CBM" width="100"/><el-table-column prop="totalGwKg" label="KG" width="100"/><el-table-column prop="status" label="状态" width="110"/>
        <el-table-column label="操作" width="500" fixed="right"><template #default="scope"><el-button size="small" type="warning" @click="loadUtilization(scope.row)">容量预警</el-button><el-button size="small" type="primary" @click="recommendByContainer(scope.row)">推荐柜型</el-button><el-button size="small" type="primary" @click="openShipmentDialog(scope.row)">生成出运</el-button><el-button size="small" @click="openDocument(scope.row)">编辑</el-button><el-button size="small" @click="copy(scope.row.id)">复制</el-button><el-button size="small" type="danger" @click="remove(scope.row.id)">删除</el-button></template></el-table-column>
      </el-table>
      <el-alert v-if="recommendation.message" :type="recommendation.needSplit?'warning':'success'" :title="recommendation.message" show-icon style="margin-top:12px" />
      <el-table v-if="recommendation.options?.length" :data="recommendation.options" border stripe size="small" style="margin-top:12px">
        <el-table-column prop="containerType" label="柜型" width="100"/><el-table-column prop="capacityCbm" label="容量CBM" width="120"/><el-table-column prop="capacityKg" label="容量KG" width="120"/><el-table-column prop="cbmRate" label="CBM利用率%" width="130"/><el-table-column prop="weightRate" label="重量利用率%" width="130"/><el-table-column prop="remainingCbm" label="剩余CBM" width="120"/><el-table-column prop="remainingKg" label="剩余KG" width="120"/><el-table-column label="是否可装" width="100"><template #default="scope">{{ scope.row.ok ? '可以' : '超柜' }}</template></el-table-column>
      </el-table>
      <el-alert v-if="utilization.message" :type="utilization.level==='danger'?'error':utilization.level==='warning'?'warning':'success'" :title="utilization.message" show-icon style="margin-top:12px" />
      <el-row v-if="utilization.message" :gutter="12" style="margin-top:12px">
        <el-col :span="3"><el-statistic title="柜型" :value="utilization.containerType || '-'" /></el-col><el-col :span="3"><el-statistic title="容量CBM" :value="utilization.capacityCbm || 0" /></el-col><el-col :span="3"><el-statistic title="已装CBM" :value="utilization.cbm || 0" /></el-col><el-col :span="3"><el-statistic title="剩余CBM" :value="utilization.remainingCbm || 0" /></el-col><el-col :span="3"><el-statistic title="CBM利用率%" :value="utilization.cbmRate || 0" /></el-col><el-col :span="3"><el-statistic title="重量利用率%" :value="utilization.weightRate || 0" /></el-col><el-col :span="3"><el-statistic title="箱数" :value="utilization.cartons || 0" /></el-col><el-col :span="3"><el-statistic title="总KG" :value="utilization.gw || 0" /></el-col>
      </el-row>
    </div>

    <el-dialog v-model="dialogVisible" :title="form.id ? `编辑装柜单：${form.no || ''}` : '新增装柜单'" width="92%" destroy-on-close>
      <el-alert title="保存装柜主单后，可在下方继续添加或编辑装柜商品明细。" type="info" show-icon style="margin-bottom:12px" />
      <el-form label-width="110px">
        <el-row :gutter="16">
          <el-col :span="12"><el-form-item label="SO"><el-select v-model="form.summaryOrderId" filterable clearable style="width:100%"><el-option v-for="s in summaryOrders" :key="s.id" :label="s.no" :value="s.id" /></el-select></el-form-item></el-col>
          <el-col :span="12"><el-form-item label="柜型"><el-select v-model="form.containerType" style="width:100%"><el-option label="20GP" value="20GP"/><el-option label="40GP" value="40GP"/><el-option label="40HQ" value="40HQ"/><el-option label="45HQ" value="45HQ"/></el-select></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="柜号"><el-input v-model="form.containerNo" /></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="封条号"><el-input v-model="form.sealNo" /></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="装柜日期"><el-date-picker v-model="form.loadDate" type="date" value-format="YYYY-MM-DD" style="width:100%" /></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="箱数"><el-input-number v-model="form.totalCartons" :min="0" style="width:100%" /></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="CBM"><el-input-number v-model="form.totalCbm" :min="0" :precision="3" style="width:100%" /></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="KG"><el-input-number v-model="form.totalGwKg" :min="0" :precision="2" style="width:100%" /></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="状态"><el-input v-model="form.status" /></el-form-item></el-col>
          <el-col :span="24"><el-form-item label="备注"><el-input v-model="form.remark" type="textarea" /></el-form-item></el-col>
        </el-row>
      </el-form>
      <DocumentLinesEditor v-if="form.id" document-type="CL" :document-id="form.id" />
      <template #footer><el-button @click="dialogVisible=false">关闭</el-button><el-button type="primary" @click="save">保存主单</el-button></template>
    </el-dialog>

    <el-dialog v-model="shipmentDialogVisible" title="生成出运单" width="620px">
      <el-alert title="从装柜单生成出运单，会复制装柜明细到出运明细。" type="info" show-icon style="margin-bottom:12px" />
      <el-form label-width="110px"><el-form-item label="运输方式"><el-select v-model="shipmentForm.shipmentMode" style="width:100%"><el-option label="海运" value="SEA"/><el-option label="空运" value="AIR"/><el-option label="快递" value="EXPRESS"/><el-option label="直发" value="DIRECT"/></el-select></el-form-item><el-form-item label="承运人"><el-input v-model="shipmentForm.carrier" /></el-form-item><el-form-item label="起运港"><el-input v-model="shipmentForm.departurePort" /></el-form-item><el-form-item label="目的港"><el-input v-model="shipmentForm.destinationPort" /></el-form-item><el-form-item label="ETD"><el-date-picker v-model="shipmentForm.etd" type="date" value-format="YYYY-MM-DD" style="width:100%" /></el-form-item><el-form-item label="ETA"><el-date-picker v-model="shipmentForm.eta" type="date" value-format="YYYY-MM-DD" style="width:100%" /></el-form-item></el-form>
      <template #footer><el-button @click="shipmentDialogVisible=false">取消</el-button><el-button type="primary" @click="generateShipment">生成出运单</el-button></template>
    </el-dialog>
  </div>
</template>
<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { useRoute } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { http } from '../api/http'
import DocumentLinesEditor from '../components/DocumentLinesEditor.vue'
const route = useRoute()
const rows=ref<any[]>([]), summaryOrders=ref<any[]>([]), summaryOrderId=ref<number|null>(null), dialogVisible=ref(false), shipmentDialogVisible=ref(false), selectedContainerId=ref<number|null>(null)
const form=reactive<any>({id:0,no:'',summaryOrderId:null,containerType:'40HQ',containerNo:'',sealNo:'',loadDate:'',status:'draft',totalCartons:0,totalCbm:0,totalGwKg:0,remark:''})
const shipmentForm=reactive<any>({shipmentMode:'SEA',carrier:'',departurePort:'',destinationPort:'',etd:'',eta:''})
const utilization=reactive<any>({})
const recommendation=reactive<any>({})
async function loadSummaryOrders(){summaryOrders.value=(await http.get('/summary-orders')).data}
async function load(){const params:any={}; if(summaryOrderId.value)params.summaryOrderId=summaryOrderId.value; rows.value=(await http.get('/container-loads',{params})).data}
function reset(){Object.assign(form,{id:0,no:'',summaryOrderId:null,containerType:'40HQ',containerNo:'',sealNo:'',loadDate:'',status:'draft',totalCartons:0,totalCbm:0,totalGwKg:0,remark:''})}
function openCreate(){reset();dialogVisible.value=true} function openDocument(row:any){Object.assign(form,row);dialogVisible.value=true}
async function loadUtilization(row:any){const res=await http.get(`/container-loads/${row.id}/utilization`); Object.assign(utilization,res.data)}
async function recommendByContainer(row:any){const res=await http.get('/container-loads/recommend',{params:{containerLoadId:row.id}}); Object.assign(recommendation,res.data)}
async function recommendBySo(){if(!summaryOrderId.value)return ElMessage.warning('请先选择 SO'); const res=await http.get('/container-loads/recommend',{params:{summaryOrderId:summaryOrderId.value}}); Object.assign(recommendation,res.data)}
function openShipmentDialog(row:any){selectedContainerId.value=row.id; Object.assign(shipmentForm,{shipmentMode:'SEA',carrier:'',departurePort:'',destinationPort:'',etd:'',eta:''}); shipmentDialogVisible.value=true}
async function save(){if(!form.containerType)return ElMessage.warning('请选择柜型'); const res=form.id?await http.put(`/container-loads/${form.id}`,form):await http.post('/container-loads',form); if(res.data)Object.assign(form,res.data); ElMessage.success('主单保存成功'); await load()}
async function copy(id:number){await http.post(`/container-loads/${id}/copy`); ElMessage.success('复制成功'); await load()}
async function generateShipment(){if(!selectedContainerId.value)return; const res=await http.post('/shipments/generate-from-container',{containerLoadId:selectedContainerId.value,...shipmentForm}); shipmentDialogVisible.value=false; ElMessage.success(`已生成出运单：${res.data?.no||''}`); await load()}
async function remove(id:number){await ElMessageBox.confirm('确认删除该装柜单？','提示'); await http.delete(`/container-loads/${id}`); ElMessage.success('已删除'); await load()}
onMounted(async()=>{const routeSoId=Number(route.query.summaryOrderId||0); if(routeSoId)summaryOrderId.value=routeSoId; await loadSummaryOrders(); await load(); const routeId=Number(route.query.id||0); const target=rows.value.find(x=>x.id===routeId); if(target)openDocument(target)})
</script>
<style scoped>
.document-no { font-weight: 700; }
</style>