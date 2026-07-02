<template>
  <div class="page">
    <div class="page-header">
      <div class="page-title">首页 Dashboard</div>
    </div>
    <el-row :gutter="16">
      <el-col :span="6" v-for="item in cards" :key="item.title">
        <div class="card stat-card">
          <div class="stat-title">{{ item.title }}</div>
          <div class="stat-value">{{ item.value }}</div>
        </div>
      </el-col>
    </el-row>
  </div>
</template>

<script setup lang="ts">
const cards = [
  { title: '待询价 RFQ', value: 0 },
  { title: '待采购 PO', value: 0 },
  { title: '待收货', value: 0 },
  { title: '待装柜', value: 0 }
]
</script>

<style scoped>
.stat-card { min-height: 110px; }
.stat-title { color: #6b7280; font-size: 14px; }
.stat-value { margin-top: 16px; font-size: 32px; font-weight: 800; color: #111827; }
</style>
