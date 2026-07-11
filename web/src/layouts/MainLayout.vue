<template>
  <el-container class="layout">
    <el-aside width="250px" class="aside">
      <div class="brand">FUTUREM Sourcing</div>
      <el-scrollbar class="menu-scroll">
        <el-menu
          router
          :default-active="route.path"
          class="menu"
          background-color="#f8fbff"
          text-color="#334155"
          active-text-color="#1d4ed8"
        >
          <el-menu-item index="/">经营看板</el-menu-item>
          <el-menu-item index="/bi-reports">BI 经营分析</el-menu-item>

          <el-sub-menu index="master-data">
            <template #title>基础资料</template>
            <el-menu-item index="/customers">客户管理</el-menu-item>
            <el-menu-item index="/customer-importers">客户进口商资料</el-menu-item>
            <el-menu-item index="/suppliers">商品供应商</el-menu-item>
            <el-menu-item index="/customer-history-products">客户历史商品</el-menu-item>
            <el-menu-item index="/label-mark-templates">标签与唛头模板</el-menu-item>
          </el-sub-menu>

          <el-sub-menu index="business-flow">
            <template #title>采购与出运</template>
            <el-menu-item index="/rfqs">询价 RFQ</el-menu-item>
            <el-menu-item index="/customer-orders">客户订单 CO</el-menu-item>
            <el-menu-item index="/purchase-orders">采购订单 PO</el-menu-item>
            <el-menu-item index="/so-orders">客户汇总单</el-menu-item>
            <el-menu-item index="/delivery-notices">供应商送货通知</el-menu-item>
            <el-menu-item index="/receiving-orders">收货单</el-menu-item>
            <el-menu-item index="/qc-orders">验货单</el-menu-item>
            <el-menu-item index="/container-loads">装柜单</el-menu-item>
            <el-menu-item index="/shipments">出运单</el-menu-item>
          </el-sub-menu>

          <el-sub-menu index="finance">
            <template #title>财务管理</template>
            <el-menu-item index="/finance-records">应收应付</el-menu-item>
            <el-menu-item index="/bank-accounts">资金账户</el-menu-item>
          </el-sub-menu>

          <el-sub-menu index="platform">
            <template #title>系统管理</template>
            <el-menu-item index="/message-center">消息中心</el-menu-item>
            <el-menu-item index="/approvals">审批流</el-menu-item>
            <el-menu-item index="/rbac">权限管理</el-menu-item>
            <el-menu-item index="/audit-logs">操作日志</el-menu-item>
            <el-menu-item index="/print-center">打印中心</el-menu-item>
            <el-menu-item index="/excel-center">Excel 中心</el-menu-item>
            <el-menu-item index="/system-settings">系统参数</el-menu-item>
            <el-menu-item index="/backup-center">备份中心</el-menu-item>
            <el-menu-item index="/monitor-center">系统监控</el-menu-item>
          </el-sub-menu>
        </el-menu>
      </el-scrollbar>
    </el-aside>
    <el-container>
      <el-header class="header">
        <div class="search-wrap">
          <el-autocomplete
            v-model="keyword"
            :fetch-suggestions="querySearch"
            placeholder="全局搜索：客户条码 / PO / 汇总单 / 送货通知 / 柜号 / 出运 / 财务"
            value-key="label"
            clearable
            style="width: 570px"
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
        <div class="version">V2.0 · RMB</div>
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
import { useRoute, useRouter } from 'vue-router'
import { http } from '../api/http'

const route = useRoute()
const router = useRouter()
const keyword = ref('')
const searchVisible = ref(false)
const searchRows = ref<any[]>([])
let timer: ReturnType<typeof setTimeout> | null = null

async function search() {
  const term = keyword.value.trim()
  if (!term) { searchRows.value = []; return [] }
  const response = await http.get('/global-search', { params: { keyword: term, top: 8 } })
  searchRows.value = response.data.items || []
  return searchRows.value
}
function querySearch(query: string, callback: (rows: any[]) => void) {
  keyword.value = query
  if (timer) clearTimeout(timer)
  timer = setTimeout(async () => {
    const rows = await search()
    callback(rows.map((item: any) => ({ ...item, label: `${item.type} ${item.no} ${item.title}` })))
  }, 250)
}
async function openSearchDialog() { await search(); searchVisible.value = true }
function selectResult(item: any) { go(item) }
function go(row: any) { searchVisible.value = false; if (row.route) router.push(row.route) }
</script>

<style scoped>
.layout { height: 100vh; background: #f5f7fb; }
.aside { background: #f8fbff !important; border-right: 1px solid #dbe7f6; box-shadow: 8px 0 24px rgba(37, 99, 235, 0.08); color: #1f2937; }
.brand { height: 64px; display:flex; align-items:center; margin:12px 14px 8px; padding:0 14px; border-radius:16px; background:linear-gradient(135deg,#2563eb 0%,#38bdf8 100%); color:#fff; font-size:18px; font-weight:800; letter-spacing:.2px; box-shadow:0 12px 26px rgba(37,99,235,.22); }
.menu-scroll { height: calc(100vh - 84px); }
.menu { --el-menu-bg-color:#f8fbff; --el-menu-text-color:#334155; --el-menu-active-color:#1d4ed8; --el-menu-hover-bg-color:#e0efff; border-right:0; padding:4px 12px 18px; background:#f8fbff !important; }
.menu :deep(.el-menu-item), .menu :deep(.el-sub-menu__title) { height:40px; margin:4px 0; border-radius:12px; color:#334155 !important; font-weight:600; }
.menu :deep(.el-menu-item:hover), .menu :deep(.el-sub-menu__title:hover) { background:#e0efff !important; color:#1d4ed8 !important; }
.menu :deep(.el-menu-item.is-active) { background:#e8f1ff !important; color:#1d4ed8 !important; box-shadow:0 10px 18px rgba(37,99,235,.18); }
.header { display:flex; align-items:center; justify-content:space-between; background:#fff; border-bottom:1px solid #e5e7eb; }
.search-wrap { display:flex; gap:8px; align-items:center; }
.search-item { display:flex; align-items:center; gap:8px; }
.search-no { font-weight:700; }
.search-title { color:#6b7280; }
.version { color:#64748b; font-weight:600; }
</style>
