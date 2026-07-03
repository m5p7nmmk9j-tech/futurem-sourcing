<template>
  <el-container class="layout">
    <el-aside width="240px" class="aside">
      <div class="brand">FUTUREM Sourcing</div>
      <el-menu router default-active="/" class="menu">
        <el-menu-item index="/">首页 Dashboard</el-menu-item>
        <el-menu-item index="/products">商品管理</el-menu-item>
        <el-menu-item index="/customers">客户管理</el-menu-item>
        <el-menu-item index="/suppliers">供应商管理</el-menu-item>
        <el-menu-item index="/markets">市场管理</el-menu-item>
        <el-menu-item index="/rfqs">RFQ 客户询价</el-menu-item>
        <el-menu-item index="/customer-orders">CO 客户订单</el-menu-item>
        <el-menu-item index="/purchase-orders">PO 采购订单</el-menu-item>
        <el-menu-item index="/so-orders">SO 汇总单</el-menu-item>
      </el-menu>
    </el-aside>
    <el-container>
      <el-header class="header">
        <div class="search">全局搜索：客户 / 供应商 / SKU / RFQ / CO / PO / SO</div>
        <div>V0.1 Alpha</div>
      </el-header>
      <el-main><router-view /></el-main>
    </el-container>
  </el-container>
</template>
<style scoped>
.layout { height: 100vh; }
.aside { background: #111827; color: #fff; }
.brand { height: 64px; display: flex; align-items: center; padding: 0 18px; font-size: 18px; font-weight: 800; }
.menu { border-right: 0; background: #111827; }
:deep(.el-menu-item) { color: #d1d5db; }
:deep(.el-menu-item.is-active) { color: #fff; background: #2563eb; }
.header { display: flex; align-items: center; justify-content: space-between; background: #fff; border-bottom: 1px solid #e5e7eb; }
.search { color: #6b7280; }
</style>
