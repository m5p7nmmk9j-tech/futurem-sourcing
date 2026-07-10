<template>
  <div class="page">
    <div class="page-header">
      <div class="page-title">审批流 Approvals</div>
      <div class="toolbar"><el-button type="primary" @click="openCreate">新增审批</el-button><el-button @click="load">刷新</el-button></div>
    </div>

    <div class="card">
      <div class="toolbar">
        <el-select v-model="status" clearable placeholder="状态" style="width:150px" @change="load"><el-option label="草稿" value="draft"/><el-option label="已提交" value="submitted"/><el-option label="审批中" value="approving"/><el-option label="已通过" value="approved"/><el-option label="已驳回" value="rejected"/><el-option label="已退回" value="returned"/></el-select>
        <el-select v-model="targetType" clearable placeholder="对象" style="width:150px" @change="load"><el-option label="PO" value="PO"/><el-option label="PAYMENT" value="PAYMENT"/><el-option label="RFQ" value="RFQ"/><el-option label="EXPENSE" value="EXPENSE"/><el-option label="DISCOUNT" value="DISCOUNT"/></el-select>
      </div>
      <el-table :data="rows" border stripe>
        <el-table-column prop="no" label="审批号" width="180"/>
        <el-table-column prop="approvalType" label="类型" width="120"/>
        <el-table-column prop="targetType" label="对象" width="110"/>
        <el-table-column prop="targetId" label="对象ID" width="90"/>
        <el-table-column prop="title" label="标题" min-width="220"/>
        <el-table-column prop="amount" label="金额" width="120"/>
        <el-table-column prop="currency" label="币种" width="80"/>
        <el-table-column prop="status" label="状态" width="110"><template #default="scope"><el-tag :type="statusType(scope.row.status)">{{ statusName(scope.row.status) }}</el-tag></template></el-table-column>
        <el-table-column label="操作" width="360" fixed="right"><template #default="scope"><el-button size="small" @click="view(scope.row)">查看</el-button><el-button size="small" type="primary" @click="submit(scope.row.id)" :disabled="scope.row.status!=='draft'&&scope.row.status!=='returned'">提交</el-button><el-button size="small" type="success" @click="approve(scope.row.id)" :disabled="!['submitted','approving'].includes(scope.row.status)">通过</el-button><el-button size="small" type="warning" @click="returnBack(scope.row.id)" :disabled="!['submitted','approving'].includes(scope.row.status)">退回</el-button><el-button size="small" type="danger" @click="reject(scope.row.id)" :disabled="!['submitted','approving'].includes(scope.row.status)">驳回</el-button></template></el-table-column>
      </el-table>
    </div>

    <el-dialog v-model="dialogVisible" title="新增审批" width="680px">
      <el-form label-width="110px">
        <el-form-item label="审批类型"><el-select v-model="form.approvalType" style="width:100%"><el-option label="采购审批" value="purchase"/><el-option label="付款审批" value="payment"/><el-option label="报价审批" value="quotation"/><el-option label="费用审批" value="expense"/><el-option label="折扣审批" value="discount"/></el-select></el-form-item>
        <el-form-item label="对象类型"><el-select v-model="form.targetType" style="width:100%"><el-option label="PO" value="PO"/><el-option label="PAYMENT" value="PAYMENT"/><el-option label="RFQ" value="RFQ"/><el-option label="EXPENSE" value="EXPENSE"/><el-option label="DISCOUNT" value="DISCOUNT"/></el-select></el-form-item>
        <el-form-item label="对象ID"><el-input-number v-model="form.targetId" :min="0" style="width:100%"/></el-form-item>
        <el-form-item label="标题"><el-input v-model="form.title"/></el-form-item>
        <el-form-item label="金额"><el-input-number v-model="form.amount" :min="0" :precision="2" style="width:100%"/></el-form-item>
        <el-form-item label="币种"><el-input v-model="form.currency"/></el-form-item>
        <el-form-item label="备注"><el-input v-model="form.remark" type="textarea"/></el-form-item>
      </el-form>
      <template #footer><el-button @click="dialogVisible=false">取消</el-button><el-button type="primary" @click="save">保存</el-button></template>
    </el-dialog>

    <el-dialog v-model="detailVisible" title="审批详情" width="760px">
      <el-descriptions :column="2" border v-if="detail.request"><el-descriptions-item label="审批号">{{ detail.request.no }}</el-descriptions-item><el-descriptions-item label="状态">{{ statusName(detail.request.status) }}</el-descriptions-item><el-descriptions-item label="标题">{{ detail.request.title }}</el-descriptions-item><el-descriptions-item label="金额">{{ detail.request.amount }} {{ detail.request.currency }}</el-descriptions-item></el-descriptions>
      <el-table :data="detail.steps || []" border stripe style="margin-top:12px"><el-table-column prop="stepNo" label="步骤" width="80"/><el-table-column prop="stepName" label="节点" width="160"/><el-table-column prop="approverId" label="审批人" width="100"/><el-table-column prop="status" label="状态" width="110"/><el-table-column prop="action" label="动作" width="100"/><el-table-column prop="comment" label="意见"/><el-table-column prop="actionAt" label="时间" width="170"/></el-table>
    </el-dialog>
  </div>
</template>
<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { http } from '../api/http'
const rows=ref<any[]>([]), status=ref<string|null>(null), targetType=ref<string|null>(null), dialogVisible=ref(false), detailVisible=ref(false)
const form=reactive<any>({approvalType:'purchase',targetType:'PO',targetId:0,title:'',amount:0,currency:'RMB',remark:''})
const detail=ref<any>({})
function statusName(s:string){return ({draft:'草稿',submitted:'已提交',approving:'审批中',approved:'已通过',rejected:'已驳回',returned:'已退回'} as any)[s]||s}
function statusType(s:string){return s==='approved'?'success':s==='rejected'?'danger':s==='returned'?'warning':s==='draft'?'info':'primary'}
async function load(){const params:any={}; if(status.value)params.status=status.value; if(targetType.value)params.targetType=targetType.value; rows.value=(await http.get('/approvals',{params})).data}
function reset(){Object.assign(form,{approvalType:'purchase',targetType:'PO',targetId:0,title:'',amount:0,currency:'RMB',remark:''})}
function openCreate(){reset();dialogVisible.value=true}
async function save(){if(!form.title)return ElMessage.warning('请输入标题'); await http.post('/approvals',form); dialogVisible.value=false; ElMessage.success('保存成功'); await load()}
async function view(row:any){detail.value=(await http.get(`/approvals/${row.id}`)).data; detailVisible.value=true}
async function submit(id:number){await http.post(`/approvals/${id}/submit`,{}); ElMessage.success('已提交'); await load()}
async function approve(id:number){await http.post(`/approvals/${id}/approve`,{}); ElMessage.success('已通过当前节点'); await load()}
async function reject(id:number){await ElMessageBox.confirm('确认驳回？','提示'); await http.post(`/approvals/${id}/reject`,{}); ElMessage.success('已驳回'); await load()}
async function returnBack(id:number){await http.post(`/approvals/${id}/return`,{}); ElMessage.success('已退回'); await load()}
onMounted(load)
</script>
<style scoped>.toolbar{display:flex;gap:8px;align-items:center}.card{margin-bottom:14px}</style>
