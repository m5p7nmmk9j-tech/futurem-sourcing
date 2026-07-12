<template>
  <div class="order-products">
    <div class="toolbar">
      <div>
        <strong>订单商品</strong>
        <span class="hint">每条商品保存独立价格、图片、包装和客户条码快照</span>
      </div>
      <div>
        <el-button size="small" @click="load">刷新</el-button>
        <el-button size="small" :disabled="locked" @click="openHistory">从历史商品复制</el-button>
        <el-button size="small" type="primary" :disabled="locked" @click="openCreate">新增订单商品</el-button>
      </div>
    </div>

    <el-table :data="rows" border stripe size="small">
      <el-table-column label="图片" width="82" align="center">
        <template #default="scope">
          <el-image
            v-if="mainImage(scope.row)"
            :src="mainImage(scope.row)"
            :preview-src-list="scope.row.images.map((item: any) => item.imageUrl)"
            preview-teleported
            fit="cover"
            class="product-image"
          />
          <span v-else class="empty">暂无</span>
        </template>
      </el-table-column>
      <el-table-column prop="product.customerItemNo" label="客户货号" width="130" />
      <el-table-column prop="product.customerBarcode" label="客户条码" width="150" />
      <el-table-column prop="product.systemSku" label="系统 SKU" width="145" />
      <el-table-column prop="product.nameCn" label="商品名称" min-width="170" />
      <el-table-column label="采购价" width="110" align="right">
        <template #default="scope">{{ formatRmb(scope.row.product.purchaseUnitPrice) }}</template>
      </el-table-column>
      <el-table-column label="销售价" width="110" align="right">
        <template #default="scope">{{ formatRmb(scope.row.product.salesUnitPrice) }}</template>
      </el-table-column>
      <el-table-column prop="line.quantity" label="数量" width="90" />
      <el-table-column prop="line.cartons" label="箱数" width="80" />
      <el-table-column prop="product.cartonQty" label="单箱数量" width="100" />
      <el-table-column prop="line.totalCbm" label="总 CBM" width="100" />
      <el-table-column prop="product.status" label="状态" width="90" />
      <el-table-column label="操作" width="150" fixed="right">
        <template #default="scope">
          <el-button size="small" :disabled="locked" @click="openEdit(scope.row)">编辑</el-button>
          <el-button size="small" type="danger" :disabled="locked" @click="remove(scope.row.product.id)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-dialog v-model="dialogVisible" :title="form.id ? '编辑订单商品' : '新增订单商品'" width="980px" destroy-on-close>
      <el-alert
        title="订单确认后，商品价格、包装、客户条码、进口商和标签/唛头模板将形成锁定快照。"
        type="info"
        show-icon
        :closable="false"
        style="margin-bottom: 14px"
      />
      <el-form label-width="105px">
        <el-row :gutter="14">
          <el-col :span="8"><el-form-item label="商品供应商"><el-select v-model="form.supplierId" filterable style="width:100%"><el-option v-for="item in suppliers" :key="item.id" :label="item.name" :value="item.id" /></el-select></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="系统 SKU"><el-input v-model="form.systemSku" placeholder="为空时系统自动生成" /></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="客户货号"><el-input v-model="form.customerItemNo" /></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="客户条码"><el-input v-model="form.customerBarcode" /></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="供应商货号"><el-input v-model="form.supplierItemNo" /></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="单位"><el-input v-model="form.unit" /></el-form-item></el-col>
          <el-col :span="12"><el-form-item label="中文名称"><el-input v-model="form.nameCn" /></el-form-item></el-col>
          <el-col :span="12"><el-form-item label="英文名称"><el-input v-model="form.nameEn" /></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="规格"><el-input v-model="form.specification" /></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="颜色"><el-input v-model="form.color" /></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="箱数"><el-input-number v-model="form.cartons" :min="0" :precision="2" style="width:100%" @change="syncQuantity" /></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="单箱数量"><el-input-number v-model="form.cartonQty" :min="0" :precision="2" style="width:100%" @change="syncQuantity" /></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="总数量"><el-input-number v-model="form.quantity" :min="0" :precision="2" style="width:100%" /></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="采购单价"><el-input-number v-model="form.purchaseUnitPrice" :min="0" :precision="2" style="width:100%" /></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="客户销售价"><el-input-number v-model="form.salesUnitPrice" :min="0" :precision="2" style="width:100%" /></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="预计销售额"><el-input :model-value="formatRmb(form.quantity * form.salesUnitPrice)" disabled /></el-form-item></el-col>
          <el-col :span="6"><el-form-item label="外箱长 cm"><el-input-number v-model="form.cartonLengthCm" :min="0" :precision="2" style="width:100%" /></el-form-item></el-col>
          <el-col :span="6"><el-form-item label="外箱宽 cm"><el-input-number v-model="form.cartonWidthCm" :min="0" :precision="2" style="width:100%" /></el-form-item></el-col>
          <el-col :span="6"><el-form-item label="外箱高 cm"><el-input-number v-model="form.cartonHeightCm" :min="0" :precision="2" style="width:100%" /></el-form-item></el-col>
          <el-col :span="6"><el-form-item label="预计 CBM"><el-input :model-value="estimatedCbm" disabled /></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="单箱毛重"><el-input-number v-model="form.cartonGwKg" :min="0" :precision="2" style="width:100%" /></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="单箱净重"><el-input-number v-model="form.cartonNwKg" :min="0" :precision="2" style="width:100%" /></el-form-item></el-col>
          <el-col :span="24">
            <el-form-item label="商品图片">
              <div class="image-editor">
                <div v-for="(image, index) in form.images" :key="index" class="image-row">
                  <el-select v-model="image.imageType" style="width:120px">
                    <el-option label="主图" value="main" />
                    <el-option label="详情图" value="detail" />
                    <el-option label="包装图" value="package" />
                    <el-option label="参考图" value="reference" />
                  </el-select>
                  <el-input v-model="image.imageUrl" placeholder="图片 URL" />
                  <el-button type="danger" plain @click="removeImage(index)">移除</el-button>
                </div>
                <el-button size="small" @click="addImage">增加图片</el-button>
              </div>
            </el-form-item>
          </el-col>
          <el-col :span="24"><el-form-item label="备注"><el-input v-model="form.remark" type="textarea" /></el-form-item></el-col>
        </el-row>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible=false">取消</el-button>
        <el-button type="primary" @click="save">保存订单商品</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="historyVisible" title="客户历史商品" width="92%" destroy-on-close>
      <div class="history-filter">
        <el-input v-model="historyKeyword" clearable placeholder="客户货号 / 条码 / SKU / 商品名称" @keyup.enter="loadHistory" />
        <el-button @click="loadHistory">查询</el-button>
      </div>
      <el-table :data="historyRows" border stripe size="small">
        <el-table-column label="图片" width="76">
          <template #default="scope"><el-image v-if="scope.row.mainImageUrl" :src="scope.row.mainImageUrl" fit="cover" class="history-image" /></template>
        </el-table-column>
        <el-table-column prop="product.customerItemNo" label="客户货号" width="130" />
        <el-table-column prop="product.customerBarcode" label="客户条码" width="150" />
        <el-table-column prop="product.nameCn" label="商品名称" min-width="180" />
        <el-table-column prop="product.supplierId" label="原供应商 ID" width="110" />
        <el-table-column label="采购价" width="110"><template #default="scope">{{ formatRmb(scope.row.product.purchaseUnitPrice) }}</template></el-table-column>
        <el-table-column label="销售价" width="110"><template #default="scope">{{ formatRmb(scope.row.product.salesUnitPrice) }}</template></el-table-column>
        <el-table-column label="操作" width="120" fixed="right"><template #default="scope"><el-button size="small" type="primary" @click="copyHistory(scope.row.product)">复制到本单</el-button></template></el-table-column>
      </el-table>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { http } from '../api/http'
