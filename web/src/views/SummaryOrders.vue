<template>
  <div class="page">
    <div class="page-header"><div class="page-title">SO 汇总单</div><el-button type="primary" @click="openCreate">新增 SO</el-button></div>
    <div class="card">
      <div class="toolbar"><el-select v-model="customerId" placeholder="按客户筛选" clearable filterable style="width:260px" @change="load"><el-option v-for="c in customers" :key="c.id" :label="c.name" :value="c.id" /></el-select><el-button @click="load">刷新</el-button></div>
      <el-table :data="rows" border stripe>
        <el-table-column prop="no" label="SO单号" width="190"/><el-table-column prop="customerId" label="客户ID" width="100"/><el-table-column prop="orderDate" label="汇总日期" width="150"/><el-table-column prop="currency" label="币种" width="90"/><el-table-column prop="goodsAmount" label="货款" width="120"/><el-table-column prop="commissionFee" label="佣金" width="120"/><el-table-column prop="warehouseFee" label="仓库费" width="120"/><el-table-column prop="loadingFee" label="装柜费" width="120"/><el-table-column prop="logisticsFee" label="物流费" width="120"/><el-table-column prop="receivableAmount" label="应收金额" width="130"/><el-table-column prop="receivedAmount" label="已收金额" width="130"/><el-table-column prop="status" label="状态" width="110"/>
        <el-table-column label="操作" width="300" fixed="right"><template #default="scope"><el-button size="small" type="success" @click="selectRow(scope.row)">明细</el-button><el-button size="small" @click="openEdit(scope.row)">编辑</el-button><el-button size="small" @click="copy(scope.row.id)">复制</el-button><el-button size="small" type="danger" @click="remove(scope.row.id)">删除</el-button></template></el-table-column>
      </el-table>
      <DocumentLinesEditor v-if="selectedId" document-type="SO" :document-id="selectedId" />
    </div>
    <el-dialog v-model="dialogVisible" :title="form.id ? '编辑 SO' : '新增 SO'" width="680px">
      <el-alert title="SO 是多个 PO 汇总后给客户收款的依据，明细可统计 CBM / KG / CTN。" type="info" show-icon style="margin-bottom:12px" />
      <el-form label-width="110px"><el-form-item label="客户"><el-select v-model="form.customerId" filterable placeholder="选择客户" style="width:100%"><el-option v-for="c in customers" :key="c.id" :label="c.name" :value="c.id" /></el-select></el-form-item><el-form-item label="汇总日期"><el-date-picker v-model="form.orderDate" type="date" value-format="YYYY-MM-DD" style="width:100%" /></el-form-item><el-form-item label="币种"><el-input v-model="form.currency" /></el-form-item><el-form-item label="状态"><el-input v-model="form.status" /></el-form-item><el-form-item label="货款"><el-input-number v-model="form.goodsAmount" :min="0" style="width:100%" /></el-form-item><el-form-item label="佣金"><el-input-number v-model="form.commissionFee" :min="0" style="width:100%" /></el-form-item><el-form-item label="仓库费"><el-input-number v-model="form.warehouseFee" :min="0" style="width:100%" /></el-form-item><el-form-item label="装柜费"><el-input-number v-model="form.loadingFee" :min="0" style="width:100%" /></el-form-item><el-form-item label="物流费"><el-input-number v-model="form.logisticsFee" :min="0" style="width:100%" /></el-form-item><el-form-item label="其他费"><el-input-number v-model="form.otherFee" :min="0" style="width:100%" /></el-form-item><el-form-item label="已收金额"><el-input-number v-model="form.receivedAmount" :min="0" style="width:100%" /></el-form-item><el-form-item label="应收金额"><el-input :model-value="receivable" disabled /></el-form-item><el-form-item label="备注"><el-input v-model="form.remark" type="textarea" /></el-form-item></el-form>
      <template #footer><el-button @click="dialogVisible=false">取消</el-button><el-button type="primary" @click="save">保存</el-button></template>
    </el-dialog>
  </div>
</template>
<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { http } from '../api/http'
import DocumentLinesEditor from '../components/DocumentLinesEditor.vue'
const rows=ref<any[]>([]), customers=ref<any[]>([]), customerId=ref<number|null>(null), dialogVisible=ref(false), selectedId=ref<number|null>(null)
const form=reactive<any>({id:0,customerId:null,orderDate:'',currency:'USD',status:'draft',goodsAmount:0,commissionFee:0,warehouseFee:0,loadingFee:0,logisticsFee:0,otherFee:0,receivedAmount:0,remark:''})
const receivable=computed(()=>Number(form.goodsAmount||0)+Number(form.commissionFee||0)+Number(form.warehouseFee||0)+Number(form.loadingFee||0)+Number(form.logisticsFee||0)+Number(form.otherFee||0))
async function loadCustomers(){customers.value=(await http.get('/customers')).data}
async function load(){const params:any={}; if(customerId.value)params.customerId=customerId.value; rows.value=(await http.get('/summary-orders',{params})).data; if(!selectedId.value&&rows.value.length)selectedId.value=rows.value[0].id}
function reset(){Object.assign(form,{id:0,customerId:null,orderDate:'',currency:'USD',status:'draft',goodsAmount:0,commissionFee:0,warehouseFee:0,loadingFee:0,logisticsFee:0,otherFee:0,receivedAmount:0,remark:''})}
function openCreate(){reset();dialogVisible.value=true} function openEdit(row:any){Object.assign(form,row);dialogVisible.value=true} function selectRow(row:any){selectedId.value=row.id}
async function save(){if(!form.customerId)return ElMessage.warning('请选择客户'); const res=form.id?await http.put(`/summary-orders/${form.id}`,form):await http.post('/summary-orders',form); dialogVisible.value=false; ElMessage.success('保存成功'); await load(); selectedId.value=res.data?.id||form.id||selectedId.value}
async function copy(id:number){await http.post(`/summary-orders/${id}/copy`); ElMessage.success('复制成功'); await load()}
async function remove(id:number){await ElMessageBox.confirm('确认删除该 SO？','提示'); await http.delete(`/summary-orders/${id}`); if(selectedId.value===id)selectedId.value=null; ElMessage.success('已删除'); await load()}
onMounted(async()=>{await loadCustomers();await load()})
</script>
