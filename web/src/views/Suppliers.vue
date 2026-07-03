<template>
  <div class="page">
    <div class="page-header"><div class="page-title">供应商管理</div><el-button type="primary" @click="openCreate">新增供应商</el-button></div>
    <div class="card">
      <div class="toolbar"><el-input v-model="keyword" placeholder="搜索供应商 / 店面 / WhatsApp" clearable style="width:360px" @keyup.enter="load"/><el-button @click="load">查询</el-button></div>
      <el-table :data="rows" border stripe>
        <el-table-column prop="code" label="编号" width="170"/><el-table-column prop="name" label="供应商" min-width="180"/><el-table-column prop="shopNo" label="店面号" width="120"/><el-table-column prop="contactName" label="联系人" width="120"/><el-table-column prop="phone" label="电话" width="140"/><el-table-column prop="whatsapp" label="WhatsApp" width="150"/>
        <el-table-column label="操作" width="180"><template #default="scope"><el-button size="small" @click="openEdit(scope.row)">编辑</el-button><el-button size="small" type="danger" @click="remove(scope.row.id)">删除</el-button></template></el-table-column>
      </el-table>
    </div>
    <el-dialog v-model="dialogVisible" :title="form.id ? '编辑供应商' : '新增供应商'" width="620px">
      <el-form label-width="100px">
        <el-form-item label="供应商名称"><el-input v-model="form.name"/></el-form-item><el-form-item label="市场ID"><el-input-number v-model="form.marketId" :min="0"/></el-form-item><el-form-item label="店面号"><el-input v-model="form.shopNo"/></el-form-item><el-form-item label="楼层"><el-input v-model="form.floorNo"/></el-form-item><el-form-item label="主营产品"><el-input v-model="form.mainProducts"/></el-form-item><el-form-item label="联系人"><el-input v-model="form.contactName"/></el-form-item><el-form-item label="电话"><el-input v-model="form.phone"/></el-form-item><el-form-item label="微信"><el-input v-model="form.wechat"/></el-form-item><el-form-item label="WhatsApp"><el-input v-model="form.whatsapp"/></el-form-item><el-form-item label="备注"><el-input v-model="form.remark" type="textarea"/></el-form-item>
      </el-form>
      <template #footer><el-button @click="dialogVisible=false">取消</el-button><el-button type="primary" @click="save">保存</el-button></template>
    </el-dialog>
  </div>
</template>
<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { http } from '../api/http'
const rows=ref<any[]>([]); const keyword=ref(''); const dialogVisible=ref(false)
const form=reactive<any>({id:0,name:'',marketId:null,shopNo:'',floorNo:'',mainProducts:'',contactName:'',phone:'',wechat:'',whatsapp:'',remark:''})
async function load(){ const res=await http.get('/suppliers',{params:{keyword:keyword.value}}); rows.value=res.data }
function reset(){ Object.assign(form,{id:0,name:'',marketId:null,shopNo:'',floorNo:'',mainProducts:'',contactName:'',phone:'',wechat:'',whatsapp:'',remark:''}) }
function openCreate(){ reset(); dialogVisible.value=true }
function openEdit(row:any){ Object.assign(form,row); dialogVisible.value=true }
async function save(){ if(!form.name) return ElMessage.warning('请输入供应商名称'); form.id ? await http.put(`/suppliers/${form.id}`,form) : await http.post('/suppliers',form); dialogVisible.value=false; ElMessage.success('保存成功'); await load() }
async function remove(id:number){ await ElMessageBox.confirm('确认删除该供应商？','提示'); await http.delete(`/suppliers/${id}`); ElMessage.success('已删除'); await load() }
onMounted(load)
</script>
