<template>
  <div class="allocation-editor">
    <div class="header-row">
      <div>
        <strong>汇总商品</strong>
        <span class="hint">仅显示已确认 PO 的剩余整箱数量</span>
      </div>
      <div>
        <el-button size="small" @click="load">刷新</el-button>
        <el-button size="small" type="primary" :disabled="!canAdd" @click="openAdd">
          {{ status === 'confirmed' ? '审批追加商品' : '加入 PO 商品' }}
        </el-button>
      </div>
    </div>

    <el-table :data="items" border stripe size="small">
      <el-table-column prop="purchaseOrder.no" label="PO 单号" width="150" />
      <el-table-column prop="product.customerItemNo" label="客户货号" width="135" />
      <el-table-column prop="product.customerBarcode" label="客户条码" width="145" />
      <el-table-column prop="product.nameCn" label="商品名称" min-width="180" />
      <el-table-column prop="item.supplierId" label="供应商 ID" width="105" />
      <el-table-column prop="item.reservedCartons" label="汇总箱数" width="100" />
      <el-table-column prop="item.reservedQuantity" label="汇总数量" width="100" />
      <el-table-column label="采购金额" width="120" align="right">
        <template #default="scope">{{ formatRmb(scope.row.item.reservedQuantity * scope.row.line.purchaseUnitPriceSnapshot) }}</template>
      </el-table-column>
      <el-table-column label="客户金额" width="120" align="right">
        <template #default="scope">{{ formatRmb(scope.row.item.reservedQuantity * scope.row.line.salesUnitPriceSnapshot) }}</template>
      </el-table-column>
      <el-table-column prop="item.reservationStatus" label="预留状态" width="120" />
      <el-table-column label="操作" width="100" fixed="right">
        <template #default="scope">
          <el-button
            v-if="scope.row.item.reservationStatus === 'draft_reserved' && status === 'draft'"
            size="small"
            type="danger"
            @click="release(scope.row.item)"
          >释放</el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-dialog v-model="visible" title="选择 PO 剩余商品" width="94%" append-to-body destroy-on-close>
      <div class="dialog-toolbar">
        <el-input v-model="keyword" clearable placeholder="PO / 客户货号 / 条码 / 商品名称" style="width:360px" />
        <el-button @click="loadAvailable">刷新可用数量</el-button>
      </div>
      <el-table :data="filteredAvailable" border stripe size="small">
        <el-table-column prop="purchaseOrder.no" label="PO 单号" width="145" />
        <el-table-column prop="product.customerItemNo" label="客户货号" width="130" />
        <el-table-column prop="product.customerBarcode" label="客户条码" width="145" />
        <el-table-column prop="product.nameCn" label="商品名称" min-width="170" />
        <el-table-column prop="purchaseOrder.supplierId" label="供应商 ID" width="105" />
        <el-table-column prop="line.cartons" label="PO 总箱数" width="100" />
        <el-table-column prop="reservedCartons" label="已预留" width="90" />
        <el-table-column prop="availableCartons" label="可用箱数" width="100" />
        <el-table-column prop="availableQuantity" label="可用数量" width="100" />
        <el-table-column label="本次整箱" width="160">
          <template #default="scope">
            <el-input-number
              v-model="scope.row.requestCartons"
              :min="1"
              :max="scope.row.availableCartons"
              :precision="0"
              controls-position="right"
              style="width:135px"
            />
          </template>
        </el-table-column>
        <el-table-column label="操作" width="100" fixed="right">
          <template #default="scope"><el-button size="small" type="primary" @click="reserve(scope.row)">加入</el-button></template>
        </el-table-column>
      </el-table>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { http } from '../api/http'
import { formatRmb } from '../utils/rmb'

const props = defineProps<{
  summaryId: number
  status: string
}>()

const emit = defineEmits<{ (event: 'changed'): void }>()

const items = ref<any[]>([])
const available = ref<any[]>([])
const visible = ref(false)
const keyword = ref('')
const canAdd = computed(() => ['draft', 'confirmed'].includes(props.status))
const filteredAvailable = computed(() => {
  const term = keyword.value.trim().toLowerCase()
  if (!term) return available.value
  return available.value.filter(row => [
    row.purchaseOrder?.no,
    row.product?.customerItemNo,
    row.product?.customerBarcode,
    row.product?.nameCn,
  ].some(value => String(value || '').toLowerCase().includes(term)))
})

async function load() {
  if (!props.summaryId) return
  items.value = (await http.get(`/customer-summaries/${props.summaryId}/items`)).data || []
}

async function loadAvailable() {
  if (!props.summaryId) return
  const response = await http.get(`/customer-summaries/${props.summaryId}/available-po-lines`)
  available.value = (response.data || []).map((row: any) => ({ ...row, requestCartons: 1 }))
}

async function openAdd() {
  visible.value = true
  await loadAvailable()
}

async function reserve(row: any) {
  const cartons = Number(row.requestCartons || 0)
  if (!Number.isInteger(cartons) || cartons <= 0) return ElMessage.warning('汇总数量必须是大于 0 的整箱数')
  if (cartons > Number(row.availableCartons || 0)) return ElMessage.warning('本次箱数超过可用箱数')

  if (props.status === 'confirmed') {
    const result = await ElMessageBox.prompt('填写确认后追加商品的原因', '审批追加', {
      inputValidator: value => !!value?.trim() || '追加原因不能为空',
    })
    await http.post(`/customer-summaries/${props.summaryId}/append-items`, {
      reason: result.value,
      items: [{ purchaseOrderLineId: row.line.id, cartons }],
    })
  } else {
    await http.post(`/customer-summaries/${props.summaryId}/reserve`, {
      purchaseOrderLineId: row.line.id,
      cartons,
    })
  }

  ElMessage.success('整箱数量已加入客户汇总单')
  await Promise.all([load(), loadAvailable()])
  emit('changed')
}

async function release(item: any) {
  const result = await ElMessageBox.prompt('填写释放预留原因', '释放汇总商品', {
    inputValidator: value => !!value?.trim() || '释放原因不能为空',
  })
  await http.post(`/customer-summaries/items/${item.id}/release`, { reason: result.value })
  ElMessage.success('预留数量已释放')
  await load()
  emit('changed')
}

watch(() => props.summaryId, load)
onMounted(load)
</script>

<style scoped>
.allocation-editor { margin-top:18px; }
.header-row { display:flex; justify-content:space-between; align-items:center; margin-bottom:12px; }
.hint { margin-left:12px; color:#64748b; font-size:12px; }
.dialog-toolbar { display:flex; gap:10px; margin-bottom:12px; }
</style>
