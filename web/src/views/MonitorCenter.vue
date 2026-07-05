<template>
  <div class="page">
    <div class="page-header"><div class="page-title">系统监控 Monitor Center</div><div class="toolbar"><el-button type="primary" @click="load">刷新</el-button></div></div>
    <div class="card">
      <el-row :gutter="16">
        <el-col :span="6"><div class="stat-card"><div class="stat-title">API</div><div class="stat-value">{{ overview.api?.status || '-' }}</div><div class="stat-sub">{{ overview.api?.version }}</div></div></el-col>
        <el-col :span="6"><div class="stat-card"><div class="stat-title">MySQL</div><div class="stat-value ok">{{ overview.mysql?.status || '-' }}</div></div></el-col>
        <el-col :span="6"><div class="stat-card"><div class="stat-title">Redis</div><div class="stat-value warn">{{ overview.redis?.status || '-' }}</div></div></el-col>
        <el-col :span="6"><div class="stat-card"><div class="stat-title">运行时长</div><div class="stat-value">{{ uptime }}</div></div></el-col>
      </el-row>
    </div>
    <div class="card">
      <div class="section-title">服务器资源</div>
      <el-row :gutter="16">
        <el-col :span="6"><div class="stat-card"><div class="stat-title">内存MB</div><div class="stat-value">{{ overview.process?.memoryMb || 0 }}</div></div></el-col>
        <el-col :span="6"><div class="stat-card"><div class="stat-title">线程</div><div class="stat-value">{{ overview.process?.threads || 0 }}</div></div></el-col>
        <el-col :span="6"><div class="stat-card"><div class="stat-title">CPU核心</div><div class="stat-value">{{ overview.server?.processorCount || 0 }}</div></div></el-col>
        <el-col :span="6"><div class="stat-card"><div class="stat-title">磁盘使用率</div><el-progress type="dashboard" :percentage="Number(overview.disk?.usedPercent || 0)" /></div></el-col>
      </el-row>
    </div>
    <div class="card">
      <div class="section-title">数据库数据量</div>
      <el-row :gutter="16"><el-col :span="4" v-for="item in dbCards" :key="item.title"><div class="stat-card small"><div class="stat-title">{{ item.title }}</div><div class="stat-value">{{ item.value }}</div></div></el-col></el-row>
    </div>
    <el-row :gutter="16">
      <el-col :span="12"><div class="card"><div class="section-title">最近登录日志</div><el-table :data="logs.loginLogs || []" border stripe size="small"><el-table-column prop="username" label="用户" width="120"/><el-table-column prop="result" label="结果" width="100"/><el-table-column prop="ipAddress" label="IP" width="150"/><el-table-column prop="loginAt" label="时间"/></el-table></div></el-col>
      <el-col :span="12"><div class="card"><div class="section-title">最近备份</div><el-table :data="logs.backups || []" border stripe size="small"><el-table-column prop="backupNo" label="备份号" width="170"/><el-table-column prop="status" label="状态" width="100"/><el-table-column prop="fileSizeBytes" label="大小" width="100"/><el-table-column prop="startedAt" label="时间"/></el-table></div></el-col>
    </el-row>
    <div class="card"><div class="section-title">系统信息</div><el-descriptions border :column="2"><el-descriptions-item label="机器名">{{ overview.server?.machine }}</el-descriptions-item><el-descriptions-item label="操作系统">{{ overview.server?.os }}</el-descriptions-item><el-descriptions-item label="磁盘总量GB">{{ overview.disk?.totalGb }}</el-descriptions-item><el-descriptions-item label="磁盘剩余GB">{{ overview.disk?.freeGb }}</el-descriptions-item></el-descriptions></div>
  </div>
</template>
<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { http } from '../api/http'
const overview=ref<any>({}), logs=ref<any>({})
const uptime=computed(()=>{const s=Number(overview.value.api?.uptimeSeconds||0); if(!s)return '-'; const h=Math.floor(s/3600), m=Math.floor((s%3600)/60); return `${h}h ${m}m`})
const dbCards=computed(()=>[{title:'用户',value:overview.value.database?.users||0},{title:'商品',value:overview.value.database?.products||0},{title:'客户',value:overview.value.database?.customers||0},{title:'供应商',value:overview.value.database?.suppliers||0},{title:'操作日志',value:overview.value.database?.auditLogs||0},{title:'备份',value:overview.value.database?.backups||0}])
async function load(){overview.value=(await http.get('/monitor-center/overview')).data; logs.value=(await http.get('/monitor-center/logs')).data}
onMounted(load)
</script>
<style scoped>.toolbar{display:flex;gap:8px}.card{margin-bottom:14px}.section-title{font-weight:800;margin-bottom:12px}.stat-card{min-height:92px;padding:14px;border:1px solid #e5e7eb;border-radius:12px;background:#fff}.stat-card.small{min-height:76px}.stat-title{color:#6b7280;font-size:14px}.stat-value{margin-top:10px;font-size:26px;font-weight:800;color:#111827}.stat-sub{margin-top:6px;color:#6b7280}.ok{color:#059669}.bad{color:#dc2626}.warn{color:#d97706}</style>
