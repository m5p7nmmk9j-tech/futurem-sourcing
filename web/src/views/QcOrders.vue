<template>
  <div class="page">
    <div class="page-header">
      <div><div class="page-title">验货单</div><div class="sub">一张收货单对应一张验货单，确认最终接受数量后生成供应商应付。</div></div>
      <el-button type="primary" @click="openCreate">新增验货单</el-button>
    </div>
    <div class="card">
      <div class="toolbar">
        <el-select v-model="filterReceiving" placeholder="按收货单筛选" clearable filterable style="width:260px" @change="load">
          <el-option v-for="r in receivings" :key="r.id" :label="r.no" :value="r.id" />
        </el-select>
        <el-select v-model="filterStatus" placeholder="按状态筛选" clearable style="width:160px" @change="load">
          <el-option label="草稿" value="draft"/><el-option label="已确认" value="confirmed"/><el-option label="已解锁" value="unlocked"/>
        </el-select>
        <el-button @click="load">刷新</el-button>
      </div>
      <el-table :data="rows" border stripe>
        <el-table-column label="验货单号" width="180"><template #default="s"><el-button link type="primary" @click="open(s.row)">{{ s.row.no }}</el-button></template></el-table-column>
        <el-table-column label="收货单" min-width="160"><template #default="s">{{ receivingName(s.row.receivingOrderId) }}</template></el-table-column>
        <el-table-column prop="purchaseOrderId" label="PO ID" width="90"/>
        <el-table-column label="验货日期" width="120"><template #default="s">{{ dateText(s.row.qcDate) }}</template></el-table-column>
        <el-table-column label="结果" width="120"><template #default="s">{{ resultLabel(s.row.result) }}</template></el-table-column>
        <el-table-column label="版本" width="70"><template #default="s">V{{ s.row.confirmationVersion || 0 }}</template></el-table-column>
        <el-table-column label="状态" width="100"><template #default="s"><el-tag :type="tagType(s.row.status)">{{ statusLabel(s.row.status) }}</el-tag></template></el-table-column>
        <el-table-column prop="remark" label="备注" min-width="180"/>
        <el-table-column label="操作" width="160" fixed="right"><template #default="s">
          <el-button size="small" @click="open(s.row)">{{ s.row.status === 'confirmed' ? '查看' : '验货' }}</el-button>
          <el-button v-if="s.row.status === 'draft'" size="small" type="danger" @click="remove(s.row.id)">删除</el-button>
        </template></el-table-column>
      </el-table>
    </div>

    <el-dialog v-model="visible" :title="form.id ? `验货单：${form.no}` : '新增验货单'" width="96%" destroy-on-close>
      <el-alert title="不合格、退回和待处理数量保留记录；应付只按最终接受数量 × PO采购单价生成。" type="info" show-icon style="margin-bottom:14px"/>
      <el-form label-width="100px">
        <el-row :gutter="16">
          <el-col :span="8"><el-form-item label="收货单"><el-select v-model="form.receivingOrderId" filterable :disabled="!!form.id" style="width:100%"><el-option v-for="r in receivings" :key="r.id" :label="r.no" :value="r.id"/></el-select></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="验货日期"><el-date-picker v-model="form.qcDate" type="date" value-format="YYYY-MM-DD" :disabled="form.status !== 'draft'" style="width:100%"/></el-form-item></el-col>
          <el-col :span="4"><el-form-item label="状态"><el-input :model-value="statusLabel(form.status)" disabled/></el-form-item></el-col>
          <el-col :span="4"><el-form-item label="版本"><el-input :model-value="`V${form.confirmationVersion || 0}`" disabled/></el-form-item></el-col>
          <el-col :span="24"><el-form-item label="备注"><el-input v-model="form.remark" type="textarea" :disabled="form.status !== 'draft'"/></el-form-item></el-col>
        </el-row>
      </el-form>
      <QcResultEditor v-if="form.id" :qc-id="form.id" :status="form.status" @changed="refresh"/>
      <template #footer>
        <el-button @click="visible=false">关闭</el-button>
        <el-button v-if="form.id && form.status==='draft'" @click="saveHeader">保存主单</el-button>
        <el-button v-if="!form.id" type="primary" @click="create">创建验货单</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { http } from '../api/http'
import QcResultEditor from '../components/QcResultEditor.vue'
const rows=ref<any[]>([]), receivings=ref<any[]>([]), filterReceiving=ref<number|null>(null), filterStatus=ref(''), visible=ref(false)
const form=reactive<any>({})
function reset(){Object.assign(form,{id:0,no:'',receivingOrderId:null,qcDate:new Date().toISOString().slice(0,10),status:'draft',result:'pending',confirmationVersion:0,remark:''})}
async function loadReceivings(){receivings.value=(await http.get('/receiving-orders')).data||[]}
async function load(){const p:any={};if(filterReceiving.value)p.receivingOrderId=filterReceiving.value;if(filterStatus.value)p.status=filterStatus.value;rows.value=(await http.get('/qc-orders',{params:p})).data||[]}
function openCreate(){reset();visible.value=true}
async function open(row:any){const r=await http.get(`/qc-orders/${row.id}`);Object.assign(form,r.data.qcOrder);visible.value=true}
async function refresh(){if(!form.id)return;const r=await http.get(`/qc-orders/${form.id}`);Object.assign(form,r.data.qcOrder);await Promise.all([load(),loadReceivings()])}
async function create(){if(!form.receivingOrderId)return ElMessage.warning('请选择收货单');const r=await http.post('/qc-orders',{receivingOrderId:form.receivingOrderId});Object.assign(form,r.data);ElMessage.success('验货单已创建');await load()}
async function saveHeader(){await http.put(`/qc-orders/${form.id}`,{receivingOrderId:form.receivingOrderId,qcDate:form.qcDate,remark:form.remark});ElMessage.success('已保存');await refresh()}
async function remove(id:number){await ElMessageBox.confirm('确认删除草稿验货单？','删除');await http.delete(`/qc-orders/${id}`);ElMessage.success('已删除');await load()}
function receivingName(id:number){return receivings.value.find(r=>r.id===id)?.no||`收货单 ${id}`}
function statusLabel(v:string){return ({draft:'草稿',confirmed:'已确认',unlocked:'已解锁'} as any)[v]||v}
function resultLabel(v:string){return ({pending:'待验货',accepted_all:'全部接受',accepted_partial:'部分接受'} as any)[v]||v}
function tagType(v:string){return v==='confirmed'?'success':v==='unlocked'?'warning':''}
function dateText(v:string){return v?String(v).slice(0,10):''}
onMounted(async()=>{reset();await Promise.all([loadReceivings(),load()])})
</script>
<style scoped>.sub{margin-top:4px;color:#64748b;font-size:13px}</style>
