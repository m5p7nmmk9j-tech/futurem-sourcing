<template>
  <div class="page">
    <div class="page-header"><div class="page-title">商品管理</div><el-button type="primary" @click="openCreate">新增商品</el-button></div>
    <div class="card">
      <div class="toolbar"><el-input v-model="keyword" placeholder="搜索 SKU / 条码 / 名称 / 客户货号" clearable style="width:380px" @keyup.enter="load"/><el-button @click="load">查询</el-button></div>
      <el-table :data="rows" border stripe>
        <el-table-column label="图片" width="90"><template #default="scope"><el-image v-if="scope.row.imageUrl" :src="scope.row.imageUrl" fit="cover" class="product-thumb"/></template></el-table-column>
        <el-table-column prop="sku" label="SKU" width="170"/><el-table-column prop="barcode" label="条码" width="170"/><el-table-column prop="nameCn" label="中文名" min-width="180"/><el-table-column prop="unit" label="单位" width="80"/><el-table-column prop="purchasePrice" label="采购价" width="100"/><el-table-column prop="cartonQty" label="单箱数量" width="110"/>
        <el-table-column label="尺寸cm" width="150"><template #default="scope">{{scope.row.cartonLengthCm||0}}×{{scope.row.cartonWidthCm||0}}×{{scope.row.cartonHeightCm||0}}</template></el-table-column>
        <el-table-column label="单箱CBM" width="110"><template #default="scope">{{cartonCbm(scope.row)}}</template></el-table-column>
        <el-table-column label="重量KG" width="120"><template #default="scope">GW {{scope.row.cartonGwKg||0}} / NW {{scope.row.cartonNwKg||0}}</template></el-table-column>
        <el-table-column prop="customerItemNo" label="客户货号" width="150"/>
        <el-table-column label="操作" width="180"><template #default="scope"><el-button size="small" @click="openEdit(scope.row)">编辑</el-button><el-button size="small" type="danger" @click="remove(scope.row.id)">删除</el-button></template></el-table-column>
      </el-table>
    </div>
    <el-dialog v-model="dialogVisible" :title="form.id?'编辑商品':'新增商品'" width="760px">
      <el-alert title="SKU 创建后不允许修改；商品条码可以修改，但必须保持全系统唯一。金额、立方数、毛重和净重均保留两位小数。" type="info" show-icon style="margin-bottom:12px"/>
      <el-form label-width="100px">
        <el-row :gutter="12">
          <el-col :span="12"><el-form-item label="SKU"><el-input v-model="form.sku" :disabled="!!form.id" placeholder="留空自动生成"/></el-form-item></el-col>
          <el-col :span="12"><el-form-item label="商品条码"><el-input v-model="form.barcode" placeholder="留空自动生成" maxlength="80" show-word-limit/></el-form-item></el-col>
          <el-col :span="12"><el-form-item label="中文名"><el-input v-model="form.nameCn"/></el-form-item></el-col>
          <el-col :span="12"><el-form-item label="英文名"><el-input v-model="form.nameEn"/></el-form-item></el-col>
          <el-col :span="12"><el-form-item label="西语名"><el-input v-model="form.nameEs"/></el-form-item></el-col>
          <el-col :span="12"><el-form-item label="品牌"><el-input v-model="form.brand"/></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="单位"><el-input v-model="form.unit"/></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="采购价"><el-input-number v-model="form.purchasePrice" :min="0" :precision="2" style="width:100%"/></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="单箱数量"><el-input-number v-model="form.cartonQty" :min="0" :precision="2" style="width:100%"/></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="长cm"><el-input-number v-model="form.cartonLengthCm" :min="0" :precision="2" style="width:100%"/></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="宽cm"><el-input-number v-model="form.cartonWidthCm" :min="0" :precision="2" style="width:100%"/></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="高cm"><el-input-number v-model="form.cartonHeightCm" :min="0" :precision="2" style="width:100%"/></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="单箱CBM"><el-input :model-value="cartonCbm(form)" disabled/></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="单箱GW"><el-input-number v-model="form.cartonGwKg" :min="0" :precision="2" style="width:100%"/></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="单箱NW"><el-input-number v-model="form.cartonNwKg" :min="0" :precision="2" style="width:100%"/></el-form-item></el-col>
          <el-col :span="12"><el-form-item label="客户货号"><el-input v-model="form.customerItemNo"/></el-form-item></el-col>
          <el-col :span="12"><el-form-item label="图片URL"><el-input v-model="form.imageUrl"/></el-form-item></el-col>
          <el-col :span="24"><el-form-item label="添加图片"><input type="file" accept="image/*" @change="selectImage"/></el-form-item></el-col>
          <el-col :span="24"><el-image v-if="form.imageUrl" :src="form.imageUrl" fit="contain" class="image-preview"/></el-col>
          <el-col :span="24"><el-form-item label="备注"><el-input v-model="form.remark" type="textarea"/></el-form-item></el-col>
        </el-row>
      </el-form>
      <template #footer><el-button @click="dialogVisible=false">取消</el-button><el-button type="primary" @click="save">保存</el-button></template>
    </el-dialog>
  </div>
</template>
<script setup lang="ts">
import {onMounted,reactive,ref} from 'vue'
import {ElMessage,ElMessageBox} from 'element-plus'
import {http} from '../api/http'
const rows=ref<any[]>([]),keyword=ref(''),dialogVisible=ref(false)
const defaults={id:0,sku:'',barcode:'',nameCn:'',nameEn:'',nameEs:'',brand:'',unit:'PCS',purchasePrice:0,cartonQty:0,cartonLengthCm:0,cartonWidthCm:0,cartonHeightCm:0,cartonGwKg:0,cartonNwKg:0,customerItemNo:'',imageUrl:'',remark:''}
const form=reactive<any>({...defaults})
const err=(e:any)=>e?.response?.data?.message||e?.response?.data||e?.message||'操作失败'
async function load(){try{rows.value=(await http.get('/products',{params:{keyword:keyword.value}})).data}catch(e:any){ElMessage.error(err(e))}}
function reset(){Object.assign(form,defaults)}function openCreate(){reset();dialogVisible.value=true}function openEdit(row:any){Object.assign(form,row);dialogVisible.value=true}
function cartonCbm(row:any){return Number(((Number(row.cartonLengthCm||0)*Number(row.cartonWidthCm||0)*Number(row.cartonHeightCm||0))/1000000).toFixed(2))}
function selectImage(event:Event){const file=(event.target as HTMLInputElement).files?.[0];if(!file)return;const reader=new FileReader();reader.onload=()=>{form.imageUrl=String(reader.result||'')};reader.readAsDataURL(file)}
async function save(){try{if(!form.nameCn)return ElMessage.warning('请输入中文名');if(form.id&&!String(form.barcode||'').trim())return ElMessage.warning('请输入商品条码');form.id?await http.put(`/products/${form.id}`,form):await http.post('/products',form);dialogVisible.value=false;ElMessage.success('保存成功');await load()}catch(e:any){ElMessage.error(err(e))}}
async function remove(id:number){try{await ElMessageBox.confirm('确认删除该商品？','提示');await http.delete(`/products/${id}`);ElMessage.success('已删除');await load()}catch(e:any){if(e!=='cancel'&&e!=='close')ElMessage.error(err(e))}}
onMounted(load)
</script>
<style scoped>.product-thumb{width:52px;height:52px;border-radius:8px;background:#f3f4f6}.image-preview{width:160px;height:120px;margin:0 0 14px 100px;border:1px solid #e5e7eb;border-radius:10px;background:#f8fafc}</style>
