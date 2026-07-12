<template>
  <div class="box">
    <div class="header">
      <div><strong>物流服务费用（人民币）</strong><div class="note">每行独立选择物流服务商，分别填写服务商成本和客户收费。</div></div>
      <el-button v-if="editable" type="primary" @click="addVisible = true">新增其他服务</el-button>
    </div>
    <el-table :data="rows" border stripe size="small">
      <el-table-column label="服务项目" min-width="145"><template #default="s"><el-input v-if="s.row.isCustom && editable" v-model="s.row.expenseName"/><span v-else>{{ s.row.expenseName }}</span></template></el-table-column>
      <el-table-column label="物流服务商" min-width="190"><template #default="s"><el-select v-model="s.row.logisticsProviderId" clearable filterable :disabled="!editable" style="width:100%"><el-option v-for="p in providers" :key="p.id" :label="p.name" :value="p.id"/></el-select></template></el-table-column>
      <el-table-column label="服务商成本" width="145"><template #default="s"><el-input-number v-model="s.row.providerCost" :precision="2" :min="0" :disabled="!editable" style="width:100%"/></template></el-table-column>
      <el-table-column label="客户收费" width="145"><template #default="s"><el-input-number v-model="s.row.customerCharge" :precision="2" :min="0" :disabled="!editable" style="width:100%"/></template></el-table-column>
      <el-table-column label="利润" width="120" align="right"><template #default="s">{{ fmt(Number(s.row.customerCharge||0)-Number(s.row.providerCost||0)) }}</template></el-table-column>
      <el-table-column label="已付" width="100" align="right"><template #default="s">{{ fmt(s.row.paidAmount) }}</template></el-table-column>
      <el-table-column label="未付" width="100" align="right"><template #default="s">{{ fmt(s.row.outstandingAmount) }}</template></el-table-column>
      <el-table-column label="备注" min-width="150"><template #default="s"><el-input v-model="s.row.remark" :disabled="!editable"/></template></el-table-column>
      <el-table-column v-if="editable" label="操作" width="145" fixed="right"><template #default="s"><el-button size="small" type="primary" @click="save(s.row)">保存</el-button><el-button v-if="s.row.isCustom" size="small" type="danger" @click="remove(s.row)">删除</el-button></template></el-table-column>
    </el-table>
    <div class="totals">
      <span>服务商成本：¥{{ fmt(providerCostTotal) }}</span>
      <span>客户收费：¥{{ fmt(customerChargeTotal) }}</span>
      <strong>物流利润：¥{{ fmt(profitTotal) }}</strong>
    </div>

    <el-dialog v-model="addVisible" title="新增其他物流服务" width="520px" append-to-body>
      <el-form label-width="100px">
        <el-form-item label="服务名称"><el-input v-model="draft.expenseName"/></el-form-item>
        <el-form-item label="服务类型"><el-select v-model="draft.serviceType" style="width:100%"><el-option v-for="item in serviceTypes" :key="item.value" :label="item.label" :value="item.value"/></el-select></el-form-item>
        <el-form-item label="物流服务商"><el-select v-model="draft.logisticsProviderId" clearable filterable style="width:100%"><el-option v-for="p in providers" :key="p.id" :label="p.name" :value="p.id"/></el-select></el-form-item>
        <el-form-item label="服务商成本"><el-input-number v-model="draft.providerCost" :precision="2" :min="0" style="width:100%"/></el-form-item>
        <el-form-item label="客户收费"><el-input-number v-model="draft.customerCharge" :precision="2" :min="0" style="width:100%"/></el-form-item>
        <el-form-item label="备注"><el-input v-model="draft.remark"/></el-form-item>
      </el-form>
      <template #footer><el-button @click="addVisible=false">取消</el-button><el-button type="primary" @click="create">保存</el-button></template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { http } from '../api/http'
import { round2 } from '../utils/shipmentFinance'

const props = defineProps<{ shipmentId:number; shipmentStatus:string }>()
const emit = defineEmits<{ changed: [] }>()
const rows = ref<any[]>([])
const providers = ref<any[]>([])
const addVisible = ref(false)
const editable = computed(() => props.shipmentStatus === 'draft')
const serviceTypes = [
  { label:'海运', value:'ocean_freight' }, { label:'报关', value:'customs' },
  { label:'拖车', value:'trucking' }, { label:'仓储', value:'warehouse' },
  { label:'快递', value:'courier' }, { label:'其他', value:'other_service' }
]
const draft = reactive<any>({ expenseName:'', serviceType:'other_service', logisticsProviderId:null, providerCost:0, customerCharge:0, remark:'' })
const providerCostTotal = computed(() => round2(rows.value.reduce((n,r)=>n+Number(r.providerCost||0),0)))
const customerChargeTotal = computed(() => round2(rows.value.reduce((n,r)=>n+Number(r.customerCharge||0),0)))
const profitTotal = computed(() => round2(customerChargeTotal.value-providerCostTotal.value))
const fmt = (value:any) => Number(value||0).toFixed(2)
const err = (e:any) => e?.response?.data?.message || e?.response?.data || e?.message || '操作失败'

async function load(){
  if(!props.shipmentId) return
  try{
    const [expenseResponse, providerResponse] = await Promise.all([
      http.get(`/shipments/${props.shipmentId}/expenses`),
      http.get('/logistics-providers',{params:{status:'active'}})
    ])
    rows.value = expenseResponse.data || []
    providers.value = providerResponse.data || []
  }catch(e:any){ ElMessage.error(err(e)) }
}
function validate(row:any){
  if((Number(row.providerCost||0)>0 || Number(row.customerCharge||0)>0) && !row.logisticsProviderId){ ElMessage.warning('有金额时必须选择物流服务商'); return false }
  return true
}
async function save(row:any){
  if(!validate(row)) return
  try{
    await http.put(`/shipments/${props.shipmentId}/expenses/${row.id}`,{...row,providerCost:round2(row.providerCost),customerCharge:round2(row.customerCharge)})
    ElMessage.success('物流费用已保存'); await load(); emit('changed')
  }catch(e:any){ ElMessage.error(err(e)) }
}
async function create(){
  if(!draft.expenseName.trim()) return ElMessage.warning('请输入服务名称')
  if(!validate(draft)) return
  try{
    await http.post(`/shipments/${props.shipmentId}/expenses`,{...draft,isCustom:true,providerCost:round2(draft.providerCost),customerCharge:round2(draft.customerCharge)})
    addVisible.value=false
    Object.assign(draft,{expenseName:'',serviceType:'other_service',logisticsProviderId:null,providerCost:0,customerCharge:0,remark:''})
    await load(); emit('changed')
  }catch(e:any){ ElMessage.error(err(e)) }
}
async function remove(row:any){
  try{ await ElMessageBox.confirm(`确认删除${row.expenseName}？`,'提示'); await http.delete(`/shipments/${props.shipmentId}/expenses/${row.id}`); await load(); emit('changed') }
  catch(e:any){ if(e!=='cancel'&&e!=='close') ElMessage.error(err(e)) }
}
watch(()=>props.shipmentId,load)
onMounted(load)
</script>

<style scoped>
.box{margin-top:14px;padding:14px;border:1px solid var(--el-border-color);border-radius:8px}.header{display:flex;justify-content:space-between;align-items:center;margin-bottom:12px}.note{margin-top:3px;color:#64748b;font-size:12px}.totals{display:flex;justify-content:flex-end;gap:24px;margin-top:12px}.totals strong{color:#1d4ed8}
</style>