import type { OrderProductDraft, OrderProductImage } from '../types/orderProduct'
import { formatRmb } from '../utils/rmb'
import { validateOrderProductDraft } from '../utils/orderProduct'

const props = defineProps<{
  customerOrderId: number
  customerId: number
  locked?: boolean
}>()

const emit = defineEmits<{ (event: 'changed'): void }>()

const rows = ref<any[]>([])
const suppliers = ref<any[]>([])
const dialogVisible = ref(false)
const historyVisible = ref(false)
const historyRows = ref<any[]>([])
const historyKeyword = ref('')

const emptyForm = (): OrderProductDraft => ({
  id: 0,
  customerOrderId: props.customerOrderId,
  supplierId: null,
  systemSku: '',
  customerItemNo: '',
  customerBarcode: '',
  supplierItemNo: '',
  nameCn: '',
  nameEn: '',
  specification: '',
  color: '',
  unit: 'PCS',
  purchaseUnitPrice: 0,
  salesUnitPrice: 0,
  quantity: 0,
  cartons: 0,
  cartonQty: 0,
  cartonLengthCm: 0,
  cartonWidthCm: 0,
  cartonHeightCm: 0,
  cartonGwKg: 0,
  cartonNwKg: 0,
  images: [],
  remark: '',
})

const form = reactive<OrderProductDraft>(emptyForm())

