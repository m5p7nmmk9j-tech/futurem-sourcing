<template>
  <div class="page">
    <div class="page-header"><div class="page-title">商品管理</div><el-button type="primary" @click="openCreate">新增商品</el-button></div>
    <div class="card">
      <div class="toolbar"><el-input v-model="keyword" placeholder="搜索 SKU / 条码 / 名称 / 客户货号" clearable style="width:380px" @keyup.enter="load"/><el-button @click="load">查询</el-button></div>
      <el-table :data="rows" border stripe>
        <el-table-column prop="sku" label="SKU" width="170"/><el-table-column prop="barcode" label="条码" width="170"/><el-table-column prop="nameCn" label="中文名" min-width="180"/><el-table-column prop="nameEn" label="英文名" min-width="180"/><el-table-column prop="unit" label="单位" width="80"/><el-table-column prop="customerItemNo" label="客户货号" width="150"/>
        <el-table-column label="操作" width="180"><template #default="scope"><el-button size="small" @click="openEdit(scope.row)">编辑</el-button><el-button size="small" type="danger" @click="remove(scope.row.id)">删除</el-button></template></el-table-column>
      </el-table>
    </div>
    <el-dialog v-model="dialogVisible" :title="form.id ? '编辑商品' : '新增商品'" width="620px">
      <el-alert title="商品资料只保存固定信息，不保存采购价、销售价、包装、CBM、KG。" type="info" show-icon style="margin-bottom:12px" />
      <el-form label-width="100px">
        <el-form-item label="中文名"><el-input v-model="form.nameCn"/></el-form-item><el-form-item label="英文名"><el-input v-model="form.nameEn"/></el-form-item><el-form-item label="西语名"><el-input v-model="form.nameEs"/></el-form-item><el-form-item label="品牌"><el-input v-model="form.brand"/></el-form-item><el-form-item label="单位"><el-input v-model="form.unit"/></el-form-item><el-form-item label="客户货号"><el-input v-model="form.customerItemNo"/></el-form-item><el-form-item label="图片URL"><el-input v-model="form.imageUrl"/></el-form-item><el-form-item label="备注"><el-input v-model="form.remark" type="textarea"/></el-form-item>
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
const form=reactive<any>({id:0,nameCn:'',nameEn:'',nameEs:'',brand:'',unit:'PCS',customerItemNo:'',imageUrl:'',remark:''})
async function load(){ const res=await http.get('/products',{params:{keyword:keyword.value}}); rows.value=res.data }
function reset(){ Object.assign(form,{id:0,nameCn:'',nameEn:'',nameEs:'',brand:'',unit:'PCS',customerItemNo:'',imageUrl:'',remark:''}) }
function openCreate(){ reset(); dialogVisible.value=true }
function openEdit(row:any){ Object.assign(form,row); dialogVisible.value=true }
async function save(){ if(!form.nameCn) return ElMessage.warning('请输入中文名'); form.id ? await http.put(`/products/${form.id}`,form) : await http.post('/products',form); dialogVisible.value=false; ElMessage.success('保存成功'); await load() }
async function remove(id:number){ await ElMessageBox.confirm('确认删除该商品？','提示'); await http.delete(`/products/${id}`); ElMessage.success('已删除'); await load() }
onMounted(load)
</script>
