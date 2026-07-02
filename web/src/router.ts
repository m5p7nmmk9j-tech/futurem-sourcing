import { createRouter, createWebHistory } from 'vue-router'
import MainLayout from './layouts/MainLayout.vue'
import Dashboard from './views/Dashboard.vue'
import Customers from './views/Customers.vue'
import Suppliers from './views/Suppliers.vue'
import Products from './views/Products.vue'
import Markets from './views/Markets.vue'

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
        { path: 'markets', name: 'markets', component: Markets }
      ]
    }
  ]
})

export default router
