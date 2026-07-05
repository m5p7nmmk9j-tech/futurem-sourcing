<template>
  <div class="page">
    <div class="page-header">
      <div class="page-title">消息中心 Notification Center</div>
      <div class="toolbar">
        <el-button type="primary" @click="generate">从预警生成消息</el-button>
        <el-button @click="readAll">全部已读</el-button>
        <el-button @click="load">刷新</el-button>
      </div>
    </div>

    <div class="card">
      <el-row :gutter="12">
        <el-col :span="6"><el-statistic title="未读" :value="summary.unread || 0" /></el-col>
        <el-col :span="6"><el-statistic title="严重" :value="summary.danger || 0" /></el-col>
        <el-col :span="6"><el-statistic title="提醒" :value="summary.warning || 0" /></el-col>
        <el-col :span="6"><el-statistic title="普通" :value="summary.info || 0" /></el-col>
      </el-row>
    </div>

    <div class="card">
      <div class="toolbar">
        <el-select v-model="status" clearable placeholder="状态" style="width:140px" @change="load"><el-option label="未读" value="unread"/><el-option label="已读" value="read"/></el-select>
        <el-select v-model="level" clearable placeholder="级别" style="width:140px" @change="load"><el-option label="严重" value="danger"/><el-option label="提醒" value="warning"/><el-option label="普通" value="info"/></el-select>
      </div>
      <el-table :data="rows" border stripe>
        <el-table-column prop="level" label="级别" width="90"><template #default="scope"><el-tag :type="tagType(scope.row.level)">{{ levelName(scope.row.level) }}</el-tag></template></el-table-column>
        <el-table-column prop="status" label="状态" width="90"><template #default="scope"><el-tag :type="scope.row.status==='unread'?'warning':'info'">{{ scope.row.status==='unread'?'未读':'已读' }}</el-tag></template></el-table-column>
        <el-table-column prop="title" label="标题" width="220" />
        <el-table-column prop="message" label="内容" min-width="280" />
        <el-table-column prop="sourceType" label="来源" width="120" />
        <el-table-column prop="sourceId" label="来源ID" width="100" />
        <el-table-column prop="createdAt" label="创建时间" width="180" />
        <el-table-column label="操作" width="180" fixed="right"><template #default="scope"><el-button size="small" @click="markRead(scope.row.id)" :disabled="scope.row.status==='read'">已读</el-button><el-button size="small" type="danger" @click="remove(scope.row.id)">删除</el-button></template></el-table-column>
      </el-table>
    </div>
  </div>
</template>
<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { http } from '../api/http'
const rows=ref<any[]>([]), summary=ref<any>({})
const status=ref('unread'), level=ref<string|null>(null)
function tagType(level:string){return level==='danger'?'danger':level==='warning'?'warning':'info'}
function levelName(level:string){return level==='danger'?'严重':level==='warning'?'提醒':'普通'}
async function load(){const params:any={}; if(status.value)params.status=status.value; if(level.value)params.level=level.value; rows.value=(await http.get('/notifications',{params})).data; summary.value=(await http.get('/notifications/summary')).data}
async function generate(){const res=await http.post('/notifications/generate-from-warnings'); ElMessage.success(`已生成 ${res.data?.created || 0} 条消息`); await load()}
async function markRead(id:number){await http.post(`/notifications/${id}/read`); ElMessage.success('已标记已读'); await load()}
async function readAll(){await http.post('/notifications/read-all'); ElMessage.success('已全部标记已读'); await load()}
async function remove(id:number){await ElMessageBox.confirm('确认删除该消息？','提示'); await http.delete(`/notifications/${id}`); ElMessage.success('已删除'); await load()}
onMounted(load)
</script>
<style scoped>.toolbar{display:flex;gap:8px;align-items:center}.card{margin-bottom:14px}</style>
