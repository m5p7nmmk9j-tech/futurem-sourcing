<template>
  <div class="page">
    <div class="page-header"><div class="page-title">出运单</div><el-button type="primary" @click="openCreate">新增出运单</el-button></div>
    <div class="card">
      <div class="toolbar">
        <el-select v-model="containerLoadId" placeholder="装柜单" clearable filterable style="width:260px" @change="load"><el-option v-for="c in containerLoads" :key="c.id" :label="c.no" :value="c.id"/></el-select>
        <el-select v-model="summaryOrderId" placeholder="汇总订单" clearable filterable style="width:260px" @change="load"><el-option v-for="s in summaryOrders" :key="s.id" :label="s.no" :value="s.id"/></el-select>
        <el-button @click="load">刷新</el-button>
      </div>
      <el-table :data="rows" border stripe>
        <el-table-column label="出运单号" width="190"><template #default="s"><el-button link type="primary" class="document-no" @click="openDocument(s.row)">{{s.row.no}}</el-button></template></el-table-column>
        <el-table-column prop="shipmentMode" label="方式" width="90"/>
        <el-table-column prop="carrier" label="承运人" width="150"/>
        <el-table-column prop="billOfLadingNo" label="提单号" width="150"/>
        <el-table-column prop="currency" label="币种" width="80"/>
        <el-table-column label="费用合计" width="120"><template #default="s">{{Number(s.row.expenseTotal||0).toFixed(2)}}</template></el-table-column>
        <el-table-column label="立方数" width="100"><template #default="s">{{Number(s.row.finalTotalCbm||0).toFixed(2)}}</template></el-table-column>
        <el-table-column label="状态" width="100"><template #default="s">{{shipmentStatusLabel(s.row.status)}}</template></el-table-column>
        <el-table-column prop="financeSyncStatus" label="财务同步" width="110"/>
        <el-table-column prop="etd" label="ETD" width="120"/><el-table-column prop="eta" label="ETA" width="120"/>
        <el-table-column label="操作" width="240" fixed="right"><template #default="s"><el-button size="small" @click="openDocument(s.row)">编辑</el-button><el-button size="small" @click="copy(s.row.id)">复制</el-button><el-button size="small" type="danger" @click="remove(s.row.id)">删除</el-button></template></el-table-column>
      </el-table>
    </div>

    <el-dialog v-model="dialogVisible" :title="form.id?`出运单：${form.no}`:'新增出运单'" width="94%" destroy-on-close>
      <el-form label-width="110px">
        <el-row :gutter="16">
          <el-col :span="12"><el-form-item label="装柜单"><el-select v-model="form.containerLoadId" clearable filterable style="width:100%"><el-option v-for="c in containerLoads" :key="c.id" :label="c.no" :value="c.id"/></el-select></el-form-item></el-col>
          <el-col :span="12"><el-form-item label="汇总订单"><el-select v-model="form.summaryOrderId" clearable filterable style="width:100%"><el-option v-for="s in summaryOrders" :key="s.id" :label="s.no" :value="s.id"/></el-select></el-form-item></el-col>
          <el-col :span="6"><el-form-item label="运输方式"><el-select v-model="form.shipmentMode" style="width:100%"><el-option label="海运" value="SEA"/><el-option label="空运" value="AIR"/><el-option label="快递" value="EXPRESS"/></el-select></el-form-item></el-col>
          <el-col :span="6"><el-form-item label="币种"><el-select v-model="form.currency" style="width:100%"><el-option label="RMB" value="RMB"/><el-option label="USD" value="USD"/><el-option label="MXN" value="MXN"/></el-select></el-form-item></el-col>
          <el-col :span="6"><el-form-item label="承运人"><el-input v-model="form.carrier"/></el-form-item></el-col>
          <el-col :span="6"><el-form-item label="船名航次"><el-input v-model="form.vesselVoyage"/></el-form-item></el-col>
          <el-col :span="6"><el-form-item label="提单号"><el-input v-model="form.billOfLadingNo"/></el-form-item></el-col>
          <el-col :span="6"><el-form-item label="起运港"><el-input v-model="form.departurePort"/></el-form-item></el-col>
          <el-col :span="6"><el-form-item label="目的港"><el-input v-model="form.destinationPort"/></el-form-item></el-col>
          <el-col :span="6"><el-form-item label="状态"><el-input :model-value="shipmentStatusLabel(form.status)" disabled/></el-form-item></el-col>
          <el-col :span="6"><el-form-item label="ETD"><el-date-picker v-model="form.etd" type="date" value-format="YYYY-MM-DD" style="width:100%"/></el-form-item></el-col>
          <el-col :span="6"><el-form-item label="ETA"><el-date-picker v-model="form.eta" type="date" value-format="YYYY-MM-DD" style="width:100%"/></el-form-item></el-col>
          <el-col :span="6"><el-form-item label="费用合计"><el-input :model-value="`${form.currency} ${Number(form.expenseTotal||0).toFixed(2)}`" disabled/></el-form-item></el-col>
          <el-col :span="6"><el-form-item label="财务同步"><el-input :model-value="form.financeSyncStatus" disabled/></el-form-item></el-col>
          <el-col :span="24" v-if="form.financeSyncMessage"><el-alert :title="form.financeSyncMessage" type="error" show-icon/></el-col>
          <el-col :span="24"><el-form-item label="备注"><el-input v-model="form.remark" type="textarea"/></el-form-item></el-col>
        </el-row>
      </el-form>

      <ShipmentMeasurements v-if="form.id" :shipment="form" @recalculate="recalculateMeasurements"/>
      <DocumentLinesEditor v-if="form.id" document-type="SHP" :document-id="form.id"/>
      <ShipmentExpensesEditor v-if="form.id" :shipment-id="form.id" :currency="form.currency" :shipment-status="form.status" @changed="reloadCurrent"/>

      <template #footer>
        <el-button @click="dialogVisible=false">关闭</el-button>
        <el-button type="primary" @click="save">保存表头</el-button>
        <el-button v-if="form.id&&form.status==='draft'" type="success" @click="confirmShipment">确认出运单</el-button>
        <el-button v-if="form.id&&form.status!=='shipped'&&form.status!=='completed'" type="warning" @click="markShipped">标记已出运</el-button>
        <el-button v-if="form.id&&['confirmed','shipped','completed'].includes(form.status)" @click="syncFinance">同步财务</el-button>
      </template>
    </el-dialog>
  </div>
