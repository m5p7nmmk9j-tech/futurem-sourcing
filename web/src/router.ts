import { createRouter, createWebHistory } from 'vue-router'
import MainLayout from './layouts/MainLayout.vue'
import Dashboard from './views/Dashboard.vue'
import Customers from './views/Customers.vue'
import Suppliers from './views/Suppliers.vue'
import LogisticsProviders from './views/LogisticsProviders.vue'
import CustomerHistoryProducts from './views/CustomerHistoryProducts.vue'
import CustomerImporterProfiles from './views/CustomerImporterProfiles.vue'
import LabelMarkTemplates from './views/LabelMarkTemplates.vue'
import Rfqs from './views/Rfqs.vue'
import CustomerOrders from './views/CustomerOrders.vue'
import PurchaseOrders from './views/PurchaseOrders.vue'
import SummaryOrders from './views/SummaryOrders.vue'
import DeliveryNotices from './views/DeliveryNotices.vue'
import ReceivingOrders from './views/ReceivingOrders.vue'
import QcOrders from './views/QcOrders.vue'
import Warehouses from './views/Warehouses.vue'
import Inventory from './views/Inventory.vue'
import ContainerLoads from './views/ContainerLoads.vue'
import Shipments from './views/Shipments.vue'
import FinanceRecords from './views/FinanceRecordsUnified.vue'
import FinancialAdjustments from './views/FinancialAdjustments.vue'
import BankAccounts from './views/BankAccounts.vue'
import BiReports from './views/BiReports.vue'
import Notifications from './views/Notifications.vue'
import Approvals from './views/Approvals.vue'
import Rbac from './views/Rbac.vue'
import AuditLogs from './views/AuditLogs.vue'
import PrintCenter from './views/PrintCenter.vue'
import ExcelCenter from './views/ExcelCenter.vue'
import SystemSettings from './views/SystemSettings.vue'
import BackupCenter from './views/BackupCenter.vue'
import MonitorCenter from './views/MonitorCenter.vue'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/',
      component: MainLayout,
      children: [
        { path: '', component: Dashboard },
        { path: 'customers', component: Customers },
        { path: 'suppliers', component: Suppliers },
        { path: 'logistics-providers', component: LogisticsProviders },
        { path: 'products', redirect: '/customer-history-products' },
        { path: 'customer-history-products', component: CustomerHistoryProducts },
        { path: 'customer-importers', component: CustomerImporterProfiles },
        { path: 'label-mark-templates', component: LabelMarkTemplates },
        { path: 'rfqs', component: Rfqs },
        { path: 'customer-orders', component: CustomerOrders },
        { path: 'purchase-orders', component: PurchaseOrders },
        { path: 'so-orders', component: SummaryOrders },
        { path: 'delivery-notices', component: DeliveryNotices },
        { path: 'receiving-orders', component: ReceivingOrders },
        { path: 'qc-orders', component: QcOrders },
        { path: 'warehouses', component: Warehouses },
        { path: 'inventory', component: Inventory },
        { path: 'container-loads', component: ContainerLoads },
        { path: 'shipments', component: Shipments },
        { path: 'finance-records', component: FinanceRecords },
        { path: 'financial-adjustments', component: FinancialAdjustments },
        { path: 'bank-accounts', component: BankAccounts },
        { path: 'bi-reports', component: BiReports },
        { path: 'message-center', component: Notifications },
        { path: 'approvals', component: Approvals },
        { path: 'rbac', component: Rbac },
        { path: 'audit-logs', component: AuditLogs },
        { path: 'print-center', component: PrintCenter },
        { path: 'excel-center', component: ExcelCenter },
        { path: 'system-settings', component: SystemSettings },
        { path: 'backup-center', component: BackupCenter },
        { path: 'monitor-center', component: MonitorCenter }
      ]
    }
  ]
})

export default router
