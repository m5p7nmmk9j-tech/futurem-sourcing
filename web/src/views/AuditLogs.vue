<template>
  <div class="page">
    <div class="page-header">
      <div class="page-title">操作日志 Audit Logs</div>
      <div class="toolbar"><el-button @click="load">刷新</el-button></div>
    </div>

    <div class="card">
      <el-row :gutter="12">
        <el-col :span="4"><el-statistic title="今日操作" :value="summary.today || 0" /></el-col>
        <el-col :span="4"><el-statistic title="新增" :value="summary.creates || 0" /></el-col>
        <el-col :span="4"><el-statistic title="修改" :value="summary.updates || 0" /></el-col>
        <el-col :span="4"><el-statistic title="删除" :value="summary.deletes || 0" /></el-col>
        <el-col :span="4"><el-statistic title="审批" :value="summary.approvals || 0" /></el-col>
        <el-col :span="4"><el-statistic title="失败" :value="summary.failures || 0" /></el-col>
      </el-row>
    </div>

    <div class="card">
      <div class="toolbar">
        <el-input v-model="filters.module" placeholder="模块" clearable style="width:150px" />
        <el-select v-model="filters.action" clearable placeholder="动作" style="width:140px"><el-option label="新增" value="create"/><el-option label="修改" value="update"/><el-option label="删除" value="delete"/><el-option label="审批通过" value="approve"/><el-option label="驳回" value="reject"/><el-option label="退回" value="return"/><el-option label="导出" value="export"/><el-option label="打印" value="print"/><el-option label="登录" value="login"/></el-select>
        <el-input v-model="filters.targetType" placeholder="对象类型" clearable style="width:150px" />
        <el-input-number v-model="filters.userId" :min="0" placeholder="用户ID" style="width:140px" />
        <el-date-picker v-model="dateRange" type="daterange" value-format="YYYY-MM-DD" range-separator="至" start-placeholder="开始" end-placeholder="结束" style="width:260px" />
        <el-button type="primary" @click="load">查询</el-button>
        <el-button @click="resetFilters">重置</el-button>
      </div>
      <el-table :data="rows" border stripe>
        <el-table-column prop="createdAt" label="时间" width="180" />
        <el-table-column prop="username" label="用户" width="120" />
        <el-table-column prop="userId" label="用户ID" width="90" />
        <el-table-column prop="module" label="模块" width="120" />
        <el-table-column prop="action" label="动作" width="110"><template #default="scope"><el-tag :type="actionType(scope.row.action)">{{ actionName(scope.row.action) }}</el-tag></template></el-table-column>
        <el-table-column prop="targetType" label="对象" width="120" />
        <el-table-column prop="targetId" label="对象ID" width="100" />
        <el-table-column prop="targetNo" label="对象单号" width="160" />
        <el-table-column prop="result" label="结果" width="100"><template #default="scope"><el-tag :type="scope.row.result==='success'?'success':'danger'">{{ scope.row.result }}</el-tag></template></el-table-column>
        <el-table-column prop="ipAddress" label="IP" width="150" />
        <el-table-column prop="userAgent" label="设备" min-width="220" show-overflow-tooltip />
        <el-table-column label="操作" width="110" fixed="right"><template #default="scope"><el-button size="small" type="danger" @click="remove(scope.row.id)">删除</el-button></template></el-table-column>
      </el-table>
    </div>
  </div>
</template>
<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { http } from '../api/http'
const rows=ref<any[]>([]), summary=ref<any>({}), dateRange=ref<any[]>([])
const filters=reactive<any>({module:'',action:'',targetType:'',userId:null})
function actionName(a:string){return ({create:'新增',update:'修改',delete:'删除',approve:'通过',reject:'驳回',return:'退回',export:'导出',print:'打印',login:'登录'} as any)[a]||a}
function actionType(a:string){return a==='delete'||a==='reject'?'danger':a==='approve'?'success':a==='return'?'warning':'info'}
async function load(){const params:any={}; if(filters.module)params.module=filters.module; if(filters.action)params.action=filters.action; if(filters.targetType)params.targetType=filters.targetType; if(filters.userId)params.userId=filters.userId; if(dateRange.value?.length===2){params.start=dateRange.value[0];params.end=dateRange.value[1]} rows.value=(await http.get('/audit-logs',{params})).data; summary.value=(await http.get('/audit-logs/summary')).data}
function resetFilters(){Object.assign(filters,{module:'',action:'',targetType:'',userId:null}); dateRange.value=[]; load()}
async function remove(id:number){await ElMessageBox.confirm('确认删除该日志？','提示'); await http.delete(`/audit-logs/${id}`); ElMessage.success('已删除'); await load()}
onMounted(load)
</script>
<style scoped>.toolbar{display:flex;gap:8px;align-items:center;margin-bottom:12px;flex-wrap:wrap}.card{margin-bottom:14px}</style>
