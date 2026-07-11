<template>
  <div class="box">
    <div class="header"><strong>出运费用（{{ currency }}）</strong><el-button type="primary" @click="addVisible=true">新增其他费用</el-button></div>
    <el-table :data="rows" border stripe size="small">
      <el-table-column label="费用" min-width="140"><template #default="s"><el-input v-if="s.row.isCustom" v-model="s.row.expenseName"/><span v-else>{{s.row.expenseName}}</span></template></el-table-column>
      <el-table-column label="供应商" min-width="200"><template #default="s"><el-select v-model="s.row.supplierId" clearable filterable style="width:100%"><el-option v-for="p in suppliers" :key="p.id" :label="p.name" :value="p.id"/></el-select></template></el-table-column>
      <el-table-column label="金额" width="145"><template #default="s"><el-input-number v-model="s.row.amount" :precision="2" :min="0" style="width:100%"/></template></el-table-column>
      <el-table-column label="已付" width="100"><template #default="s">{{fmt(s.row.paidAmount)}}</template></el-table-column>
      <el-table-column label="预付抵扣" width="110"><template #default="s">{{fmt(s.row.prepaymentAppliedAmount)}}</template></el-table-column>
      <el-table-column label="未付" width="100"><template #default="s">{{fmt(s.row.outstandingAmount)}}</template></el-table-column>
      <el-table-column label="状态" width="100"><template #default="s">{{financeStatusLabel(s.row.financeStatus)}}</template></el-table-column>
      <el-table-column label="备注" min-width="160"><template #default="s"><el-input v-model="s.row.remark"/></template></el-table-column>
      <el-table-column label="操作" width="145" fixed="right"><template #default="s"><el-button size="small" type="primary" @click="save(s.row)">保存</el-button><el-button v-if="s.row.isCustom" size="small" type="danger" @click="remove(s.row)">删除</el-button></template></el-table-column>
    </el-table>
    <div class="total">合计：{{currency}} {{fmt(total)}}</div>
    <el-dialog v-model="addVisible" title="新增其他费用" width="500px">
      <el-form label-width="90px">
        <el-form-item label="费用名称"><el-input v-model="draft.expenseName"/></el-form-item>
        <el-form-item label="供应商"><el-select v-model="draft.supplierId" clearable filterable style="width:100%"><el-option v-for="p in suppliers" :key="p.id" :label="p.name" :value="p.id"/></el-select></el-form-item>
        <el-form-item label="金额"><el-input-number v-model="draft.amount" :precision="2" :min="0" style="width:100%"/></el-form-item>
        <el-form-item label="备注"><el-input v-model="draft.remark"/></el-form-item>
      </el-form>
      <template #footer><el-button @click="addVisible=false">取消</el-button><el-button type="primary" @click="create">保存</el-button></template>
    </el-dialog>
  </div>
</template>
<script setup lang="ts">
import {computed,onMounted,reactive,ref,watch} from 'vue'
import {ElMessage,ElMessageBox} from 'element-plus'
import {http} from '../api/http'
import {financeStatusLabel,round2} from '../utils/shipmentFinance'
const props=defineProps<{shipmentId:number;currency:string;shipmentStatus:string}>()
const emit=defineEmits<{(e:'changed'):void}>()
const rows=ref<any[]>([]),suppliers=ref<any[]>([]),addVisible=ref(false)
const draft=reactive<any>({expenseName:'',supplierId:null,amount:0,remark:''})
const total=computed(()=>round2(rows.value.reduce((n,r)=>n+Number(r.amount||0),0)))
const fmt=(v:any)=>Number(v||0).toFixed(2)
const err=(e:any)=>e?.response?.data?.message||e?.response?.data||e?.message||'操作失败'
async function load(){if(!props.shipmentId)return;try{const [a,b]=await Promise.all([http.get(`/shipments/${props.shipmentId}/expenses`),http.get('/suppliers')]);rows.value=a.data;suppliers.value=b.data}catch(e:any){ElMessage.error(err(e))}}
async function save(row:any){if(Number(row.amount||0)>0&&!row.supplierId)return ElMessage.warning('金额大于0时必须选择供应商');try{await http.put(`/shipments/${props.shipmentId}/expenses/${row.id}`,{...row,amount:round2(row.amount),currency:props.currency});ElMessage.success('费用已保存');await load();emit('changed')}catch(e:any){ElMessage.error(err(e))}}
async function create(){if(!draft.expenseName.trim())return ElMessage.warning('请输入费用名称');if(Number(draft.amount||0)>0&&!draft.supplierId)return ElMessage.warning('金额大于0时必须选择供应商');try{await http.post(`/shipments/${props.shipmentId}/expenses`,{...draft,isCustom:true,amount:round2(draft.amount),currency:props.currency});addVisible.value=false;Object.assign(draft,{expenseName:'',supplierId:null,amount:0,remark:''});await load();emit('changed')}catch(e:any){ElMessage.error(err(e))}}
async function remove(row:any){try{await ElMessageBox.confirm(`确认删除${row.expenseName}？`,'提示');await http.delete(`/shipments/${props.shipmentId}/expenses/${row.id}`);await load();emit('changed')}catch(e:any){if(e!=='cancel'&&e!=='close')ElMessage.error(err(e))}}
watch(()=>props.shipmentId,load);onMounted(load)
</script>
<style scoped>.box{margin-top:14px;padding:14px;border:1px solid var(--el-border-color);border-radius:8px}.header{display:flex;justify-content:space-between;align-items:center;margin-bottom:12px}.total{text-align:right;font-weight:700;margin-top:12px}</style>
