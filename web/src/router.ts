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
        { path: 'qc-orders', component: QcOrders }
      ]
    }
  ]
})

export default router
