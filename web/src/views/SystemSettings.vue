<template>
  <div class="page">
    <div class="page-header">
      <div class="page-title">系统参数 System Settings</div>
      <div class="toolbar"><el-button type="primary" @click="seed">初始化默认参数</el-button><el-button @click="load">刷新</el-button></div>
    </div>

    <div class="card">
      <div class="toolbar">
        <el-select v-model="group" clearable placeholder="参数分组" style="width:220px" @change="loadSettings">
          <el-option v-for="g in groups" :key="g.code" :label="`${g.name} / ${g.code}`" :value="g.code" />
        </el-select>
        <el-button type="primary" @click="openCreate">新增参数</el-button>
      </div>
      <el-table :data="rows" border stripe>
        <el-table-column prop="settingGroup" label="分组" width="130" />
        <el-table-column prop="settingKey" label="参数Key" width="220" />
        <el-table-column prop="settingValue" label="参数值" min-width="260" show-overflow-tooltip />
        <el-table-column prop="valueType" label="类型" width="100" />
        <el-table-column prop="isSystem" label="系统" width="80"><template #default="s">{{ s.row.isSystem ? '是' : '否' }}</template></el-table-column>
        <el-table-column prop="remark" label="说明" min-width="200" />
        <el-table-column label="操作" width="180" fixed="right"><template #default="s"><el-button size="small" @click="openEdit(s.row)">编辑</el-button><el-button size="small" type="danger" @click="remove(s.row.id)">删除</el-button></template></el-table-column>
      </el-table>
    </div>

    <el-dialog v-model="dialogVisible" :title="form.id ? '编辑参数' : '新增参数'" width="680px">
      <el-form label-width="100px">
        <el-form-item label="分组"><el-select v-model="form.settingGroup" style="width:100%"><el-option v-for="g in groups" :key="g.code" :label="`${g.name} / ${g.code}`" :value="g.code" /></el-select></el-form-item>
        <el-form-item label="Key"><el-input v-model="form.settingKey" /></el-form-item>
        <el-form-item label="Value"><el-input v-model="form.settingValue" type="textarea" :rows="4" /></el-form-item>
        <el-form-item label="类型"><el-select v-model="form.valueType" style="width:100%"><el-option label="文本" value="text"/><el-option label="数字" value="number"/><el-option label="布尔" value="bool"/><el-option label="JSON" value="json"/></el-select></el-form-item>
        <el-form-item label="系统参数"><el-switch v-model="form.isSystem" /></el-form-item>
        <el-form-item label="说明"><el-input v-model="form.remark" type="textarea" :rows="3" /></el-form-item>
      </el-form>
      <template #footer><el-button @click="dialogVisible=false">取消</el-button><el-button type="primary" @click="save">保存</el-button></template>
    </el-dialog>
  </div>
</template>
<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { http } from '../api/http'
const rows=ref<any[]>([]), groups=ref<any[]>([]), group=ref('')
const dialogVisible=ref(false)
const form=reactive<any>({id:0,settingGroup:'general',settingKey:'',settingValue:'',valueType:'text',isSystem:false,remark:''})
async function load(){groups.value=(await http.get('/system-settings/groups')).data; await loadSettings()}
async function loadSettings(){const params:any={}; if(group.value)params.group=group.value; rows.value=(await http.get('/system-settings',{params})).data}
async function seed(){const r=await http.post('/system-settings/seed'); ElMessage.success(`初始化完成：新增${r.data.created} / 总数${r.data.total}`); await loadSettings()}
function reset(){Object.assign(form,{id:0,settingGroup:group.value||'general',settingKey:'',settingValue:'',valueType:'text',isSystem:false,remark:''})}
function openCreate(){reset(); dialogVisible.value=true}
function openEdit(row:any){Object.assign(form,row); dialogVisible.value=true}
async function save(){if(!form.settingKey)return ElMessage.warning('请输入参数Key'); form.id?await http.put(`/system-settings/${form.id}`,form):await http.post('/system-settings',form); dialogVisible.value=false; ElMessage.success('保存成功'); await loadSettings()}
async function remove(id:number){await ElMessageBox.confirm('确认删除该参数？','提示'); await http.delete(`/system-settings/${id}`); ElMessage.success('已删除'); await loadSettings()}
onMounted(load)
</script>
<style scoped>.toolbar{display:flex;gap:8px;align-items:center;margin-bottom:12px;flex-wrap:wrap}.card{margin-bottom:14px}</style>
