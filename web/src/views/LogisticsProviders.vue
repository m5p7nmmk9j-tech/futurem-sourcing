<template>
  <div class="page">
    <div class="page-header">
      <div><div class="page-title">物流服务商</div><div class="page-subtitle">独立维护货代、报关、拖车、仓储、快递和其他物流服务商。</div></div>
      <el-button type="primary" @click="openCreate">新增物流服务商</el-button>
    </div>
    <div class="card">
      <div class="toolbar">
        <el-input v-model="keyword" clearable placeholder="编码或名称" style="width:260px" @keyup.enter="load"/>
        <el-select v-model="serviceType" clearable placeholder="服务类型" style="width:180px"><el-option v-for="item in serviceTypes" :key="item.value" :label="item.label" :value="item.value"/></el-select>
        <el-button type="primary" @click="load">查询</el-button>
      </div>
      <el-table :data="rows" border stripe>
        <el-table-column prop="code" label="编码" width="130"/>
        <el-table-column prop="name" label="名称" min-width="180"/>
        <el-table-column label="服务类型" min-width="260"><template #default="s"><el-tag v-for="value in parseTypes(s.row.serviceTypesJson)" :key="value" style="margin-right:6px">{{typeLabel(value)}}</el-tag></template></el-table-column>
        <el-table-column prop="contactName" label="联系人" width="120"/>
        <el-table-column prop="phone" label="电话" width="150"/>
        <el-table-column prop="email" label="邮箱" min-width="180"/>
        <el-table-column label="状态" width="90"><template #default="s"><el-tag :type="s.row.status==='active'?'success':'info'">{{s.row.status==='active'?'启用':'停用'}}</el-tag></template></el-table-column>
        <el-table-column label="操作" width="100"><template #default="s"><el-button size="small" @click="openEdit(s.row)">编辑</el-button></template></el-table-column>
      </el-table>
    </div>

    <el-dialog v-model="visible" :title="form.id?'编辑物流服务商':'新增物流服务商'" width="700px">
      <el-form label-width="110px">
        <el-row :gutter="16">
          <el-col :span="12"><el-form-item label="编码"><el-input v-model="form.code"/></el-form-item></el-col>
          <el-col :span="12"><el-form-item label="名称"><el-input v-model="form.name"/></el-form-item></el-col>
          <el-col :span="24"><el-form-item label="服务类型"><el-checkbox-group v-model="form.serviceTypes"><el-checkbox v-for="item in serviceTypes" :key="item.value" :label="item.value">{{item.label}}</el-checkbox></el-checkbox-group></el-form-item></el-col>
          <el-col :span="12"><el-form-item label="联系人"><el-input v-model="form.contactName"/></el-form-item></el-col>
          <el-col :span="12"><el-form-item label="电话"><el-input v-model="form.phone"/></el-form-item></el-col>
          <el-col :span="12"><el-form-item label="邮箱"><el-input v-model="form.email"/></el-form-item></el-col>
          <el-col :span="12"><el-form-item label="税号"><el-input v-model="form.taxId"/></el-form-item></el-col>
          <el-col :span="24"><el-form-item label="地址"><el-input v-model="form.address" type="textarea"/></el-form-item></el-col>
          <el-col :span="12"><el-form-item label="状态"><el-select v-model="form.status" style="width:100%"><el-option label="启用" value="active"/><el-option label="停用" value="inactive"/></el-select></el-form-item></el-col>
          <el-col :span="24"><el-form-item label="备注"><el-input v-model="form.remark" type="textarea"/></el-form-item></el-col>
        </el-row>
      </el-form>
      <template #footer><el-button @click="visible=false">取消</el-button><el-button type="primary" @click="save">保存</el-button></template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { http } from '../api/http'

const rows=ref<any[]>([]),visible=ref(false),keyword=ref(''),serviceType=ref('')
const serviceTypes=[{label:'国际货代',value:'ocean_freight'},{label:'报关行',value:'customs'},{label:'拖车公司',value:'trucking'},{label:'仓库服务',value:'warehouse'},{label:'快递公司',value:'courier'},{label:'其他服务',value:'other_service'}]
const form=reactive<any>({})
function reset(){Object.assign(form,{id:0,code:'',name:'',serviceTypes:[],contactName:'',phone:'',email:'',address:'',taxId:'',status:'active',remark:''})}
async function load(){rows.value=(await http.get('/logistics-providers',{params:{keyword:keyword.value||undefined,serviceType:serviceType.value||undefined}})).data||[]}
function openCreate(){reset();visible.value=true}
function openEdit(row:any){Object.assign(form,{...row,serviceTypes:parseTypes(row.serviceTypesJson)});visible.value=true}
async function save(){if(!form.code.trim()||!form.name.trim())return ElMessage.warning('请输入编码和名称');const payload={...form,serviceTypesJson:JSON.stringify(form.serviceTypes||[])};if(form.id)await http.put(`/logistics-providers/${form.id}`,payload);else await http.post('/logistics-providers',payload);ElMessage.success('物流服务商已保存');visible.value=false;await load()}
function parseTypes(value:string){try{return JSON.parse(value||'[]')}catch{return []}}
function typeLabel(value:string){return serviceTypes.find(x=>x.value===value)?.label||value}
onMounted(load)
</script>
<style scoped>.page-subtitle{margin-top:4px;color:#64748b;font-size:13px}.toolbar{display:flex;gap:10px;margin-bottom:14px}</style>
