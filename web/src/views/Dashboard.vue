<template>
  <div class="page">
    <div class="page-header"><div class="page-title">首页 Dashboard / 老板驾驶舱 V2</div><div class="toolbar"><el-select v-model="period" style="width:140px" @change="load"><el-option label="今日" value="today"/><el-option label="本周" value="week"/><el-option label="本月" value="month"/><el-option label="本季度" value="quarter"/><el-option label="本年" value="year"/></el-select><el-button @click="load">刷新</el-button></div></div>

    <div class="card"><div class="section-title">核心经营指标</div><el-row :gutter="16"><el-col :span="4"><div class="stat-card"><div class="stat-title">收入</div><div class="stat-value money">{{ profit.income || 0 }}</div></div></el-col><el-col :span="4"><div class="stat-card"><div class="stat-title">成本</div><div class="stat-value money">{{ profit.cost || 0 }}</div></div></el-col><el-col :span="4"><div class="stat-card profit"><div class="stat-title">净利润</div><div class="stat-value money">{{ profit.netProfit || 0 }}</div></div></el-col><el-col :span="4"><div class="stat-card"><div class="stat-title">利润率</div><div class="stat-value money">{{ profit.profitRate || 0 }}%</div></div></el-col><el-col :span="4"><div class="stat-card"><div class="stat-title">已收</div><div class="stat-value money">{{ profit.collected || 0 }}</div></div></el-col><el-col :span="4"><div class="stat-card"><div class="stat-title">已付</div><div class="stat-value money">{{ profit.paid || 0 }}</div></div></el-col></el-row></div>

    <div class="card"><div class="section-title">KPI 仪表盘</div><el-row :gutter="16"><el-col :span="4" v-for="k in kpiCards" :key="k.title"><div class="gauge-card"><el-progress type="dashboard" :percentage="Number(k.value || 0)" :width="110"/><div class="gauge-title">{{ k.title }}</div></div></el-col></el-row></div>

    <div class="card"><div class="section-title">业务流程漏斗</div><el-table :data="funnel" border stripe size="small"><el-table-column prop="step" label="步骤" width="70"/><el-table-column prop="name" label="节点" width="140"/><el-table-column prop="count" label="数量" width="100"/><el-table-column prop="conversionRate" label="转化率%" width="110"/><el-table-column prop="dropCount" label="流失" width="100"/><el-table-column label="进度"><template #default="scope"><el-progress :percentage="Number(scope.row.conversionRate || 0)" :stroke-width="14"/></template></el-table-column></el-table></div>

    <div class="card"><div class="section-title">经营趋势</div><el-table :data="trends" border stripe size="small"><el-table-column prop="date" label="日期" width="160"/><el-table-column prop="sales" label="销售/收入"/><el-table-column prop="purchase" label="采购/成本"/><el-table-column prop="profit" label="利润"/><el-table-column prop="collected" label="收款"/><el-table-column prop="paid" label="付款"/></el-table></div>

    <el-row :gutter="16"><el-col :span="12"><div class="card"><div class="section-title">客户利润 TOP</div><el-table :data="customerRanking" border stripe size="small"><el-table-column prop="customerId" label="客户ID" width="90"/><el-table-column prop="income" label="收入"/><el-table-column prop="cost" label="成本"/><el-table-column prop="profit" label="利润"/><el-table-column prop="profitRate" label="利润率%" width="100"/></el-table></div></el-col><el-col :span="12"><div class="card"><div class="section-title">供应商采购 TOP</div><el-table :data="supplierRanking" border stripe size="small"><el-table-column prop="supplierId" label="供应商ID" width="100"/><el-table-column prop="purchaseAmount" label="采购额"/><el-table-column prop="paidAmount" label="已付"/><el-table-column prop="balance" label="余额"/></el-table></div></el-col></el-row>

    <div class="card"><div class="section-title">业务预警中心</div><el-row :gutter="16"><el-col :span="6"><el-alert :title="`总预警：${warnings.total || 0}`" type="info" show-icon /></el-col><el-col :span="6"><el-alert :title="`严重：${warnings.danger || 0}`" type="error" show-icon /></el-col><el-col :span="6"><el-alert :title="`提醒：${warnings.warning || 0}`" type="warning" show-icon /></el-col><el-col :span="6"><el-alert title="红色优先处理，黄色跟进" type="success" show-icon /></el-col></el-row><el-table :data="warnings.items || []" border stripe size="small" style="margin-top:12px"><el-table-column prop="type" label="类型" width="100"/><el-table-column prop="level" label="级别" width="90"><template #default="scope"><el-tag :type="scope.row.level==='danger'?'danger':'warning'">{{ scope.row.level==='danger'?'严重':'提醒' }}</el-tag></template></el-table-column><el-table-column prop="no" label="单号" width="180"/><el-table-column prop="message" label="说明" min-width="260"/><el-table-column prop="date" label="日期" width="160"/><el-table-column prop="amount" label="金额" width="120"/></el-table></div>

    <div class="card"><div class="section-title">今日业务</div><el-row :gutter="16"><el-col :span="3" v-for="item in todayCards" :key="item.title"><div class="stat-card small"><div class="stat-title">{{ item.title }}</div><div class="stat-value">{{ item.value }}</div></div></el-col></el-row></div>

    <div class="card"><div class="section-title">基础资料</div><el-row :gutter="16"><el-col :span="8"><div class="stat-card"><div class="stat-title">客户</div><div class="stat-value">{{ summary.masterData?.customers || 0 }}</div></div></el-col><el-col :span="8"><div class="stat-card"><div class="stat-title">供应商</div><div class="stat-value">{{ summary.masterData?.suppliers || 0 }}</div></div></el-col><el-col :span="8"><div class="stat-card"><div class="stat-title">商品</div><div class="stat-value">{{ summary.masterData?.products || 0 }}</div></div></el-col></el-row></div>

    <div class="card"><div class="section-title">财务总览</div><el-row :gutter="16"><el-col :span="4"><div class="stat-card"><div class="stat-title">应收</div><div class="stat-value money">{{ summary.finance?.receivableAmount || 0 }}</div></div></el-col><el-col :span="4"><div class="stat-card"><div class="stat-title">已收</div><div class="stat-value money">{{ summary.finance?.receivedAmount || 0 }}</div></div></el-col><el-col :span="4"><div class="stat-card warn"><div class="stat-title">未收</div><div class="stat-value money">{{ summary.finance?.receivableBalance || 0 }}</div></div></el-col><el-col :span="4"><div class="stat-card"><div class="stat-title">应付</div><div class="stat-value money">{{ summary.finance?.payableAmount || 0 }}</div></div></el-col><el-col :span="4"><div class="stat-card"><div class="stat-title">已付</div><div class="stat-value money">{{ summary.finance?.paidAmount || 0 }}</div></div></el-col><el-col :span="4"><div class="stat-card danger"><div class="stat-title">未付</div><div class="stat-value money">{{ summary.finance?.payableBalance || 0 }}</div></div></el-col></el-row></div>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { http } from '../api/http'
