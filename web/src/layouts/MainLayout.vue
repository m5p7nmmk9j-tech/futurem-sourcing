<template>
  <el-container class="layout">
    <el-aside width="240px" class="aside">
      <div class="brand">FUTUREM Sourcing</div>
      <el-menu router default-active="/" class="menu">
        <el-menu-item index="/">Dashboard</el-menu-item>
        <el-menu-item index="/products">Products</el-menu-item>
        <el-menu-item index="/customers">Customers</el-menu-item>
        <el-menu-item index="/suppliers">Suppliers</el-menu-item>
        <el-menu-item index="/markets">Markets</el-menu-item>
        <el-menu-item index="/rfqs">RFQ</el-menu-item>
        <el-menu-item index="/customer-orders">CO</el-menu-item>
        <el-menu-item index="/purchase-orders">PO</el-menu-item>
        <el-menu-item index="/so-orders">SO</el-menu-item>
        <el-menu-item index="/receiving-orders">Receiving</el-menu-item>
        <el-menu-item index="/qc-orders">QC</el-menu-item>
        <el-menu-item index="/container-loads">Container</el-menu-item>
        <el-menu-item index="/shipments">Shipment</el-menu-item>
      </el-menu>
    </el-aside>
    <el-container>
      <el-header class="header"><div class="search">Global Search</div><div>V0.1 Alpha</div></el-header>
      <el-main><router-view /></el-main>
    </el-container>
  </el-container>
</template>
<style scoped>
.layout { height: 100vh; }
.aside { background: #111827; color: #fff; }
.brand { height: 64px; display: flex; align-items: center; padding: 0 18px; font-size: 18px; font-weight: 800; }
.menu { border-right: 0; background: #111827; }
.header { display: flex; align-items: center; justify-content: space-between; background: #fff; border-bottom: 1px solid #e5e7eb; }
.search { color: #6b7280; }
</style>
