<template>
  <div class="page">
    <div class="page-header">
      <div class="page-title">首页 Dashboard / 业务驾驶舱</div>
      <el-button @click="load">刷新</el-button>
    </div>

    <div class="card">
      <div class="section-title">基础资料</div>
      <el-row :gutter="16">
        <el-col :span="8"><div class="stat-card"><div class="stat-title">客户</div><div class="stat-value">{{ summary.masterData?.customers || 0 }}</div></div></el-col>
        <el-col :span="8"><div class="stat-card"><div class="stat-title">供应商</div><div class="stat-value">{{ summary.masterData?.suppliers || 0 }}</div></div></el-col>
        <el-col :span="8"><div class="stat-card"><div class="stat-title">商品</div><div class="stat-value">{{ summary.masterData?.products || 0 }}</div></div></el-col>
      </el-row>
    </div>

    <div class="card">
      <div class="section-title">业务流程</div>
      <el-row :gutter="16">
        <el-col :span="3" v-for="item in workflowCards" :key="item.title"><div class="stat-card small"><div class="stat-title">{{ item.title }}</div><div class="stat-value">{{ item.value }}</div></div></el-col>
      </el-row>
    </div>

    <div class="card">
      <div class="section-title">财务总览</div>
      <el-row :gutter="16">
        <el-col :span="4"><div class="stat-card"><div class="stat-title">应收</div><div class="stat-value money">{{ summary.finance?.receivableAmount || 0 }}</div></div></el-col>
        <el-col :span="4"><div class="stat-card"><div class="stat-title">已收</div><div class="stat-value money">{{ summary.finance?.receivedAmount || 0 }}</div></div></el-col>
        <el-col :span="4"><div class="stat-card warn"><div class="stat-title">未收</div><div class="stat-value money">{{ summary.finance?.receivableBalance || 0 }}</div></div></el-col>
        <el-col :span="4"><div class="stat-card"><div class="stat-title">应付</div><div class="stat-value money">{{ summary.finance?.payableAmount || 0 }}</div></div></el-col>
        <el-col :span="4"><div class="stat-card"><div class="stat-title">已付</div><div class="stat-value money">{{ summary.finance?.paidAmount || 0 }}</div></div></el-col>
        <el-col :span="4"><div class="stat-card danger"><div class="stat-title">未付</div><div class="stat-value money">{{ summary.finance?.payableBalance || 0 }}</div></div></el-col>
      </el-row>
    </div>

    <div class="card">
      <div class="section-title">预警中心</div>
      <el-row :gutter="16">
        <el-col :span="4"><el-alert :title="`待付款 PO：${summary.alerts?.unpaidPo || 0}`" type="warning" show-icon /></el-col>
        <el-col :span="4"><el-alert :title="`待收款 SO：${summary.alerts?.unpaidSo || 0}`" type="warning" show-icon /></el-col>
        <el-col :span="4"><el-alert :title="`QC异常：${summary.alerts?.qcFailed || 0}`" type="error" show-icon /></el-col>
        <el-col :span="6"><el-alert :title="`未出运装柜：${summary.alerts?.containersNotShipped || 0}`" type="info" show-icon /></el-col>
        <el-col :span="6"><el-alert :title="`未到港出运：${summary.alerts?.shipmentsNotArrived || 0}`" type="info" show-icon /></el-col>
      </el-row>
    </div>

    <div class="card">
      <div class="section-title">待办事项</div>
      <el-row :gutter="16">
        <el-col :span="12">
          <h3>待收款</h3>
          <el-table :data="todo.pendingReceivables || []" border stripe size="small"><el-table-column prop="no" label="单号"/><el-table-column prop="currency" label="币种" width="80"/><el-table-column prop="balance" label="未收" width="120"/><el-table-column prop="status" label="状态" width="100"/></el-table>
        </el-col>
        <el-col :span="12">
          <h3>待付款</h3>
          <el-table :data="todo.pendingPayables || []" border stripe size="small"><el-table-column prop="no" label="单号"/><el-table-column prop="currency" label="币种" width="80"/><el-table-column prop="balance" label="未付" width="120"/><el-table-column prop="status" label="状态" width="100"/></el-table>
        </el-col>
      </el-row>
      <el-row :gutter="16" style="margin-top:16px">
        <el-col :span="12">
          <h3>待出运装柜</h3>
          <el-table :data="todo.pendingContainers || []" border stripe size="small"><el-table-column prop="no" label="装柜单"/><el-table-column prop="containerType" label="柜型" width="90"/><el-table-column prop="totalCbm" label="CBM" width="90"/><el-table-column prop="status" label="状态" width="120"/></el-table>
        </el-col>
        <el-col :span="12">
          <h3>未完成出运</h3>
          <el-table :data="todo.pendingShipments || []" border stripe size="small"><el-table-column prop="no" label="出运单"/><el-table-column prop="shipmentMode" label="方式" width="90"/><el-table-column prop="eta" label="ETA" width="120"/><el-table-column prop="status" label="状态" width="120"/></el-table>
        </el-col>
      </el-row>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { http } from '../api/http'
const summary = ref<any>({})
const todo = ref<any>({})
const workflowCards = computed(() => [
  { title: 'RFQ', value: summary.value.workflow?.rfqs || 0 },
  { title: 'CO', value: summary.value.workflow?.customerOrders || 0 },
  { title: 'PO', value: summary.value.workflow?.purchaseOrders || 0 },
  { title: 'SO', value: summary.value.workflow?.summaryOrders || 0 },
  { title: '收货', value: summary.value.workflow?.receivingOrders || 0 },
  { title: 'QC', value: summary.value.workflow?.qcOrders || 0 },
  { title: '装柜', value: summary.value.workflow?.containerLoads || 0 },
  { title: '出运', value: summary.value.workflow?.shipments || 0 }
])
async function load(){summary.value=(await http.get('/business-dashboard/summary')).data; todo.value=(await http.get('/business-dashboard/todo')).data}
onMounted(load)
</script>

<style scoped>
.section-title { font-weight: 800; margin-bottom: 12px; }
.stat-card { min-height: 92px; padding: 14px; border: 1px solid #e5e7eb; border-radius: 12px; background: #fff; }
.stat-card.small { min-height: 76px; }
.stat-card.warn { background:#fff7ed; }
.stat-card.danger { background:#fef2f2; }
.stat-title { color: #6b7280; font-size: 14px; }
.stat-value { margin-top: 10px; font-size: 28px; font-weight: 800; color: #111827; }
.stat-value.money { font-size: 22px; }
h3 { margin: 0 0 8px; font-size: 15px; }
</style>
