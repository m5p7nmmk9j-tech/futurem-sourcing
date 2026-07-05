<template>
  <div class="page">
    <div class="page-header">
      <div class="page-title">打印中心 Print Center</div>
      <div class="toolbar"><el-button type="primary" @click="seed">初始化模板</el-button><el-button @click="load">刷新</el-button></div>
    </div>

    <el-tabs v-model="activeTab" class="card">
      <el-tab-pane label="打印预览" name="preview">
        <div class="toolbar">
          <el-select v-model="previewForm.documentType" placeholder="单据类型" style="width:220px"><el-option v-for="d in documents" :key="d.type" :label="d.name" :value="d.type"/></el-select>
          <el-input-number v-model="previewForm.id" :min="1" placeholder="单据ID" style="width:160px" />
          <el-select v-model="previewForm.language" style="width:120px"><el-option label="English" value="en"/><el-option label="中文" value="zh"/><el-option label="Español" value="es"/></el-select>
          <el-button type="primary" @click="preview">预览</el-button>
          <el-button @click="openPrint">新窗口打印</el-button>
        </div>
        <iframe v-if="previewHtml" class="print-frame" :srcdoc="previewHtml"></iframe>
        <el-empty v-else description="请选择单据并点击预览" />
      </el-tab-pane>

      <el-tab-pane label="模板管理" name="templates">
        <div class="toolbar">
          <el-button type="primary" @click="openCreate">新增模板</el-button>
          <el-select v-model="filter.documentType" clearable placeholder="单据类型" style="width:220px" @change="loadTemplates"><el-option v-for="d in documents" :key="d.type" :label="d.name" :value="d.type"/></el-select>
          <el-select v-model="filter.language" clearable placeholder="语言" style="width:120px" @change="loadTemplates"><el-option label="English" value="en"/><el-option label="中文" value="zh"/><el-option label="Español" value="es"/></el-select>
        </div>
        <el-table :data="templates" border stripe>
          <el-table-column prop="code" label="编码" width="180" />
          <el-table-column prop="name" label="名称" width="220" />
          <el-table-column prop="documentType" label="单据类型" width="130" />
          <el-table-column prop="language" label="语言" width="90" />
          <el-table-column prop="paperSize" label="纸张" width="90" />
          <el-table-column prop="isDefault" label="默认" width="80"><template #default="s">{{ s.row.isDefault ? '是' : '否' }}</template></el-table-column>
          <el-table-column prop="status" label="状态" width="100" />
          <el-table-column prop="remark" label="备注" />
          <el-table-column label="操作" width="190" fixed="right"><template #default="s"><el-button size="small" @click="openEdit(s.row)">编辑</el-button><el-button size="small" type="danger" @click="remove(s.row.id)">删除</el-button></template></el-table-column>
        </el-table>
      </el-tab-pane>
    </el-tabs>

    <el-dialog v-model="dialogVisible" :title="form.id ? '编辑模板' : '新增模板'" width="900px">
      <el-form label-width="100px">
        <el-row :gutter="12">
          <el-col :span="12"><el-form-item label="编码"><el-input v-model="form.code" /></el-form-item></el-col>
          <el-col :span="12"><el-form-item label="名称"><el-input v-model="form.name" /></el-form-item></el-col>
        </el-row>
        <el-row :gutter="12">
          <el-col :span="8"><el-form-item label="单据类型"><el-select v-model="form.documentType" style="width:100%"><el-option v-for="d in documents" :key="d.type" :label="d.name" :value="d.type"/></el-select></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="语言"><el-select v-model="form.language" style="width:100%"><el-option label="English" value="en"/><el-option label="中文" value="zh"/><el-option label="Español" value="es"/></el-select></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="纸张"><el-select v-model="form.paperSize" style="width:100%"><el-option label="A4" value="A4"/><el-option label="Letter" value="Letter"/></el-select></el-form-item></el-col>
        </el-row>
        <el-row :gutter="12">
          <el-col :span="8"><el-form-item label="默认"><el-switch v-model="form.isDefault" /></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="状态"><el-select v-model="form.status" style="width:100%"><el-option label="启用" value="active"/><el-option label="停用" value="disabled"/></el-select></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="备注"><el-input v-model="form.remark" /></el-form-item></el-col>
        </el-row>
        <el-form-item label="模板内容"><el-input v-model="form.body" type="textarea" :rows="14" placeholder="支持变量：{{company}} {{documentType}} {{no}} {{date}} {{status}} {{amount}} {{currency}}" /></el-form-item>
      </el-form>
      <template #footer><el-button @click="dialogVisible=false">取消</el-button><el-button type="primary" @click="save">保存</el-button></template>
    </el-dialog>
  </div>
</template>
<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { http } from '../api/http'
const activeTab=ref('preview'), documents=ref<any[]>([]), templates=ref<any[]>([]), previewHtml=ref('')
const filter=reactive<any>({documentType:'',language:''})
const previewForm=reactive<any>({documentType:'PO',id:1,language:'en'})
const dialogVisible=ref(false)
const form=reactive<any>({id:0,code:'',name:'',documentType:'PO',language:'en',paperSize:'A4',body:'',isDefault:false,status:'active',remark:''})
async function load(){documents.value=(await http.get('/print-center/documents')).data; await loadTemplates()}
async function loadTemplates(){const params:any={}; if(filter.documentType)params.documentType=filter.documentType; if(filter.language)params.language=filter.language; templates.value=(await http.get('/print-center/templates',{params})).data}
async function seed(){const r=await http.post('/print-center/seed'); ElMessage.success(`模板初始化完成：${r.data.templates}`); await loadTemplates()}
function reset(){Object.assign(form,{id:0,code:'',name:'',documentType:'PO',language:'en',paperSize:'A4',body:"<html><body style='font-family:Arial;padding:32px'><h1>{{documentType}}</h1><h2>{{company}}</h2><p>No: {{no}}</p><p>Date: {{date}}</p><p>Status: {{status}}</p><p>Amount: {{amount}} {{currency}}</p></body></html>",isDefault:false,status:'active',remark:''})}
function openCreate(){reset();dialogVisible.value=true}
function openEdit(row:any){Object.assign(form,row);dialogVisible.value=true}
async function save(){form.id?await http.put(`/print-center/templates/${form.id}`,form):await http.post('/print-center/templates',form); dialogVisible.value=false; ElMessage.success('保存成功'); await loadTemplates()}
async function remove(id:number){await ElMessageBox.confirm('确认删除模板？','提示'); await http.delete(`/print-center/templates/${id}`); ElMessage.success('已删除'); await loadTemplates()}
async function preview(){const r=await http.get('/print-center/preview',{params:previewForm}); previewHtml.value=r.data.html}
function openPrint(){const url=`/api/print-center/html?documentType=${previewForm.documentType}&id=${previewForm.id}&language=${previewForm.language}`; window.open(url,'_blank')}
onMounted(load)
</script>
<style scoped>.toolbar{display:flex;gap:8px;align-items:center;margin-bottom:12px;flex-wrap:wrap}.card{padding:12px}.print-frame{width:100%;height:680px;border:1px solid #e5e7eb;background:#fff}</style>
