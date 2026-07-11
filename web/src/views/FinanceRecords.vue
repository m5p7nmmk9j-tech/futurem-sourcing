<template>
  <div class="page">
    <div class="page-header"><div class="page-title">财务</div><el-button type="primary" @click="openCreate">新增财务记录</el-button></div>
    <div class="card">
      <div class="toolbar"><strong>利润统计</strong><el-button @click="loadAnalytics">刷新利润</el-button></div>
      <el-row :gutter="12">
        <el-col :span="3"><el-statistic title="SO收入" :value="profit.soIncome||0"/></el-col>
        <el-col :span="3"><el-statistic title="PO成本" :value="profit.poCost||0"/></el-col>
        <el-col :span="3"><el-statistic title="出运费用" :value="profit.shipmentExpense||0"/></el-col>
        <el-col :span="3"><el-statistic title="其它费用" :value="profit.expense||0"/></el-col>
        <el-col :span="3"><el-statistic title="毛利润" :value="profit.grossProfit||0"/></el-col>
        <el-col :span="3"><el-statistic title="净利润" :value="profit.netProfit||0"/></el-col>
        <el-col :span="3"><el-statistic title="利润率%" :value="profit.profitRate||0"/></el-col>
        <el-col :span="3"><el-statistic title="预付款抵扣" :value="profit.prepaymentApplied||0"/></el-col>
      </el-row>
    </div>
    <div class="card">
      <el-row :gutter="12">
        <el-col :span="3"><el-statistic title="总金额" :value="summary.totalAmount||0"/></el-col>
        <el-col :span="3"><el-statistic title="已收/已付" :value="summary.paidAmount||0"/></el-col>
        <el-col :span="3"><el-statistic title="预付款抵扣" :value="summary.prepaymentAppliedAmount||0"/></el-col>
        <el-col :span="3"><el-statistic title="转预付款" :value="summary.overpaymentTransferredAmount||0"/></el-col>
        <el-col :span="3"><el-statistic title="余额" :value="summary.balanceAmount||0"/></el-col>
        <el-col :span="3"><el-statistic title="应收余额" :value="summary.receivableBalance||0"/></el-col>
        <el-col :span="3"><el-statistic title="应付余额" :value="summary.payableBalance||0"/></el-col>
        <el-col :span="3"><el-statistic title="待处理" :value="summary.pendingCount||0"/></el-col>
      </el-row>
    </div>
    <div class="card">
      <div class="toolbar"><el-select v-model="agingType" style="width:160px" @change="loadAnalytics"><el-option label="应收账龄" value="receivable"/><el-option label="应付账龄" value="payable"/></el-select><el-button @click="loadAnalytics">刷新统计</el-button></div>
      <el-row :gutter="12"><el-col :span="4"><el-statistic title="30天内" :value="aging.current||0"/></el-col><el-col :span="4"><el-statistic title="31-60天" :value="aging.days31To60||0"/></el-col><el-col :span="4"><el-statistic title="61-90天" :value="aging.days61To90||0"/></el-col><el-col :span="4"><el-statistic title="90天以上" :value="aging.over90||0"/></el-col><el-col :span="4"><el-statistic title="合计" :value="aging.total||0"/></el-col><el-col :span="4"><el-statistic title="笔数" :value="aging.count||0"/></el-col></el-row>
    </div>
    <div class="card">
      <div class="toolbar">
        <el-select v-model="recordType" placeholder="类型" clearable style="width:160px" @change="load"><el-option label="应收" value="receivable"/><el-option label="应付" value="payable"/><el-option label="费用" value="expense"/><el-option label="收入" value="income"/></el-select>
        <el-select v-model="customerId" placeholder="客户" clearable filterable style="width:220px" @change="load"><el-option v-for="c in customers" :key="c.id" :label="c.name" :value="c.id"/></el-select>
        <el-select v-model="supplierId" placeholder="供应商" clearable filterable style="width:220px" @change="load"><el-option v-for="s in suppliers" :key="s.id" :label="s.name" :value="s.id"/></el-select>
        <el-button @click="load">刷新</el-button>
      </div>
      <el-table :data="rows" border stripe>
        <el-table-column prop="no" label="财务单号" width="180"/><el-table-column prop="recordType" label="类型" width="90"/>
        <el-table-column label="来源" width="120"><template #default="s">{{sourceLabel(s.row.targetType)}}</template></el-table-column>
        <el-table-column prop="supplierId" label="供应商ID" width="100"/><el-table-column prop="currency" label="币种" width="70"/>
        <el-table-column label="应收/应付" width="110"><template #default="s">{{fmt(s.row.amount)}}</template></el-table-column>
        <el-table-column label="已收/已付" width="110"><template #default="s">{{fmt(s.row.paidAmount)}}</template></el-table-column>
        <el-table-column label="预付抵扣" width="110"><template #default="s">{{fmt(s.row.prepaymentAppliedAmount)}}</template></el-table-column>
        <el-table-column label="转预付款" width="110"><template #default="s">{{fmt(s.row.overpaymentTransferredAmount)}}</template></el-table-column>
        <el-table-column label="未收/未付" width="110"><template #default="s">{{fmt(expenseOutstanding(s.row))}}</template></el-table-column>
        <el-table-column prop="status" label="状态" width="90"/>
        <el-table-column label="操作" width="230" fixed="right"><template #default="s"><el-button size="small" type="success" :disabled="expenseOutstanding(s.row)<=0" @click="openPayment(s.row)">{{s.row.recordType==='payable'||s.row.recordType==='expense'?'付款':'收款'}}</el-button><el-button size="small" :disabled="s.row.targetType==='SHIPMENT_EXPENSE'" @click="openEdit(s.row)">编辑</el-button><el-button size="small" type="danger" :disabled="s.row.targetType==='SHIPMENT_EXPENSE'" @click="remove(s.row.id)">删除</el-button></template></el-table-column>
      </el-table>
    </div>
    <div class="card"><div class="toolbar"><el-select v-model="balanceType" style="width:160px" @change="loadBalances"><el-option label="按客户应收" value="receivable"/><el-option label="按供应商应付" value="payable"/></el-select></div><el-table :data="balances" border stripe size="small"><el-table-column v-if="balanceType==='receivable'" prop="customerId" label="客户ID" width="120"/><el-table-column v-if="balanceType==='payable'" prop="supplierId" label="供应商ID" width="120"/><el-table-column prop="amount" label="总金额" width="140"/><el-table-column prop="paidAmount" label="已收/已付" width="140"/><el-table-column v-if="balanceType==='payable'" prop="prepaymentAppliedAmount" label="预付款抵扣" width="140"/><el-table-column prop="balanceAmount" label="余额" width="140"/><el-table-column prop="count" label="笔数" width="100"/></el-table></div>
    <el-dialog v-model="dialogVisible" :title="form.id?'编辑财务记录':'新增财务记录'" width="680px"><el-form label-width="110px"><el-form-item label="类型"><el-select v-model="form.recordType" style="width:100%"><el-option label="应收" value="receivable"/><el-option label="应付" value="payable"/><el-option label="费用" value="expense"/><el-option label="收入" value="income"/></el-select></el-form-item><el-form-item label="来源类型"><el-select v-model="form.targetType" style="width:100%"><el-option label="SO" value="SO"/><el-option label="PO" value="PO"/><el-option label="Container" value="CONTAINER"/><el-option label="Shipment" value="SHIPMENT"/><el-option label="Manual" value="MANUAL"/></el-select></el-form-item><el-form-item label="来源ID"><el-input-number v-model="form.targetId" :min="0" style="width:100%"/></el-form-item><el-form-item label="客户"><el-select v-model="form.customerId" clearable filterable style="width:100%"><el-option v-for="c in customers" :key="c.id" :label="c.name" :value="c.id"/></el-select></el-form-item><el-form-item label="供应商"><el-select v-model="form.supplierId" clearable filterable style="width:100%"><el-option v-for="s in suppliers" :key="s.id" :label="s.name" :value="s.id"/></el-select></el-form-item><el-form-item label="币种"><el-input v-model="form.currency"/></el-form-item><el-form-item label="金额"><el-input-number v-model="form.amount" :min="0" :precision="2" style="width:100%"/></el-form-item><el-form-item label="已付/已收"><el-input-number v-model="form.paidAmount" :min="0" :precision="2" style="width:100%"/></el-form-item><el-form-item label="日期"><el-date-picker v-model="form.recordDate" type="date" value-format="YYYY-MM-DD" style="width:100%"/></el-form-item><el-form-item label="备注"><el-input v-model="form.remark" type="textarea"/></el-form-item></el-form><template #footer><el-button @click="dialogVisible=false">取消</el-button><el-button type="primary" @click="save">保存</el-button></template></el-dialog>
    <el-dialog v-model="paymentDialog" :title="payment.direction==='pay'?'付款':'收款'" width="620px"><el-form label-width="110px"><el-form-item label="资金账户"><el-select v-model="payment.bankAccountId" filterable clearable style="width:100%"><el-option v-for="a in accounts" :key="a.id" :label="`${a.name} / ${a.currency} / ${a.currentBalance}`" :value="a.id"/></el-select></el-form-item><el-form-item label="方式"><el-select v-model="payment.paymentMethod" style="width:100%"><el-option label="银行" value="bank"/><el-option label="现金" value="cash"/><el-option label="PayPal" value="paypal"/><el-option label="Wise" value="wise"/><el-option label="其它" value="other"/></el-select></el-form-item><el-form-item label="金额"><el-input-number v-model="payment.amount" :min="0" :precision="2" style="width:100%"/></el-form-item><el-form-item label="手续费"><el-input-number v-model="payment.feeAmount" :min="0" :precision="2" style="width:100%"/></el-form-item><el-form-item label="汇率"><el-input-number v-model="payment.exchangeRate" :min="0" :precision="6" style="width:100%"/></el-form-item><el-form-item label="日期"><el-date-picker v-model="payment.paymentDate" type="date" value-format="YYYY-MM-DD" style="width:100%"/></el-form-item><el-form-item label="附件"><el-input v-model="payment.attachmentUrl"/></el-form-item><el-form-item label="备注"><el-input v-model="payment.remark" type="textarea"/></el-form-item></el-form><template #footer><el-button @click="paymentDialog=false">取消</el-button><el-button type="primary" @click="savePayment">确认</el-button></template></el-dialog>
  </div>
