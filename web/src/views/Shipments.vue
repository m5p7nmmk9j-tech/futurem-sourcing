<template>
  <div class="page">
    <div class="page-header"><div><div class="page-title">出运单</div><div class="page-subtitle">一张装柜单对应一张出运单，所有金额统一人民币。</div></div></div>
    <div class="card">
      <div class="toolbar"><el-select v-model="containerLoadId" placeholder="装柜单" clearable filterable style="width:260px" @change="load"><el-option v-for="c in containerLoads" :key="c.id" :label="c.no" :value="c.id"/></el-select><el-select v-model="summaryOrderId" placeholder="原汇总单" clearable filterable style="width:260px" @change="load"><el-option v-for="s in summaryOrders" :key="s.id" :label="s.no" :value="s.id"/></el-select><el-button @click="load">刷新</el-button></div>
      <el-table :data="rows" border stripe>
        <el-table-column label="出运单号" width="180"><template #default="s"><el-button link type="primary" class="document-no" @click="openDocument(s.row)">{{s.row.no}}</el-button></template></el-table-column>
        <el-table-column prop="containerNo" label="柜号" width="145"/><el-table-column prop="sealNo" label="封条号" width="130"/><el-table-column prop="shipmentMode" label="方式" width="80"/><el-table-column prop="carrier" label="承运人" width="140"/><el-table-column prop="billOfLadingNo" label="提单号" width="145"/>
        <el-table-column label="服务商成本" width="120" align="right"><template #default="s">¥{{fmt(s.row.expenseTotal)}}</template></el-table-column><el-table-column label="客户收费" width="120" align="right"><template #default="s">¥{{fmt(s.row.customerChargeTotal)}}</template></el-table-column><el-table-column label="物流利润" width="120" align="right"><template #default="s">¥{{fmt(s.row.logisticsProfitTotal)}}</template></el-table-column>
        <el-table-column label="状态" width="105"><template #default="s">{{shipmentStatusLabel(s.row.status)}}</template></el-table-column><el-table-column prop="etd" label="ETD" width="120"/><el-table-column prop="eta" label="ETA" width="120"/>
        <el-table-column label="操作" width="130" fixed="right"><template #default="s"><el-button size="small" @click="openDocument(s.row)">查看</el-button><el-button v-if="!s.row.containerLoadId" size="small" @click="copy(s.row.id)">复制</el-button></template></el-table-column>
      </el-table>
    </div>

    <el-dialog v-model="dialogVisible" :title="`出运单：${form.no||''}`" width="94%" destroy-on-close>
      <el-form label-width="105px" :disabled="locked">
        <el-row :gutter="16">
          <el-col :span="6"><el-form-item label="装柜单"><el-input :model-value="containerName(form.containerLoadId)" disabled/></el-form-item></el-col><el-col :span="6"><el-form-item label="柜型"><el-input v-model="form.containerType" disabled/></el-form-item></el-col><el-col :span="6"><el-form-item label="柜号"><el-input v-model="form.containerNo" disabled/></el-form-item></el-col><el-col :span="6"><el-form-item label="封条号"><el-input v-model="form.sealNo" disabled/></el-form-item></el-col>
          <el-col :span="6"><el-form-item label="运输方式"><el-select v-model="form.shipmentMode" style="width:100%"><el-option label="海运" value="SEA"/><el-option label="空运" value="AIR"/><el-option label="快递" value="EXPRESS"/></el-select></el-form-item></el-col><el-col :span="6"><el-form-item label="船公司/承运人"><el-input v-model="form.carrier"/></el-form-item></el-col><el-col :span="6"><el-form-item label="船名航次"><el-input v-model="form.vesselVoyage"/></el-form-item></el-col><el-col :span="6"><el-form-item label="提单号"><el-input v-model="form.billOfLadingNo"/></el-form-item></el-col>
          <el-col :span="6"><el-form-item label="起运港"><el-input v-model="form.departurePort"/></el-form-item></el-col><el-col :span="6"><el-form-item label="目的港"><el-input v-model="form.destinationPort"/></el-form-item></el-col><el-col :span="6"><el-form-item label="ETD"><el-date-picker v-model="form.etd" type="date" value-format="YYYY-MM-DD" style="width:100%"/></el-form-item></el-col><el-col :span="6"><el-form-item label="ETA"><el-date-picker v-model="form.eta" type="date" value-format="YYYY-MM-DD" style="width:100%"/></el-form-item></el-col>
          <el-col :span="6"><el-form-item label="状态"><el-input :model-value="shipmentStatusLabel(form.status)" disabled/></el-form-item></el-col><el-col :span="6"><el-form-item label="服务商成本"><el-input :model-value="`¥${fmt(form.expenseTotal)}`" disabled/></el-form-item></el-col><el-col :span="6"><el-form-item label="客户收费"><el-input :model-value="`¥${fmt(form.customerChargeTotal)}`" disabled/></el-form-item></el-col><el-col :span="6"><el-form-item label="物流利润"><el-input :model-value="`¥${fmt(form.logisticsProfitTotal)}`" disabled/></el-form-item></el-col>
          <el-col :span="24"><el-form-item label="备注"><el-input v-model="form.remark" type="textarea"/></el-form-item></el-col>
        </el-row>
      </el-form>
      <ShipmentMeasurements v-if="form.id" :shipment="form" @recalculate="recalculateMeasurements"/>
      <LogisticsExpenseEditor v-if="form.id" :shipment-id="form.id" :shipment-status="form.status" @changed="reloadCurrent"/>
      <template #footer><el-button @click="dialogVisible=false">关闭</el-button><el-button v-if="form.id&&form.status==='draft'" type="primary" @click="save">保存出运资料</el-button><el-button v-if="form.id&&form.status==='draft'" type="success" @click="confirmShipment">确认出运单</el-button><el-button v-if="form.id&&form.status==='confirmed'" type="warning" @click="markShipped">确认货柜离仓发运</el-button></template>
    </el-dialog>
  </div>
