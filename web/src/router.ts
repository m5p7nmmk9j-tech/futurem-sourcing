import { createRouter, createWebHistory } from 'vue-router'
import MainLayout from './layouts/MainLayout.vue'
import Dashboard from './views/Dashboard.vue'
import Customers from './views/Customers.vue'
import Suppliers from './views/Suppliers.vue'
import Products from './views/Products.vue'
import Markets from './views/Markets.vue'
import Rfqs from './views/Rfqs.vue'
import CustomerOrders from './views/CustomerOrders.vue'
import PurchaseOrders from './views/PurchaseOrders.vue'
import SummaryOrders from './views/SummaryOrders.vue'
import ReceivingOrders from './views/ReceivingOrders.vue'
import QcOrders from './views/QcOrders.vue'
import ContainerLoads from './views/ContainerLoads.vue'
import Shipments from './views/Shipments.vue'
import FinanceRecords from './views/FinanceRecords.vue'
import BankAccounts from './views/BankAccounts.vue'
import BiReports from './views/BiReports.vue'
import Notifications from './views/Notifications.vue'
import Approvals from './views/Approvals.vue'
import Rbac from './views/Rbac.vue'
import AuditLogs from './views/AuditLogs.vue'
import PrintCenter from './views/PrintCenter.vue'
import ExcelCenter from './views/ExcelCenter.vue'
import SystemSettings from './views/SystemSettings.vue'

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
        { path: 'products', component: Products },
        { path: 'markets', component: Markets },
        { path: 'rfqs', component: Rfqs },
        { path: 'customer-orders', component: CustomerOrders },
        { path: 'purchase-orders', component: PurchaseOrders },
        { path: 'so-orders', component: SummaryOrders },
        { path: 'receiving-orders', component: ReceivingOrders },
        { path: 'qc-orders', component: QcOrders },
        { path: 'container-loads', component: ContainerLoads },
        { path: 'shipments', component: Shipments },
        { path: 'finance-records', component: FinanceRecords },
        { path: 'bank-accounts', component: BankAccounts },
        { path: 'bi-reports', component: BiReports },
        { path: 'message-center', component: Notifications },
        { path: 'approvals', component: Approvals },
        { path: 'rbac', component: Rbac },
        { path: 'audit-logs', component: AuditLogs },
        { path: 'print-center', component: PrintCenter },
        { path: 'excel-center', component: ExcelCenter },
        { path: 'system-settings', component: SystemSettings }
      ]
    }
  ]
})

export default router
