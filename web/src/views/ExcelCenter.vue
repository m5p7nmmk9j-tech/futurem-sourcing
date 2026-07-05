<template>
  <div class="page">
    <div class="page-header">
      <div class="page-title">Excel中心 Excel Center</div>
      <div class="toolbar"><el-button @click="load">刷新</el-button></div>
    </div>

    <div class="card">
      <div class="toolbar">
        <el-select v-model="module" placeholder="选择模块" style="width:260px" @change="clearResult">
          <el-option v-for="m in modules" :key="m.code" :label="`${m.name} / ${m.code}`" :value="m.code" />
        </el-select>
        <el-button type="primary" @click="downloadTemplate">下载模板</el-button>
        <el-button type="success" @click="exportData">导出数据</el-button>
      </div>
      <el-alert title="当前支持CSV模板、CSV导出和CSV上传解析；后续可继续接入xlsx真实导入写库。" type="info" show-icon />
    </div>

    <div class="card">
      <div class="section-title">上传导入</div>
      <el-upload :auto-upload="false" :limit="1" accept=".csv,.txt" :on-change="onFileChange" :on-remove="onFileRemove">
        <el-button type="primary">选择CSV文件</el-button>
      </el-upload>
      <div class="toolbar" style="margin-top:12px">
        <el-button type="primary" :disabled="!selectedFile" @click="importFile">上传解析</el-button>
      </div>
    </div>

    <div class="card" v-if="currentTemplate.length">
      <div class="section-title">字段模板</div>
      <el-table :data="currentTemplate.map((x:string,i:number)=>({index:i+1,field:x}))" border stripe size="small">
        <el-table-column prop="index" label="#" width="80" />
        <el-table-column prop="field" label="字段名" />
      </el-table>
    </div>

    <div class="card" v-if="result">
      <div class="section-title">导入结果</div>
      <el-descriptions border :column="2">
        <el-descriptions-item label="模块">{{ result.module }}</el-descriptions-item>
        <el-descriptions-item label="解析行数">{{ result.rows }}</el-descriptions-item>
        <el-descriptions-item label="已导入">{{ result.imported }}</el-descriptions-item>
        <el-descriptions-item label="说明">{{ result.message }}</el-descriptions-item>
      </el-descriptions>
    </div>
  </div>
</template>
<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { http } from '../api/http'
const modules=ref<any[]>([])
const module=ref('customers')
const selectedFile=ref<any>(null)
const result=ref<any>(null)
const currentTemplate=computed(()=>modules.value.find(x=>x.code===module.value)?.template || [])
async function load(){modules.value=(await http.get('/excel-center/modules')).data}
function clearResult(){result.value=null; selectedFile.value=null}
function downloadTemplate(){window.open(`/api/excel-center/template/${module.value}`,'_blank')}
function exportData(){window.open(`/api/excel-center/export/${module.value}`,'_blank')}
function onFileChange(file:any){selectedFile.value=file.raw}
function onFileRemove(){selectedFile.value=null}
async function importFile(){if(!selectedFile.value)return ElMessage.warning('请选择文件'); const form=new FormData(); form.append('file',selectedFile.value); result.value=(await http.post(`/excel-center/import/${module.value}`,form,{headers:{'Content-Type':'multipart/form-data'}})).data; ElMessage.success('上传解析完成')}
onMounted(load)
</script>
<style scoped>.toolbar{display:flex;gap:8px;align-items:center;margin-bottom:12px;flex-wrap:wrap}.card{margin-bottom:14px}.section-title{font-weight:800;margin-bottom:12px}</style>
