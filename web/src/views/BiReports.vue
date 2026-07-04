<template>
  <div class="page">
    <div class="page-header">
      <div class="page-title">经营分析 BI</div>
      <div class="toolbar">
        <el-select v-model="period" style="width:140px" @change="load">
          <el-option label="今日" value="today"/><el-option label="本周" value="week"/><el-option label="本月" value="month"/><el-option label="本季度" value="quarter"/><el-option label="本年" value="year"/><el-option label="全部" value="all"/>
        </el-select>
        <el-button @click="load">刷新</el-button>
      </div>
    </div>

    <div class="card">
      <div class="section-title">利润分析</div>
      <el-row :gutter="12">
        <el-col :span="3"><el-statistic title="收入" :value="profit.income || 0" /></el-col>
        <el-col :span="3"><el-statistic title="成本" :value="profit.cost || 0" /></el-col>
        <el-col :span="3"><el-statistic title="应收" :value="profit.receivable || 0" /></el-col>
        <el-col :span="3"><el-statistic title="应付" :value="profit.payable || 0" /></el-col>
        <el-col :span="3"><el-statistic title="费用" :value="profit.expense || 0" /></el-col>
        <el-col :span="3"><el-statistic title="净利润" :value="profit.netProfit || 0" /></el-col>
        <el-col :span="3"><el-statistic title="利润率%" :value="profit.profitRate || 0" /></el-col>
        <el-col :span="3"><el-statistic title="已收/已付" :value="`${profit.collected || 0}/${profit.paid || 0}`" /></el-col>
      </el-row>
    </div>

    <div class="card">
      <div class="section-title">KPI 看板</div>
      <el-row :gutter="12">
        <el-col :span="4"><el-statistic title="采购完成率%" :value="kpi.purchaseCompletionRate || 0" /></el-col>
        <el-col :span="4"><el-statistic title="QC合格率%" :value="kpi.qcPassRate || 0" /></el-col>
        <el-col :span="4"><el-statistic title="出运完成率%" :value="kpi.shipmentDoneRate || 0" /></el-col>
        <el-col :span="4"><el-statistic title="回款率%" :value="kpi.collectionRate || 0" /></el-col>
        <el-col :span="4"><el-statistic title="付款率%" :value="kpi.paymentRate || 0" /></el-col>
        <el-col :span="4"><el-statistic title="净利率%" :value="profit.profitRate || 0" /></el-col>
      </el-row>
    </div>

    <div class="card">
      <div class="section-title">业务流程漏斗</div>
      <el-table :data="funnel" border stripe size="small">
        <el-table-column prop="step" label="步骤" width="70" />
        <el-table-column prop="name" label="节点" width="140" />
        <el-table-column prop="count" label="数量" width="110" />
        <el-table-column prop="conversionRate" label="相对RFQ转化率%" width="160" />
        <el-table-column prop="dropCount" label="上一节点流失" width="140" />
        <el-table-column label="进度" min-width="260">
          <template #default="scope"><el-progress :percentage="Number(scope.row.conversionRate || 0)" :stroke-width="14" /></template>
        </el-table-column>
      </el-table>
    </div>

    <div class="card">
      <div class="section-title">趋势分析</div>
      <el-table :data="trends" border stripe size="small">
        <el-table-column prop="date" label="日期" width="160" />
        <el-table-column prop="sales" label="销售/收入" width="140" />
        <el-table-column prop="purchase" label="采购/成本" width="140" />
        <el-table-column prop="profit" label="利润" width="140" />
        <el-table-column prop="collected" label="已收" width="140" />
        <el-table-column prop="paid" label="已付" width="140" />
      </el-table>
    </div>

    <el-row :gutter="12">
      <el-col :span="12">
        <div class="card"><div class="section-title">客户利润 TOP20</div>
          <el-table :data="customerRanking" border stripe size="small">
            <el-table-column prop="customerId" label="客户ID" width="100" />
            <el-table-column prop="income" label="收入" width="120" />
            <el-table-column prop="cost" label="成本" width="120" />
            <el-table-column prop="profit" label="利润" width="120" />
            <el-table-column prop="profitRate" label="利润率%" width="110" />
            <el-table-column prop="count" label="笔数" width="80" />
          </el-table>
        </div>
      </el-col>
      <el-col :span="12">
        <div class="card"><div class="section-title">供应商采购 TOP20</div>
          <el-table :data="supplierRanking" border stripe size="small">
            <el-table-column prop="supplierId" label="供应商ID" width="110" />
            <el-table-column prop="purchaseAmount" label="采购额" width="130" />
            <el-table-column prop="paidAmount" label="已付" width="120" />
            <el-table-column prop="balance" label="余额" width="120" />
            <el-table-column prop="count" label="笔数" width="80" />
          </el-table>
        </div>
      </el-col>
    </el-row>

    <div class="card">
      <div class="section-title">商品销售 TOP20</div>
      <el-table :data="productRanking" border stripe size="small">
        <el-table-column prop="sku" label="SKU" width="160" />
        <el-table-column prop="productName" label="商品" min-width="240" />
        <el-table-column prop="quantity" label="数量" width="120" />
        <el-table-column prop="amount" label="金额" width="120" />
        <el-table-column prop="cartons" label="箱数" width="100" />
        <el-table-column prop="cbm" label="CBM" width="100" />
        <el-table-column prop="kg" label="KG" width="100" />
      </el-table>
    </div>
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { http } from '../api/http'
const period = ref('month')
const profit = ref<any>({})
const kpi = ref<any>({})
const trends = ref<any[]>([])
const funnel = ref<any[]>([])
const customerRanking = ref<any[]>([])
const supplierRanking = ref<any[]>([])
const productRanking = ref<any[]>([])
async function load(){
  const params={period:period.value}
  profit.value=(await http.get('/bi-reports/profit',{params})).data
  kpi.value=(await http.get('/bi-reports/kpi',{params})).data
  funnel.value=(await http.get('/bi-reports/funnel',{params})).data
  trends.value=(await http.get('/bi-reports/trends',{params})).data
  customerRanking.value=(await http.get('/bi-reports/customer-profit-ranking',{params})).data
  supplierRanking.value=(await http.get('/bi-reports/supplier-purchase-ranking',{params})).data
  productRanking.value=(await http.get('/bi-reports/product-ranking',{params:{documentType:'SO'}})).data
}
onMounted(load)
</script>

<style scoped>
.section-title{font-weight:800;margin-bottom:12px}.card{margin-bottom:14px}.toolbar{display:flex;gap:8px;align-items:center}
</style>
