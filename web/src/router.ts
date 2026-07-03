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

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/',
      component: MainLayout,
      children: [
        { path: '', name: 'dashboard', component: Dashboard },
        { path: 'customers', name: 'customers', component: Customers },
        { path: 'suppliers', name: 'suppliers', component: Suppliers },
        { path: 'products', name: 'products', component: Products },
        { path: 'markets', name: 'markets', component: Markets },
        { path: 'rfqs', name: 'rfqs', component: Rfqs },
        { path: 'customer-orders', name: 'customer-orders', component: CustomerOrders },
        { path: 'purchase-orders', name: 'purchase-orders', component: PurchaseOrders },
        { path: 'so-orders', name: 'so-orders', component: SummaryOrders },
        { path: 'receiving-orders', name: 'receiving-orders', component: ReceivingOrders }
      ]
    }
  ]
})

export default router
