<template>
  <div class="page">
    <div class="page-header">
      <div class="page-title">备份中心 Backup Center</div>
      <div class="toolbar"><el-button type="primary" @click="runBackup">立即备份</el-button><el-button @click="seed">初始化任务</el-button><el-button @click="load">刷新</el-button></div>
    </div>

    <el-tabs v-model="activeTab" class="card">
      <el-tab-pane label="备份任务" name="jobs">
        <div class="toolbar"><el-button type="primary" @click="openCreate">新增任务</el-button></div>
        <el-table :data="jobs" border stripe>
          <el-table-column prop="name" label="任务名称" width="180" />
          <el-table-column prop="scheduleType" label="周期" width="100" />
          <el-table-column prop="backupScope" label="范围" width="120" />
          <el-table-column prop="storagePath" label="路径" width="160" />
          <el-table-column prop="isEnabled" label="启用" width="80"><template #default="s">{{ s.row.isEnabled ? '是' : '否' }}</template></el-table-column>
          <el-table-column prop="lastRunAt" label="上次运行" width="180" />
          <el-table-column prop="nextRunAt" label="下次运行" width="180" />
          <el-table-column prop="status" label="状态" width="100" />
          <el-table-column label="操作" width="230" fixed="right"><template #default="s"><el-button size="small" @click="openEdit(s.row)">编辑</el-button><el-button size="small" type="success" @click="runBackup(s.row.id)">运行</el-button><el-button size="small" type="danger" @click="deleteJob(s.row.id)">删除</el-button></template></el-table-column>
        </el-table>
      </el-tab-pane>

      <el-tab-pane label="备份历史" name="history">
        <el-table :data="history" border stripe>
          <el-table-column prop="backupNo" label="备份号" width="180" />
          <el-table-column prop="backupType" label="类型" width="100" />
          <el-table-column prop="fileName" label="文件" width="220" />
          <el-table-column prop="fileSizeBytes" label="大小(bytes)" width="120" />
          <el-table-column prop="status" label="状态" width="100"><template #default="s"><el-tag :type="s.row.status==='success'?'success':'danger'">{{ s.row.status }}</el-tag></template></el-table-column>
          <el-table-column prop="startedAt" label="开始" width="180" />
          <el-table-column prop="finishedAt" label="结束" width="180" />
          <el-table-column prop="message" label="说明" min-width="220" />
          <el-table-column label="操作" width="270" fixed="right"><template #default="s"><el-button size="small" @click="verify(s.row.id)">校验</el-button><el-button size="small" type="primary" @click="download(s.row.id)">下载</el-button><el-button size="small" type="warning" @click="restore(s.row.id)">恢复校验</el-button><el-button size="small" type="danger" @click="deleteHistory(s.row.id)">删除</el-button></template></el-table-column>
        </el-table>
      </el-tab-pane>

      <el-tab-pane label="恢复历史" name="restore">
        <el-table :data="restoreRows" border stripe>
          <el-table-column prop="restoreNo" label="恢复号" width="180" />
          <el-table-column prop="fileName" label="文件" width="220" />
          <el-table-column prop="verifiedBeforeRestore" label="已校验" width="100"><template #default="s">{{ s.row.verifiedBeforeRestore ? '是' : '否' }}</template></el-table-column>
          <el-table-column prop="status" label="状态" width="110" />
          <el-table-column prop="startedAt" label="开始" width="180" />
          <el-table-column prop="finishedAt" label="结束" width="180" />
          <el-table-column prop="message" label="说明" min-width="260" />
        </el-table>
      </el-tab-pane>
    </el-tabs>

    <el-dialog v-model="dialogVisible" :title="form.id ? '编辑备份任务' : '新增备份任务'" width="620px">
      <el-form label-width="100px">
        <el-form-item label="任务名称"><el-input v-model="form.name" /></el-form-item>
        <el-form-item label="周期"><el-select v-model="form.scheduleType" style="width:100%"><el-option label="手动" value="manual"/><el-option label="每日" value="daily"/><el-option label="每周" value="weekly"/><el-option label="每月" value="monthly"/></el-select></el-form-item>
        <el-form-item label="备份范围"><el-select v-model="form.backupScope" style="width:100%"><el-option label="数据库" value="database"/><el-option label="系统配置" value="settings"/></el-select></el-form-item>
        <el-form-item label="保存路径"><el-input v-model="form.storagePath" /></el-form-item>
        <el-form-item label="启用"><el-switch v-model="form.isEnabled" /></el-form-item>
        <el-form-item label="状态"><el-input v-model="form.status" /></el-form-item>
        <el-form-item label="备注"><el-input v-model="form.remark" type="textarea" :rows="3" /></el-form-item>
      </el-form>
      <template #footer><el-button @click="dialogVisible=false">取消</el-button><el-button type="primary" @click="saveJob">保存</el-button></template>
    </el-dialog>
  </div>
</template>
<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { http } from '../api/http'
const activeTab=ref('jobs'), jobs=ref<any[]>([]), history=ref<any[]>([]), restoreRows=ref<any[]>([]), dialogVisible=ref(false)
const form=reactive<any>({id:0,name:'',scheduleType:'manual',backupScope:'database',storagePath:'backups',isEnabled:true,status:'ready',remark:''})
async function load(){jobs.value=(await http.get('/backup-center/jobs')).data; history.value=(await http.get('/backup-center/history')).data; restoreRows.value=(await http.get('/backup-center/restore-history')).data}
async function seed(){const r=await http.post('/backup-center/seed'); ElMessage.success(`初始化完成：${r.data.jobs} 个任务`); await load()}
function reset(){Object.assign(form,{id:0,name:'',scheduleType:'manual',backupScope:'database',storagePath:'backups',isEnabled:true,status:'ready',remark:''})}
function openCreate(){reset();dialogVisible.value=true}
function openEdit(row:any){Object.assign(form,row);dialogVisible.value=true}
async function saveJob(){form.id?await http.put(`/backup-center/jobs/${form.id}`,form):await http.post('/backup-center/jobs',form); dialogVisible.value=false; ElMessage.success('保存成功'); await load()}
async function deleteJob(id:number){await ElMessageBox.confirm('确认删除任务？','提示'); await http.delete(`/backup-center/jobs/${id}`); ElMessage.success('已删除'); await load()}
async function runBackup(jobId?:number){const params:any={}; if(jobId)params.jobId=jobId; const r=await http.post('/backup-center/run',null,{params}); ElMessage.success(`备份完成：${r.data.backupNo || ''}`); await load(); activeTab.value='history'}
async function verify(id:number){const r=await http.post(`/backup-center/verify/${id}`); ElMessage[r.data.verified?'success':'error'](r.data.verified?'校验通过':'校验失败')}
function download(id:number){window.open(`/api/backup-center/download/${id}`,'_blank')}
async function restore(id:number){await ElMessageBox.confirm('该操作只做恢复前校验和记录，真实恢复需DBA确认。继续？','提示'); const r=await http.post(`/backup-center/restore/${id}`); ElMessage.success(r.data.message || '已记录'); await load(); activeTab.value='restore'}
async function deleteHistory(id:number){await ElMessageBox.confirm('确认删除备份文件和记录？','提示'); await http.delete(`/backup-center/history/${id}`); ElMessage.success('已删除'); await load()}
onMounted(load)
</script>
<style scoped>.toolbar{display:flex;gap:8px;align-items:center;margin-bottom:12px;flex-wrap:wrap}.card{padding:12px}</style>