</template>
<script setup lang="ts">
import {onMounted,reactive,ref} from 'vue'
import {ElMessage,ElMessageBox} from 'element-plus'
import {http} from '../api/http'
import DocumentLinesEditor from '../components/DocumentLinesEditor.vue'
import ShipmentMeasurements from '../components/ShipmentMeasurements.vue'
import ShipmentExpensesEditor from '../components/ShipmentExpensesEditor.vue'
import {shipmentStatusLabel} from '../utils/shipmentFinance'
const rows=ref<any[]>([]),containerLoads=ref<any[]>([]),summaryOrders=ref<any[]>([])
const containerLoadId=ref<number|null>(null),summaryOrderId=ref<number|null>(null),dialogVisible=ref(false)
const defaults={id:0,no:'',containerLoadId:null,summaryOrderId:null,shipmentMode:'SEA',currency:'RMB',carrier:'',vesselVoyage:'',billOfLadingNo:'',departurePort:'',destinationPort:'',etd:'',eta:'',status:'draft',calculatedTotalCbm:0,finalTotalCbm:0,calculatedGrossWeightKg:0,finalGrossWeightKg:0,calculatedNetWeightKg:0,finalNetWeightKg:0,expenseTotal:0,financeSyncStatus:'not_synced',financeSyncMessage:'',remark:''}
const form=reactive<any>({...defaults})
const err=(e:any)=>e?.response?.data?.message||e?.response?.data||e?.message||'操作失败'
async function loadOptions(){const [a,b]=await Promise.all([http.get('/container-loads'),http.get('/summary-orders')]);containerLoads.value=a.data;summaryOrders.value=b.data}
async function load(){const params:any={};if(containerLoadId.value)params.containerLoadId=containerLoadId.value;if(summaryOrderId.value)params.summaryOrderId=summaryOrderId.value;rows.value=(await http.get('/shipments',{params})).data}
function reset(){Object.assign(form,defaults)}
function openCreate(){reset();dialogVisible.value=true}
async function openDocument(row:any){dialogVisible.value=true;Object.assign(form,(await http.get(`/shipments/${row.id}`)).data)}
async function save(){try{if(!form.shipmentMode)return ElMessage.warning('请选择运输方式');const res=form.id?await http.put(`/shipments/${form.id}`,form):await http.post('/shipments',form);Object.assign(form,res.data);ElMessage.success('保存成功');await load()}catch(e:any){ElMessage.error(err(e))}}
async function reloadCurrent(){if(!form.id)return;Object.assign(form,(await http.get(`/shipments/${form.id}`)).data);await load()}
async function recalculateMeasurements(overwriteFinalValues:boolean){try{Object.assign(form,(await http.post(`/shipments/${form.id}/recalculate-measurements`,{overwriteFinalValues})).data);ElMessage.success('体积重量已重新计算')}catch(e:any){ElMessage.error(err(e))}}
async function confirmShipment(){try{Object.assign(form,(await http.post(`/shipments/${form.id}/confirm`)).data);ElMessage.success('出运单已确认');await reloadCurrent()}catch(e:any){ElMessage.error(err(e))}}
async function markShipped(){try{Object.assign(form,(await http.post(`/shipments/${form.id}/mark-shipped`)).data);ElMessage.success('已标记出运');await reloadCurrent()}catch(e:any){ElMessage.error(err(e))}}
async function syncFinance(){try{await http.post(`/shipments/${form.id}/sync-finance`);ElMessage.success('财务同步完成');await reloadCurrent()}catch(e:any){ElMessage.error(err(e))}}
async function copy(id:number){try{await http.post(`/shipments/${id}/copy`);ElMessage.success('复制成功');await load()}catch(e:any){ElMessage.error(err(e))}}
async function remove(id:number){try{await ElMessageBox.confirm('确认删除？','提示');await http.delete(`/shipments/${id}`);ElMessage.success('已删除');await load()}catch(e:any){if(e!=='cancel'&&e!=='close')ElMessage.error(err(e))}}
onMounted(async()=>{await loadOptions();await load()})
</script>
<style scoped>.document-no{font-weight:700}</style>