const estimatedCbm = computed(() => {
  const cartonCbm = Number(form.cartonLengthCm || 0) * Number(form.cartonWidthCm || 0) * Number(form.cartonHeightCm || 0) / 1_000_000
  return (cartonCbm * Number(form.cartons || 0)).toFixed(2)
})

async function load() {
  if (!props.customerOrderId) return
  const response = await http.get(`/customer-orders/${props.customerOrderId}/products`)
  rows.value = response.data || []
}

async function loadSuppliers() {
  const response = await http.get('/suppliers')
  suppliers.value = response.data || []
}

function resetForm() {
  Object.assign(form, emptyForm())
}

function openCreate() {
  resetForm()
  dialogVisible.value = true
}

function openEdit(row: any) {
  Object.assign(form, emptyForm(), row.product, {
    customerOrderId: props.customerOrderId,
    quantity: Number(row.line?.quantity || 0),
    cartons: Number(row.line?.cartons || 0),
    images: (row.images || []).map((item: any) => ({ ...item })),
  })
  dialogVisible.value = true
}

function syncQuantity() {
  if (Number(form.cartons || 0) > 0 && Number(form.cartonQty || 0) > 0) {
    form.quantity = Number(form.cartons || 0) * Number(form.cartonQty || 0)
  }
}

function addImage() {
  form.images.push({ imageUrl: '', imageType: form.images.length ? 'detail' : 'main', sortNo: form.images.length + 1 })
}

function removeImage(index: number) {
  form.images.splice(index, 1)
}

async function save() {
  const errors = validateOrderProductDraft(form)
  if (errors.length) return ElMessage.warning(errors[0])
  const payload = {
    ...form,
    images: form.images
      .filter(item => item.imageUrl.trim())
      .map((item, index) => ({ ...item, sortNo: index + 1 })),
  }
  if (form.id) await http.put(`/order-products/${form.id}`, payload)
  else await http.post('/order-products', payload)
  dialogVisible.value = false
  ElMessage.success('订单商品保存成功')
  await load()
  emit('changed')
}

async function remove(id: number) {
  await ElMessageBox.confirm('删除该订单商品及本订单明细？', '提示')
  await http.delete(`/order-products/${id}`)
  ElMessage.success('已删除')
  await load()
  emit('changed')
}

async function openHistory() {
  historyVisible.value = true
  await loadHistory()
}

async function loadHistory() {
  const response = await http.get('/order-products/history', {
    params: { customerId: props.customerId, keyword: historyKeyword.value || undefined },
  })
  historyRows.value = (response.data || []).filter((row: any) => row.product.sourceCustomerOrderId !== props.customerOrderId)
}

async function copyHistory(product: any) {
  await http.post(`/order-products/${product.id}/copy-to-order`, {
    targetCustomerOrderId: props.customerOrderId,
    supplierId: product.supplierId,
  })
  ElMessage.success('历史商品已复制为本订单的新商品记录')
  historyVisible.value = false
  await load()
  emit('changed')
}

function mainImage(row: any): string {
  return row.images?.find((item: OrderProductImage) => item.imageType === 'main')?.imageUrl || row.images?.[0]?.imageUrl || ''
}

watch(() => props.customerOrderId, load)
onMounted(async () => { await Promise.all([loadSuppliers(), load()]) })
</script>

<style scoped>
.order-products { margin-top: 18px; }
.toolbar { display:flex; align-items:center; justify-content:space-between; margin-bottom:12px; }
.hint { margin-left:12px; color:#64748b; font-size:12px; }
.product-image, .history-image { width:52px; height:52px; border-radius:8px; }
.empty { color:#94a3b8; }
.image-editor { width:100%; display:flex; flex-direction:column; gap:8px; }
.image-row { display:flex; gap:8px; width:100%; }
.history-filter { display:flex; gap:8px; margin-bottom:12px; }
</style>