</template>
<script setup lang="ts">
import {onMounted,reactive,ref} from 'vue'
import {ElMessage,ElMessageBox} from 'element-plus'
import {http} from '../api/http'
import {expenseOutstanding} from '../utils/shipmentFinance'
const rows=ref<any[]>([]),customers=ref<any[]>([]),suppliers=ref<any[]>([]),accounts=ref<any[]>([]),balances=ref<any[]>([])
const summary=ref<any>({}),aging=ref<any>({}),profit=ref<any>({})
const recordType=ref<string|null>(null),customerId=ref<number|null>(null),supplierId=ref<number|null>(null),dialogVisible=ref(false),paymentDialog=ref(false)
const agingType=ref('receivable'),balanceType=ref('receivable')
const form=reactive<any>({id:0,recordType:'receivable',targetType:'MANUAL',targetId:0,customerId:null,supplierId:null,currency:'RMB',amount:0,paidAmount:0,recordDate:'',remark:''})
const payment=reactive<any>({direction:'receive',financeRecordId:0,bankAccountId:null,paymentMethod:'bank',amount:0,exchangeRate:1,feeAmount:0,paymentDate:'',attachmentUrl:'',remark:''})
const fmt=(v:any)=>Number(v||0).toFixed(2),err=(e:any)=>e?.response?.data?.message||e?.response?.data||e?.message||'操作失败'
const sourceLabel=(v:string)=>v==='SHIPMENT_EXPENSE'?'出运费用':v
async function loadOptions(){const [a,b,c]=await Promise.all([http.get('/customers'),http.get('/suppliers'),http.get('/bank-accounts')]);customers.value=a.data;suppliers.value=b.data;accounts.value=c.data}
async function load(){try{const params:any={};if(recordType.value)params.recordType=recordType.value;if(customerId.value)params.customerId=customerId.value;if(supplierId.value)params.supplierId=supplierId.value;rows.value=(await http.get('/finance-records',{params})).data;await loadAnalytics()}catch(e:any){ElMessage.error(err(e))}}
async function loadAnalytics(){const [a,b,c]=await Promise.all([http.get('/finance-records/summary'),http.get('/finance-records/profit-summary'),http.get('/finance-records/aging',{params:{recordType:agingType.value}})]);summary.value=a.data;profit.value=b.data;aging.value=c.data;await loadBalances()}
async function loadBalances(){balances.value=(await http.get('/finance-records/partner-balances',{params:{recordType:balanceType.value}})).data}
function reset(){Object.assign(form,{id:0,recordType:'receivable',targetType:'MANUAL',targetId:0,customerId:null,supplierId:null,currency:'RMB',amount:0,paidAmount:0,recordDate:'',remark:''})}
function openCreate(){reset();dialogVisible.value=true}function openEdit(row:any){Object.assign(form,row);dialogVisible.value=true}
async function save(){try{if(!form.recordType)return ElMessage.warning('请选择类型');form.id?await http.put(`/finance-records/${form.id}`,form):await http.post('/finance-records',form);dialogVisible.value=false;ElMessage.success('保存成功');await load()}catch(e:any){ElMessage.error(err(e))}}
async function remove(id:number){try{await ElMessageBox.confirm('确认删除该财务记录？','提示');await http.delete(`/finance-records/${id}`);await load()}catch(e:any){if(e!=='cancel'&&e!=='close')ElMessage.error(err(e))}}
function openPayment(row:any){Object.assign(payment,{direction:(row.recordType==='payable'||row.recordType==='expense')?'pay':'receive',financeRecordId:row.id,bankAccountId:null,paymentMethod:'bank',amount:expenseOutstanding(row),exchangeRate:1,feeAmount:0,paymentDate:'',attachmentUrl:'',remark:''});paymentDialog.value=true}
async function savePayment(){try{if(!payment.amount)return ElMessage.warning('请输入金额');await http.post('/payments',payment);paymentDialog.value=false;ElMessage.success('操作成功');await load();await loadOptions()}catch(e:any){ElMessage.error(err(e))}}
onMounted(async()=>{await loadOptions();await load()})
</script>
