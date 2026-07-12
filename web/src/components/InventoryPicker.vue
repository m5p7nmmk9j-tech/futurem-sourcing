<template>
  <div class="inventory-picker">
    <div class="picker-header">
      <div>
        <div class="section-title">选择仓库库存</div>
        <div class="section-note">仅显示当前客户、当前仓库的可用库存。锁定数量不能超过可用数量。</div>
      </div>
      <div class="picker-actions">
        <el-input v-model="keyword" clearable placeholder="货号 / 条码 / 商品 / PO / 批次" style="width: 280px" @keyup.enter="load" />
        <el-button @click="load">刷新</el-button>
      </div>
    </div>

    <el-alert v-if="!customerId || !warehouseId" title="请先保存客户和仓库，再选择库存。" type="warning" show-icon />
    <el-table v-else :data="rows" border stripe size="small" max-height="420">
      <el-table-column width="55">
        <template #default="scope"><el-checkbox v-model="scope.row.selected" :disabled="disabled" @change="emitChange" /></template>
      </el-table-column>
      <el-table-column prop="lot.lotNo" label="批次" width="145" />
      <el-table-column prop="product.customerItemNo" label="客户货号" width="120" />
      <el-table-column prop="product.customerBarcode" label="客户条码" width="140" />
      <el-table-column prop="product.nameCn" label="商品名称" min-width="170" />
      <el-table-column prop="purchaseOrder.no" label="PO" width="125" />
      <el-table-column prop="supplier.name" label="供应商" min-width="135" />
      <el-table-column prop="location.code" label="库位" width="90" />
      <el-table-column label="可用数量" width="100" align="right"><template #default="scope">{{ numberText(scope.row.lot.availableQuantity) }}</template></el-table-column>
      <el-table-column label="可用箱数" width="100" align="right"><template #default="scope">{{ numberText(scope.row.lot.availableCartons) }}</template></el-table-column>
      <el-table-column label="本次锁定数量" width="145"><template #default="scope"><el-input-number v-model="scope.row.quantity" :min="0" :max="Number(scope.row.lot.availableQuantity || 0)" :precision="2" :disabled="disabled || !scope.row.selected" controls-position="right" style="width: 125px" @change="syncCartons(scope.row)" /></template></el-table-column>
      <el-table-column label="本次锁定箱数" width="145"><template #default="scope"><el-input-number v-model="scope.row.cartons" :min="0" :max="Number(scope.row.lot.availableCartons || 0)" :precision="2" :disabled="disabled || !scope.row.selected" controls-position="right" style="width: 125px" @change="emitChange" /></template></el-table-column>
      <el-table-column label="CBM" width="90" align="right"><template #default="scope">{{ numberText(Number(scope.row.cartons || 0) * Number(scope.row.lot.cartonCbm || 0)) }}</template></el-table-column>
      <el-table-column label="毛重 KG" width="110" align="right"><template #default="scope">{{ numberText(Number(scope.row.cartons || 0) * Number(scope.row.lot.cartonGwKg || 0)) }}</template></el-table-column>
    </el-table>

    <div v-if="customerId && warehouseId" class="picker-summary">
      已选择 {{ selectedItems.length }} 个批次，数量 {{ numberText(totalQuantity) }}，箱数 {{ numberText(totalCartons) }}，CBM {{ numberText(totalCbm) }}，毛重 {{ numberText(totalWeight) }} KG
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { http } from '../api/http'

interface SelectionItem {
  inventoryLotId: number
  quantity: number
  cartons: number
}

const props = defineProps<{
  customerId?: number | null
  warehouseId?: number | null
  disabled?: boolean
}>()
const emit = defineEmits<{ change: [items: SelectionItem[]] }>()
const rows = ref<any[]>([])
const keyword = ref('')

const selectedItems = computed<SelectionItem[]>(() => rows.value
  .filter(row => row.selected && Number(row.quantity) > 0 && Number(row.cartons) > 0)
  .map(row => ({ inventoryLotId: row.lot.id, quantity: Number(row.quantity), cartons: Number(row.cartons) })))
const totalQuantity = computed(() => selectedItems.value.reduce((sum, item) => sum + item.quantity, 0))
const totalCartons = computed(() => selectedItems.value.reduce((sum, item) => sum + item.cartons, 0))
const totalCbm = computed(() => rows.value.filter(row => row.selected).reduce((sum, row) => sum + Number(row.cartons || 0) * Number(row.lot.cartonCbm || 0), 0))
const totalWeight = computed(() => rows.value.filter(row => row.selected).reduce((sum, row) => sum + Number(row.cartons || 0) * Number(row.lot.cartonGwKg || 0), 0))

async function load() {
  if (!props.customerId || !props.warehouseId) { rows.value = []; emitChange(); return }
  const response = await http.get('/inventory', {
    params: { customerId: props.customerId, warehouseId: props.warehouseId, keyword: keyword.value || undefined, status: 'available' }
  })
  rows.value = (response.data || [])
    .filter((row: any) => Number(row.lot?.availableQuantity || 0) > 0 && Number(row.lot?.availableCartons || 0) > 0)
    .map((row: any) => ({ ...row, selected: false, quantity: 0, cartons: 0 }))
  emitChange()
}
function syncCartons(row: any) {
  if (Number(row.lot.cartonQty || 0) > 0) row.cartons = Math.min(Number(row.lot.availableCartons || 0), Number(row.quantity || 0) / Number(row.lot.cartonQty))
  emitChange()
}
function emitChange() { emit('change', selectedItems.value) }
function numberText(value: unknown) { return Number(value || 0).toLocaleString('zh-CN', { maximumFractionDigits: 2 }) }

watch(() => [props.customerId, props.warehouseId], load, { immediate: true })
defineExpose({ load })
</script>

<style scoped>
.inventory-picker { margin-top: 16px; }
.picker-header { display: flex; align-items: center; justify-content: space-between; gap: 12px; margin-bottom: 12px; }
.picker-actions { display: flex; gap: 8px; }
.section-title { font-weight: 700; color: #1e293b; }
.section-note { margin-top: 3px; color: #64748b; font-size: 12px; }
.picker-summary { margin-top: 12px; padding: 10px 12px; border-radius: 8px; background: #eff6ff; color: #1d4ed8; font-weight: 600; }
</style>
