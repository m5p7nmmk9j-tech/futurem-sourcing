<template>
  <div class="page">
    <div class="page-header">
      <div><div class="page-title">应收应付</div><div class="sub">商品货款与客户物流费用合并在同一应收单中，明细分类展示。全部人民币。</div></div>
    </div>
    <div class="card stats">
      <el-statistic title="应收余额" :value="summary.receivableBalance || 0" prefix="¥" />
      <el-statistic title="应付余额" :value="summary.payableBalance || 0" prefix="¥" />
      <el-statistic title="已收/已付" :value="summary.paidAmount || 0" prefix="¥" />
      <el-statistic title="待处理" :value="summary.pendingCount || 0" />
    </div>
    <div class="card">
      <div class="toolbar">
        <el-select v-model="filters.recordType" clearable placeholder="应收/应付" @change="load"><el-option label="应收" value="receivable"/><el-option label="应付" value="payable"/></el-select>
        <el-select v-model="filters.customerId" clearable filterable placeholder="客户" @change="load"><el-option v-for="x in customers" :key="x.id" :label="x.name" :value="x.id"/></el-select>
        <el-select v-model="filters.supplierId" clearable filterable placeholder="商品供应商" @change="load"><el-option v-for="x in suppliers" :key="x.id" :label="x.name" :value="x.id"/></el-select>
        <el-select v-model="filters.logisticsProviderId" clearable filterable placeholder="物流服务商" @change="load"><el-option v-for="x in providers" :key="x.id" :label="x.name" :value="x.id"/></el-select>
        <el-button @click="load">刷新</el-button>
      </div>
      <el-table :data="rows" border stripe>
        <el-table-column prop="no" label="财务单号" width="180"/>
        <el-table-column label="类型" width="85"><template #default="s">{{s.row.recordType==='receivable'?'应收':'应付'}}</template></el-table-column>
        <el-table-column label="往来对象" min-width="180"><template #default="s">{{counterparty(s.row)}}</template></el-table-column>
        <el-table-column label="来源" min-width="160"><template #default="s">{{sourceLabel(s.row.targetType)}}</template></el-table-column>
        <el-table-column label="金额" width="120" align="right"><template #default="s">¥{{money(s.row.amount)}}</template></el-table-column>
        <el-table-column label="已结算" width="120" align="right"><template #default="s">¥{{money(s.row.paidAmount)}}</template></el-table-column>
        <el-table-column label="未结算" width="120" align="right"><template #default="s">¥{{money(outstanding(s.row))}}</template></el-table-column>
        <el-table-column label="状态" width="100"><template #default="s">{{statusLabel(s.row.status)}}</template></el-table-column>
        <el-table-column label="操作" width="160" fixed="right"><template #default="s"><el-button size="small" @click="openLines(s.row)">查看明细</el-button><el-button size="small" type="success" :disabled="outstanding(s.row)<=0" @click="openPayment(s.row)">{{s.row.recordType==='payable'?'付款':'收款'}}</el-button></template></el-table-column>
      </el-table>
    </div>
    <el-dialog v-model="linesVisible" :title="`财务明细：${current?.no || ''}`" width="920px" destroy-on-close><FinanceRecordLines v-if="current?.id" :finance-record-id="current.id"/></el-dialog>
    <el-dialog v-model="paymentVisible" :title="payment.direction==='pay'?'付款':'收款'" width="540px">
      <el-form label-width="100px">
        <el-form-item label="资金账户"><el-select v-model="payment.bankAccountId" filterable style="width:100%"><el-option v-for="x in accounts" :key="x.id" :label="`${x.name} / ¥${money(x.currentBalance)}`" :value="x.id"/></el-select></el-form-item>
        <el-form-item label="金额"><el-input-number v-model="payment.amount" :min="0" :precision="2" style="width:100%"/></el-form-item>
        <el-form-item label="日期"><el-date-picker v-model="payment.paymentDate" type="date" value-format="YYYY-MM-DD" style="width:100%"/></el-form-item>
        <el-form-item label="备注"><el-input v-model="payment.remark" type="textarea"/></el-form-item>
      </el-form>
      <template #footer><el-button @click="paymentVisible=false">取消</el-button><el-button type="primary" @click="savePayment">确认</el-button></template>
    </el-dialog>
  </div>
</template>
<script setup lang="ts">
import {onMounted,reactive,ref} from 'vue'
import {ElMessage} from 'element-plus'
import {http} from '../api/http'
import FinanceRecordLines from '../components/FinanceRecordLines.vue'
import {expenseOutstanding} from '../utils/shipmentFinance'
const rows=ref<any[]>([]),customers=ref<any[]>([]),suppliers=ref<any[]>([]),providers=ref<any[]>([]),accounts=ref<any[]>([]),summary=ref<any>({}),linesVisible=ref(false),paymentVisible=ref(false),current=ref<any>(null)
const filters=reactive<any>({recordType:null,customerId:null,supplierId:null,logisticsProviderId:null})
const payment=reactive<any>({direction:'receive',financeRecordId:0,bankAccountId:null,paymentMethod:'bank',amount:0,exchangeRate:1,feeAmount:0,paymentDate:'',remark:''})
const money=(v:any)=>Number(v||0).toFixed(2),outstanding=(r:any)=>expenseOutstanding(r),err=(e:any)=>e?.response?.data?.message||e?.message||'操作失败'
async function loadOptions(){const[a,b,c,d]=await Promise.all([http.get('/customers'),http.get('/suppliers'),http.get('/logistics-providers'),http.get('/bank-accounts')]);customers.value=a.data||[];suppliers.value=b.data||[];providers.value=c.data||[];accounts.value=d.data||[]}
async function load(){const params:any={};Object.entries(filters).forEach(([k,v])=>{if(v)params[k]=v});const[a,b]=await Promise.all([http.get('/finance-records',{params}),http.get('/finance-records/summary')]);rows.value=a.data||[];summary.value=b.data||{}}
function counterparty(r:any){if(r.logisticsProviderId)return `物流：${providers.value.find(x=>x.id===r.logisticsProviderId)?.name||r.logisticsProviderId}`;if(r.customerId)return `客户：${customers.value.find(x=>x.id===r.customerId)?.name||r.customerId}`;return `供应商：${suppliers.value.find(x=>x.id===r.supplierId)?.name||r.supplierId||''}`}
function sourceLabel(v:string){return ({CONTAINER_LOAD:'装柜商品与物流应收',SHIPMENT_EXPENSE:'物流服务商应付',QC_ACCEPTED_LINE:'验货商品应付'} as any)[v]||v}
function statusLabel(v:string){return ({pending:'未结清',partial:'部分结清',done:'已结清'} as any)[v]||v}
function openLines(r:any){current.value=r;linesVisible.value=true}
function openPayment(r:any){Object.assign(payment,{direction:r.recordType==='payable'?'pay':'receive',financeRecordId:r.id,bankAccountId:null,paymentMethod:'bank',amount:outstanding(r),exchangeRate:1,feeAmount:0,paymentDate:'',remark:''});paymentVisible.value=true}
async function savePayment(){try{await http.post('/payments',payment);paymentVisible.value=false;ElMessage.success('操作成功');await load()}catch(e:any){ElMessage.error(err(e))}}
onMounted(async()=>{await loadOptions();await load()})
</script>
<style scoped>.sub{margin-top:4px;color:#64748b;font-size:13px}.stats{display:grid;grid-template-columns:repeat(4,1fr);gap:16px}.toolbar{display:flex;gap:10px;margin-bottom:14px;flex-wrap:wrap}.toolbar .el-select{width:190px}</style>
