<template>
  <div class="page">
    <div class="page-header">
      <div>
        <div class="page-title">客户历史商品</div>
        <div class="subtitle">历史商品只用于查询和复制；复制后生成新的订单商品记录，不覆盖原数据。</div>
      </div>
      <el-button @click="load">刷新</el-button>
    </div>

    <div class="card">
      <div class="toolbar">
        <el-select v-model="filters.customerId" clearable filterable placeholder="客户" style="width:220px">
          <el-option v-for="item in customers" :key="item.id" :label="item.name" :value="item.id" />
        </el-select>
        <el-select v-model="filters.supplierId" clearable filterable placeholder="商品供应商" style="width:220px">
          <el-option v-for="item in suppliers" :key="item.id" :label="item.name" :value="item.id" />
        </el-select>
        <el-input v-model="filters.keyword" clearable placeholder="客户货号 / 条码 / SKU / 名称" style="width:300px" @keyup.enter="load" />
        <el-button type="primary" @click="load">查询</el-button>
      </div>

      <el-table :data="rows" border stripe>
        <el-table-column label="图片" width="86" align="center">
          <template #default="scope"><el-image v-if="scope.row.mainImageUrl" :src="scope.row.mainImageUrl" :preview-src-list="[scope.row.mainImageUrl]" fit="cover" preview-teleported class="image" /></template>
        </el-table-column>
        <el-table-column prop="product.customerItemNo" label="客户货号" width="140" />
        <el-table-column prop="product.customerBarcode" label="客户条码" width="160" />
        <el-table-column prop="product.systemSku" label="系统 SKU" width="150" />
        <el-table-column prop="product.nameCn" label="商品名称" min-width="180" />
        <el-table-column prop="product.supplierId" label="供应商 ID" width="105" />
        <el-table-column label="采购价" width="115" align="right"><template #default="scope">{{ formatRmb(scope.row.product.purchaseUnitPrice) }}</template></el-table-column>
        <el-table-column label="销售价" width="115" align="right"><template #default="scope">{{ formatRmb(scope.row.product.salesUnitPrice) }}</template></el-table-column>
        <el-table-column prop="product.cartonQty" label="单箱数量" width="100" />
        <el-table-column prop="product.batchCode" label="批次" width="110" />
        <el-table-column prop="product.sourceCustomerOrderId" label="来源 CO ID" width="110" />
        <el-table-column prop="product.status" label="状态" width="100" />
      </el-table>
    </div>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { http } from '../api/http'
import { formatRmb } from '../utils/rmb'

const rows = ref<any[]>([])
const customers = ref<any[]>([])
const suppliers = ref<any[]>([])
const filters = reactive({ customerId: null as number | null, supplierId: null as number | null, keyword: '' })

async function load() {
  const response = await http.get('/order-products/history', {
    params: {
      customerId: filters.customerId || undefined,
      supplierId: filters.supplierId || undefined,
      keyword: filters.keyword || undefined,
    },
  })
  rows.value = response.data || []
}

onMounted(async () => {
  const [customerResponse, supplierResponse] = await Promise.all([
    http.get('/customers'),
    http.get('/suppliers'),
  ])
  customers.value = customerResponse.data || []
  suppliers.value = supplierResponse.data || []
  await load()
})
</script>

<style scoped>
.subtitle { color:#64748b; margin-top:4px; font-size:13px; }
.toolbar { display:flex; gap:10px; margin-bottom:14px; }
.image { width:56px; height:56px; border-radius:8px; }
</style>
