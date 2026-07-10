<template>
  <el-container class="layout">
    <el-aside width="240px" class="aside">
      <div class="brand">FUTUREM Sourcing</div>
      <el-menu
        router
        default-active="/"
        class="menu"
        background-color="#f8fbff"
        text-color="#334155"
        active-text-color="#1d4ed8"
      >
        <el-menu-item index="/">Dashboard</el-menu-item>
        <el-menu-item index="/bi-reports">BI 经营分析</el-menu-item>
        <el-menu-item index="/message-center">消息中心</el-menu-item>
        <el-menu-item index="/approvals">审批流</el-menu-item>
        <el-menu-item index="/rbac">权限管理</el-menu-item>
        <el-menu-item index="/audit-logs">操作日志</el-menu-item>
        <el-menu-item index="/print-center">打印中心</el-menu-item>
        <el-menu-item index="/excel-center">Excel中心</el-menu-item>
        <el-menu-item index="/system-settings">系统参数</el-menu-item>
        <el-menu-item index="/backup-center">备份中心</el-menu-item>
        <el-menu-item index="/monitor-center">系统监控</el-menu-item>
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
        <el-menu-item index="/finance-records">Finance</el-menu-item>
        <el-menu-item index="/bank-accounts">Accounts</el-menu-item>
      </el-menu>
    </el-aside>
    <el-container>
      <el-header class="header">
        <div class="search-wrap">
          <el-autocomplete
            v-model="keyword"
            :fetch-suggestions="querySearch"
            placeholder="全局搜索：SKU / 客户 / 供应商 / PO / SO / 柜号 / 出运 / 财务"
            value-key="label"
            clearable
            style="width: 520px"
            @select="selectResult"
            @keyup.enter="openSearchDialog"
          >
            <template #default="{ item }">
              <div class="search-item">
                <el-tag size="small">{{ item.type }}</el-tag>
                <span class="search-no">{{ item.no }}</span>
                <span class="search-title">{{ item.title }}</span>
              </div>
            </template>
          </el-autocomplete>
          <el-button @click="openSearchDialog">搜索</el-button>
        </div>
        <div>V1.0 RC</div>
      </el-header>
      <el-main><router-view /></el-main>
    </el-container>

    <el-dialog v-model="searchVisible" title="全局搜索结果" width="860px">
      <el-table :data="searchRows" border stripe>
        <el-table-column prop="type" label="类型" width="100" />
        <el-table-column prop="no" label="编号" width="180" />
        <el-table-column prop="title" label="标题" min-width="260" />
        <el-table-column prop="id" label="ID" width="90" />
        <el-table-column label="操作" width="120"><template #default="scope"><el-button size="small" type="primary" @click="go(scope.row)">进入</el-button></template></el-table-column>
      </el-table>
    </el-dialog>
  </el-container>
</template>
<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { http } from '../api/http'
const router = useRouter()
const keyword = ref('')
const searchVisible = ref(false)
const searchRows = ref<any[]>([])
let timer:any = null
async function search(){
  const kw = keyword.value.trim()
  if(!kw){ searchRows.value=[]; return [] }
  const res = await http.get('/global-search',{params:{keyword:kw,top:8}})
  searchRows.value = res.data.items || []
  return searchRows.value
}
function querySearch(q:string, cb:any){
  keyword.value = q
  clearTimeout(timer)
  timer=setTimeout(async()=>{
    const rows = await search()
    cb(rows.map((x:any)=>({...x,label:`${x.type} ${x.no} ${x.title}`})))
  },250)
}
async function openSearchDialog(){ await search(); searchVisible.value=true }
function selectResult(item:any){ go(item) }
function go(row:any){ searchVisible.value=false; if(row.route) router.push(row.route) }
</script>
<style scoped>
.layout { height: 100vh; background: #f5f7fb; }
.aside {
  background: #f8fbff !important;
  border-right: 1px solid #dbe7f6;
  box-shadow: 8px 0 24px rgba(37, 99, 235, 0.08);
  color: #1f2937;
}
.brand {
  height: 64px;
  display: flex;
  align-items: center;
  margin: 12px 14px 8px;
  padding: 0 14px;
  border-radius: 16px;
  background: linear-gradient(135deg, #2563eb 0%, #38bdf8 100%);
  color: #fff;
  font-size: 18px;
  font-weight: 800;
  letter-spacing: .2px;
  box-shadow: 0 12px 26px rgba(37, 99, 235, .22);
}
.menu {
  --el-menu-bg-color: #f8fbff;
  --el-menu-text-color: #334155;
  --el-menu-active-color: #1d4ed8;
  --el-menu-hover-bg-color: #e0efff;
  border-right: 0;
  padding: 4px 12px 18px;
  background: #f8fbff !important;
}
.menu :deep(.el-menu-item) {
  height: 40px;
  margin: 4px 0;
  border-radius: 12px;
  background: transparent !important;
  color: #334155 !important;
  font-weight: 600;
}
.menu :deep(.el-menu-item:hover) {
  background: #e0efff !important;
  color: #1d4ed8 !important;
}
.menu :deep(.el-menu-item.is-active) {
  background: #e8f1ff !important;
  color: #1d4ed8 !important;
  box-shadow: 0 10px 18px rgba(37, 99, 235, .22);
}
.header { display: flex; align-items: center; justify-content: space-between; background: #fff; border-bottom: 1px solid #e5e7eb; }
.search-wrap{display:flex;gap:8px;align-items:center}.search-item{display:flex;align-items:center;gap:8px}.search-no{font-weight:700}.search-title{color:#6b7280}
</style>