</template>
<script setup lang="ts">
import {computed,onMounted,reactive,ref} from 'vue'
import {ElMessage} from 'element-plus'
import {http} from '../api/http'
import ShipmentMeasurements from '../components/ShipmentMeasurements.vue'
import LogisticsExpenseEditor from '../components/LogisticsExpenseEditor.vue'
import {shipmentStatusLabel} from '../utils/shipmentFinance'
const rows=ref<any[]>([]),containerLoads=ref<any[]>([]),summaryOrders=ref<any[]>([]),containerLoadId=ref<number|null>(null),summaryOrderId=ref<number|null>(null),dialogVisible=ref(false)
const defaults={id:0,no:'',containerLoadId:null,summaryOrderId:null,shipmentMode:'SEA',currency:'RMB',carrier:'',vesselVoyage:'',billOfLadingNo:'',departurePort:'',destinationPort:'',etd:'',eta:'',status:'draft',calculatedTotalCbm:0,finalTotalCbm:0,calculatedGrossWeightKg:0,finalGrossWeightKg:0,calculatedNetWeightKg:0,finalNetWeightKg:0,expenseTotal:0,customerChargeTotal:0,logisticsProfitTotal:0,remark:''}
const form=reactive<any>({...defaults}),locked=computed(()=>['shipped','completed'].includes(form.status)),fmt=(v:any)=>Number(v||0).toFixed(2),err=(e:any)=>e?.response?.data?.message||e?.response?.data||e?.message||'操作失败'
async function loadOptions(){const[a,b]=await Promise.all([http.get('/container-loads'),http.get('/summary-orders')]);containerLoads.value=a.data||[];summaryOrders.value=b.data||[]}
async function load(){const params:any={};if(containerLoadId.value)params.containerLoadId=containerLoadId.value;if(summaryOrderId.value)params.summaryOrderId=summaryOrderId.value;rows.value=(await http.get('/shipments',{params})).data||[]}
function containerName(id:any){return containerLoads.value.find(x=>x.id===id)?.no||''}
async function openDocument(row:any){dialogVisible.value=true;Object.assign(form,(await http.get(`/shipments/${row.id}`)).data)}
async function save(){try{const res=await http.put(`/shipments/${form.id}`,form);Object.assign(form,res.data);ElMessage.success('出运资料已保存');await load()}catch(e:any){ElMessage.error(err(e))}}
async function reloadCurrent(){if(!form.id)return;Object.assign(form,(await http.get(`/shipments/${form.id}`)).data);await load()}
async function recalculateMeasurements(overwriteFinalValues:boolean){try{Object.assign(form,(await http.post(`/shipments/${form.id}/recalculate-measurements`,{overwriteFinalValues})).data)}catch(e:any){ElMessage.error(err(e))}}
async function confirmShipment(){try{Object.assign(form,(await http.post(`/shipments/${form.id}/confirm`)).data);ElMessage.success('出运单已确认');await reloadCurrent()}catch(e:any){ElMessage.error(err(e))}}
async function markShipped(){try{Object.assign(form,(await http.post(`/shipments/${form.id}/mark-shipped`)).data);ElMessage.success('货柜已确认离仓发运');await reloadCurrent()}catch(e:any){ElMessage.error(err(e))}}
async function copy(id:number){try{await http.post(`/shipments/${id}/copy`);await load()}catch(e:any){ElMessage.error(err(e))}}
onMounted(async()=>{await loadOptions();await load()})
</script>
<style scoped>.page-subtitle{margin-top:4px;color:#64748b;font-size:13px}.toolbar{display:flex;gap:10px;margin-bottom:14px}.document-no{font-weight:700}</style>
