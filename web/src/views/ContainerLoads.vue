<template>
  <div class="page">
    <div class="page-header"><div class="page-title">Container Load 装柜</div><el-button type="primary" @click="openCreate">新增装柜单</el-button></div>
    <div class="card">
      <div class="toolbar"><el-select v-model="summaryOrderId" placeholder="按SO筛选" clearable filterable style="width:300px" @change="load"><el-option v-for="s in summaryOrders" :key="s.id" :label="s.no" :value="s.id" /></el-select><el-button @click="load">刷新</el-button></div>
      <el-table :data="rows" border stripe>
        <el-table-column prop="no" label="装柜单号" width="190"/><el-table-column prop="summaryOrderId" label="SO ID" width="100"/><el-table-column prop="containerType" label="柜型" width="120"/><el-table-column prop="containerNo" label="柜号" width="160"/><el-table-column prop="sealNo" label="封条号" width="160"/><el-table-column prop="loadDate" label="装柜日期" width="150"/><el-table-column prop="totalCartons" label="箱数" width="100"/><el-table-column prop="totalCbm" label="CBM" width="100"/><el-table-column prop="totalGwKg" label="KG" width="100"/><el-table-column prop="status" label="状态" width="110"/>
        <el-table-column label="操作" width="260" fixed="right"><template #default="scope"><el-button size="small" type="success" @click="selectRow(scope.row)">明细</el-button><el-button size="small" @click="openEdit(scope.row)">编辑</el-button><el-button size="small" type="danger" @click="remove(scope.row.id)">删除</el-button></template></el-table-column>
      </el-table>
      <DocumentLinesEditor v-if="selectedId" document-type="CL" :document-id="selectedId" />
    </div>
    <el-dialog v-model="dialogVisible" :title="form.id ? '编辑装柜单' : '新增装柜单'" width="660px">
      <el-alert title="装柜单用于拼柜/整柜管理，商品装柜箱数、CBM、重量在明细里填写。" type="info" show-icon style="margin-bottom:12px" />
      <el-form label-width="110px"><el-form-item label="SO 汇总单"><el-select v-model="form.summaryOrderId" filterable clearable style="width:100%"><el-option v-for="s in summaryOrders" :key="s.id" :label="s.no" :value="s.id" /></el-select></el-form-item><el-form-item label="柜型"><el-select v-model="form.containerType" style="width:100%"><el-option label="20GP" value="20GP"/><el-option label="40GP" value="40GP"/><el-option label="40HQ" value="40HQ"/><el-option label="45HQ" value="45HQ"/><el-option label="28FT" value="28FT"/><el-option label="58FT" value="58FT"/><el-option label="68FT" value="68FT"/></el-select></el-form-item><el-form-item label="柜号"><el-input v-model="form.containerNo" /></el-form-item><el-form-item label="封条号"><el-input v-model="form.sealNo" /></el-form-item><el-form-item label="装柜日期"><el-date-picker v-model="form.loadDate" type="date" value-format="YYYY-MM-DD" style="width:100%" /></el-form-item><el-form-item label="总箱数"><el-input-number v-model="form.totalCartons" :min="0" style="width:100%" /></el-form-item><el-form-item label="总CBM"><el-input-number v-model="form.totalCbm" :min="0" :precision="3" style="width:100%" /></el-form-item><el-form-item label="总KG"><el-input-number v-model="form.totalGwKg" :min="0" :precision="2" style="width:100%" /></el-form-item><el-form-item label="状态"><el-input v-model="form.status" /></el-form-item><el-form-item label="备注"><el-input v-model="form.remark" type="textarea" /></el-form-item></el-form>
      <template #footer><el-button @click="dialogVisible=false">取消</el-button><el-button type="primary" @click="save">保存</el-button></template>
    </el-dialog>
  </div>
</template>
<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { http } from '../api/http'
import DocumentLinesEditor from '../components/DocumentLinesEditor.vue'
const rows=ref<any[]>([]), summaryOrders=ref<any[]>([]), summaryOrderId=ref<number|null>(null), dialogVisible=ref(false), selectedId=ref<number|null>(null)
const form=reactive<any>({id:0,summaryOrderId:null,containerType:'40HQ',containerNo:'',sealNo:'',loadDate:'',status:'draft',totalCartons:0,totalCbm:0,totalGwKg:0,remark:''})
async function loadSummaryOrders(){summaryOrders.value=(await http.get('/summary-orders')).data}
async function load(){const params:any={}; if(summaryOrderId.value)params.summaryOrderId=summaryOrderId.value; rows.value=(await http.get('/container-loads',{params})).data; if(!selectedId.value&&rows.value.length)selectedId.value=rows.value[0].id}
function reset(){Object.assign(form,{id:0,summaryOrderId:null,containerType:'40HQ',containerNo:'',sealNo:'',loadDate:'',status:'draft',totalCartons:0,totalCbm:0,totalGwKg:0,remark:''})}
function openCreate(){reset();dialogVisible.value=true} function openEdit(row:any){Object.assign(form,row);dialogVisible.value=true} function selectRow(row:any){selectedId.value=row.id}
async function save(){if(!form.containerType)return ElMessage.warning('请选择柜型'); const res=form.id?await http.put(`/container-loads/${form.id}`,form):await http.post('/container-loads',form); dialogVisible.value=false; ElMessage.success('保存成功'); await load(); selectedId.value=res.data?.id||form.id||selectedId.value}
async function remove(id:number){await ElMessageBox.confirm('确认删除该装柜单？','提示'); await http.delete(`/container-loads/${id}`); if(selectedId.value===id)selectedId.value=null; ElMessage.success('已删除'); await load()}
onMounted(async()=>{await loadSummaryOrders();await load()})
</script>