const summary = ref<any>({})
const warnings = ref<any>({})
const period = ref('month')
const profit = ref<any>({})
const kpi = ref<any>({})
const funnel = ref<any[]>([])
const trends = ref<any[]>([])
const customerRanking = ref<any[]>([])
const supplierRanking = ref<any[]>([])
const todayCards = computed(() => [{ title: 'RFQ', value: summary.value.today?.rfqs || 0 }, { title: 'CO', value: summary.value.today?.customerOrders || 0 }, { title: 'PO', value: summary.value.today?.purchaseOrders || 0 }, { title: 'SO', value: summary.value.today?.summaryOrders || 0 }, { title: '收货', value: summary.value.today?.receivingOrders || 0 }, { title: 'QC', value: summary.value.today?.qcOrders || 0 }, { title: '装柜', value: summary.value.today?.containerLoads || 0 }, { title: '出运', value: summary.value.today?.shipments || 0 }])
const kpiCards = computed(() => [{ title: '采购完成率', value: kpi.value.purchaseCompletionRate || 0 }, { title: 'QC合格率', value: kpi.value.qcPassRate || 0 }, { title: '出运完成率', value: kpi.value.shipmentDoneRate || 0 }, { title: '回款率', value: kpi.value.collectionRate || 0 }, { title: '付款率', value: kpi.value.paymentRate || 0 }, { title: '净利率', value: profit.value.profitRate || 0 }])
async function load(){
  const params={period:period.value}
  summary.value=(await http.get('/business-dashboard/summary')).data
  warnings.value=(await http.get('/business-dashboard/warnings')).data
  profit.value=(await http.get('/bi-reports/profit',{params})).data
  kpi.value=(await http.get('/bi-reports/kpi',{params})).data
  funnel.value=(await http.get('/bi-reports/funnel',{params})).data
  trends.value=(await http.get('/bi-reports/trends',{params})).data
  customerRanking.value=(await http.get('/bi-reports/customer-profit-ranking',{params:{period:period.value,top:10}})).data
  supplierRanking.value=(await http.get('/bi-reports/supplier-purchase-ranking',{params:{period:period.value,top:10}})).data
}
onMounted(load)
</script>

<style scoped>
.toolbar{display:flex;gap:8px;align-items:center}.section-title { font-weight: 800; margin-bottom: 12px; }
.stat-card { min-height: 92px; padding: 14px; border: 1px solid #e5e7eb; border-radius: 12px; background: #fff; }
.stat-card.small { min-height: 76px; }
.stat-card.warn { background:#fff7ed; }
.stat-card.danger { background:#fef2f2; }
.stat-card.profit { background:#ecfdf5; }
.stat-title { color: #6b7280; font-size: 14px; }
.stat-value { margin-top: 10px; font-size: 28px; font-weight: 800; color: #111827; }
.stat-value.money { font-size: 22px; }
.gauge-card{display:flex;flex-direction:column;align-items:center;gap:8px;border:1px solid #e5e7eb;border-radius:12px;padding:12px;background:#fff}.gauge-title{font-weight:700;color:#374151}
</style>
