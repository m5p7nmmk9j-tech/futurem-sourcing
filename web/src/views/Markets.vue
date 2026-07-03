<template>
  <div class="page">
    <div class="page-header"><div class="page-title">市场管理</div><el-button type="primary" @click="openCreate">新增市场</el-button></div>
    <div class="card">
      <div class="toolbar"><el-input v-model="keyword" placeholder="搜索市场" clearable style="width:320px" @keyup.enter="load"/><el-button @click="load">查询</el-button></div>
      <el-table :data="rows" border stripe>
        <el-table-column prop="code" label="编号" width="160"/><el-table-column prop="name" label="市场名称" min-width="220"/><el-table-column prop="city" label="城市" width="120"/><el-table-column prop="address" label="地址" min-width="240"/>
        <el-table-column label="操作" width="180"><template #default="scope"><el-button size="small" @click="openEdit(scope.row)">编辑</el-button><el-button size="small" type="danger" @click="remove(scope.row.id)">删除</el-button></template></el-table-column>
      </el-table>
    </div>
    <el-dialog v-model="dialogVisible" :title="form.id ? '编辑市场' : '新增市场'" width="520px">
      <el-form label-width="90px"><el-form-item label="市场名称"><el-input v-model="form.name"/></el-form-item><el-form-item label="城市"><el-input v-model="form.city"/></el-form-item><el-form-item label="地址"><el-input v-model="form.address"/></el-form-item><el-form-item label="备注"><el-input v-model="form.remark" type="textarea"/></el-form-item></el-form>
      <template #footer><el-button @click="dialogVisible=false">取消</el-button><el-button type="primary" @click="save">保存</el-button></template>
    </el-dialog>
  </div>
</template>
<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { http } from '../api/http'
const rows = ref<any[]>([]); const keyword = ref(''); const dialogVisible = ref(false)
const form = reactive<any>({ id: 0, name: '', city: '义乌', address: '', remark: '' })
async function load(){ const res = await http.get('/markets',{params:{keyword:keyword.value}}); rows.value=res.data }
function reset(){ Object.assign(form,{ id:0,name:'',city:'义乌',address:'',remark:''}) }
function openCreate(){ reset(); dialogVisible.value=true }
function openEdit(row:any){ Object.assign(form,row); dialogVisible.value=true }
async function save(){ if(!form.name) return ElMessage.warning('请输入市场名称'); form.id ? await http.put(`/markets/${form.id}`,form) : await http.post('/markets',form); dialogVisible.value=false; ElMessage.success('保存成功'); await load() }
async function remove(id:number){ await ElMessageBox.confirm('确认删除该市场？','提示'); await http.delete(`/markets/${id}`); ElMessage.success('已删除'); await load() }
onMounted(load)
</script>
