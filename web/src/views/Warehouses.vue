<template>
  <div class="page">
    <div class="page-header">
      <div>
        <div class="page-title">仓库与库位</div>
        <div class="page-subtitle">维护仓库基础资料，并在仓库下管理库位。</div>
      </div>
      <el-button type="primary" @click="openWarehouse()">新增仓库</el-button>
    </div>

    <div class="card">
      <el-table :data="rows" border stripe>
        <el-table-column prop="warehouse.code" label="仓库编码" width="130" />
        <el-table-column prop="warehouse.name" label="仓库名称" min-width="180" />
        <el-table-column prop="warehouse.address" label="地址" min-width="240" />
        <el-table-column prop="warehouse.contactName" label="联系人" width="120" />
        <el-table-column prop="warehouse.contactPhone" label="电话" width="150" />
        <el-table-column prop="locationCount" label="库位数" width="90" />
        <el-table-column label="状态" width="100">
          <template #default="scope"><el-tag :type="scope.row.warehouse.status === 'active' ? 'success' : 'info'">{{ scope.row.warehouse.status === 'active' ? '启用' : '停用' }}</el-tag></template>
        </el-table-column>
        <el-table-column label="操作" width="220" fixed="right">
          <template #default="scope">
            <el-button size="small" @click="openWarehouse(scope.row.warehouse)">编辑</el-button>
            <el-button size="small" type="primary" @click="openLocations(scope.row.warehouse)">库位</el-button>
          </template>
        </el-table-column>
      </el-table>
    </div>

    <el-dialog v-model="warehouseVisible" :title="warehouseForm.id ? '编辑仓库' : '新增仓库'" width="620px">
      <el-form label-width="100px">
        <el-form-item label="仓库编码"><el-input v-model="warehouseForm.code" /></el-form-item>
        <el-form-item label="仓库名称"><el-input v-model="warehouseForm.name" /></el-form-item>
        <el-form-item label="地址"><el-input v-model="warehouseForm.address" type="textarea" /></el-form-item>
        <el-form-item label="联系人"><el-input v-model="warehouseForm.contactName" /></el-form-item>
        <el-form-item label="电话"><el-input v-model="warehouseForm.contactPhone" /></el-form-item>
        <el-form-item label="状态"><el-select v-model="warehouseForm.status"><el-option label="启用" value="active" /><el-option label="停用" value="inactive" /></el-select></el-form-item>
      </el-form>
      <template #footer><el-button @click="warehouseVisible = false">取消</el-button><el-button type="primary" @click="saveWarehouse">保存</el-button></template>
    </el-dialog>

    <el-dialog v-model="locationVisible" :title="`${currentWarehouse?.name || ''} - 库位管理`" width="900px">
      <div class="toolbar"><el-button type="primary" @click="openLocation()">新增库位</el-button></div>
      <el-table :data="locations" border stripe size="small">
        <el-table-column prop="code" label="库位编码" width="120" />
        <el-table-column prop="name" label="库位名称" min-width="150" />
        <el-table-column prop="zone" label="区域" width="100" />
        <el-table-column prop="aisle" label="巷道" width="100" />
        <el-table-column prop="rack" label="货架" width="100" />
        <el-table-column prop="bin" label="货位" width="100" />
        <el-table-column prop="status" label="状态" width="90" />
        <el-table-column label="操作" width="100"><template #default="scope"><el-button size="small" @click="openLocation(scope.row)">编辑</el-button></template></el-table-column>
      </el-table>
    </el-dialog>

    <el-dialog v-model="locationEditVisible" :title="locationForm.id ? '编辑库位' : '新增库位'" width="560px" append-to-body>
      <el-form label-width="90px">
        <el-form-item label="库位编码"><el-input v-model="locationForm.code" /></el-form-item>
        <el-form-item label="库位名称"><el-input v-model="locationForm.name" /></el-form-item>
        <el-form-item label="区域"><el-input v-model="locationForm.zone" /></el-form-item>
        <el-form-item label="巷道"><el-input v-model="locationForm.aisle" /></el-form-item>
        <el-form-item label="货架"><el-input v-model="locationForm.rack" /></el-form-item>
        <el-form-item label="货位"><el-input v-model="locationForm.bin" /></el-form-item>
        <el-form-item label="状态"><el-select v-model="locationForm.status"><el-option label="启用" value="active" /><el-option label="停用" value="inactive" /></el-select></el-form-item>
      </el-form>
      <template #footer><el-button @click="locationEditVisible = false">取消</el-button><el-button type="primary" @click="saveLocation">保存</el-button></template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { http } from '../api/http'

const rows = ref<any[]>([])
const locations = ref<any[]>([])
const currentWarehouse = ref<any>(null)
const warehouseVisible = ref(false)
const locationVisible = ref(false)
const locationEditVisible = ref(false)
const warehouseForm = reactive<any>({})
const locationForm = reactive<any>({})

async function load() { rows.value = (await http.get('/warehouses')).data || [] }
function openWarehouse(row?: any) {
  Object.assign(warehouseForm, row ? { ...row } : { id: 0, code: '', name: '', address: '', contactName: '', contactPhone: '', status: 'active' })
  warehouseVisible.value = true
}
async function saveWarehouse() {
  if (!warehouseForm.code || !warehouseForm.name) return ElMessage.warning('请输入仓库编码和名称')
  if (warehouseForm.id) await http.put(`/warehouses/${warehouseForm.id}`, warehouseForm)
  else await http.post('/warehouses', warehouseForm)
  ElMessage.success('仓库已保存')
  warehouseVisible.value = false
  await load()
}
async function openLocations(warehouse: any) {
  currentWarehouse.value = warehouse
  locationVisible.value = true
  await loadLocations()
}
async function loadLocations() {
  locations.value = (await http.get('/warehouse-locations', { params: { warehouseId: currentWarehouse.value.id } })).data || []
}
function openLocation(row?: any) {
  Object.assign(locationForm, row ? { ...row } : { id: 0, warehouseId: currentWarehouse.value.id, code: '', name: '', zone: '', aisle: '', rack: '', bin: '', status: 'active' })
  locationEditVisible.value = true
}
async function saveLocation() {
  if (!locationForm.code || !locationForm.name) return ElMessage.warning('请输入库位编码和名称')
  if (locationForm.id) await http.put(`/warehouse-locations/${locationForm.id}`, locationForm)
  else await http.post('/warehouse-locations', locationForm)
  ElMessage.success('库位已保存')
  locationEditVisible.value = false
  await Promise.all([loadLocations(), load()])
}

onMounted(load)
</script>

<style scoped>
.page-subtitle { margin-top: 4px; color: #64748b; font-size: 13px; }
.toolbar { margin-bottom: 12px; }
</style>
