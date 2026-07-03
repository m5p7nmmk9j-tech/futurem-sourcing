<template>
  <div class="lines-box">
    <div class="lines-header">
      <div class="lines-title">明细 Items</div>
      <div>
        <el-button size="small" @click="load">刷新</el-button>
        <el-button size="small" type="primary" @click="openCreate">添加商品</el-button>
      </div>
    </div>

    <el-row :gutter="12" class="summary-row">
      <el-col :span="4"><el-statistic title="数量" :value="summary.quantity || 0" /></el-col>
      <el-col :span="4"><el-statistic title="金额" :value="summary.amount || 0" /></el-col>
      <el-col :span="4"><el-statistic title="箱数" :value="summary.cartons || 0" /></el-col>
      <el-col :span="4"><el-statistic title="CBM" :value="summary.cbm || 0" /></el-col>
      <el-col :span="4"><el-statistic title="GW KG" :value="summary.gwKg || 0" /></el-col>
      <el-col :span="4"><el-statistic title="NW KG" :value="summary.nwKg || 0" /></el-col>
    </el-row>

    <el-table :data="rows" border stripe size="small">
      <el-table-column prop="sku" label="SKU" width="130" />
      <el-table-column prop="productName" label="商品" min-width="180" />
      <el-table-column prop="quantity" label="数量" width="90" />
      <el-table-column prop="unitPrice" label="单价" width="90" />
      <el-table-column prop="amount" label="金额" width="100" />
      <el-table-column prop="cartonQty" label="单箱数" width="90" />
      <el-table-column prop="cartons" label="箱数" width="90" />
      <el-table-column prop="cartonCbm" label="单箱CBM" width="100" />
      <el-table-column prop="totalCbm" label="总CBM" width="100" />
      <el-table-column prop="totalGwKg" label="总GW" width="100" />
      <el-table-column label="操作" width="150" fixed="right">
        <template #default="scope">
          <el-button size="small" @click="openEdit(scope.row)">编辑</el-button>
          <el-button size="small" type="danger" @click="remove(scope.row.id)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-dialog v-model="dialogVisible" :title="form.id ? '编辑明细' : '新增明细'" width="860px">
      <el-form label-width="95px">
        <el-row :gutter="12">
          <el-col :span="12"><el-form-item label="商品"><el-select v-model="form.productId" filterable clearable style="width:100%" @change="selectProduct"><el-option v-for="p in products" :key="p.id" :label="`${p.sku} / ${p.nameCn}`" :value="p.id" /></el-select></el-form-item></el-col>
          <el-col :span="12"><el-form-item label="名称"><el-input v-model="form.productName" /></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="SKU"><el-input v-model="form.sku" /></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="数量"><el-input-number v-model="form.quantity" :min="0" :precision="2" style="width:100%" /></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="单价"><el-input-number v-model="form.unitPrice" :min="0" :precision="4" style="width:100%" /></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="单箱数量"><el-input-number v-model="form.cartonQty" :min="0" :precision="2" style="width:100%" /></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="箱数"><el-input-number v-model="form.cartons" :min="0" :precision="2" style="width:100%" /></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="单位"><el-input v-model="form.unit" /></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="长cm"><el-input-number v-model="form.cartonLengthCm" :min="0" :precision="2" style="width:100%" /></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="宽cm"><el-input-number v-model="form.cartonWidthCm" :min="0" :precision="2" style="width:100%" /></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="高cm"><el-input-number v-model="form.cartonHeightCm" :min="0" :precision="2" style="width:100%" /></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="单箱GW"><el-input-number v-model="form.cartonGwKg" :min="0" :precision="2" style="width:100%" /></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="单箱NW"><el-input-number v-model="form.cartonNwKg" :min="0" :precision="2" style="width:100%" /></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="排序"><el-input-number v-model="form.sortNo" :min="0" style="width:100%" /></el-form-item></el-col>
          <el-col :span="24"><el-form-item label="备注"><el-input v-model="form.remark" type="textarea" /></el-form-item></el-col>
        </el-row>
      </el-form>
      <template #footer><el-button @click="dialogVisible=false">取消</el-button><el-button type="primary" @click="save">保存</el-button></template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { http } from '../api/http'

const props = defineProps<{ documentType: string, documentId: number | null }>()
const rows = ref<any[]>([])
const products = ref<any[]>([])
const summary = ref<any>({})
const dialogVisible = ref(false)
const form = reactive<any>({ id: 0, documentType: '', documentId: 0, productId: null, sku: '', productName: '', unit: 'PCS', quantity: 0, unitPrice: 0, cartonQty: 0, cartons: 0, cartonLengthCm: 0, cartonWidthCm: 0, cartonHeightCm: 0, cartonGwKg: 0, cartonNwKg: 0, sortNo: 0, remark: '' })

async function loadProducts() { products.value = (await http.get('/products')).data }
async function load() { if (!props.documentId) return; rows.value = (await http.get('/document-lines', { params: { documentType: props.documentType, documentId: props.documentId } })).data; summary.value = (await http.get('/document-lines/summary', { params: { documentType: props.documentType, documentId: props.documentId } })).data }
function reset() { Object.assign(form, { id: 0, documentType: props.documentType, documentId: props.documentId, productId: null, sku: '', productName: '', unit: 'PCS', quantity: 0, unitPrice: 0, cartonQty: 0, cartons: 0, cartonLengthCm: 0, cartonWidthCm: 0, cartonHeightCm: 0, cartonGwKg: 0, cartonNwKg: 0, sortNo: rows.value.length + 1, remark: '' }) }
function selectProduct(id: number) { const p = products.value.find(x => x.id === id); if (p) { form.sku = p.sku; form.productName = p.nameCn; form.unit = p.unit || 'PCS'; form.customerItemNo = p.customerItemNo } }
function openCreate() { if (!props.documentId) return ElMessage.warning('请先保存主单'); reset(); dialogVisible.value = true }
function openEdit(row: any) { Object.assign(form, row); dialogVisible.value = true }
async function save() { if (!form.productName) return ElMessage.warning('请输入商品名称'); form.id ? await http.put(`/document-lines/${form.id}`, form) : await http.post('/document-lines', form); dialogVisible.value = false; ElMessage.success('保存成功'); await load() }
async function remove(id: number) { await ElMessageBox.confirm('确认删除该明细？', '提示'); await http.delete(`/document-lines/${id}`); ElMessage.success('已删除'); await load() }

watch(() => props.documentId, load)
onMounted(async () => { await loadProducts(); await load() })
</script>

<style scoped>
.lines-box { margin-top: 16px; }
.lines-header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 12px; }
.lines-title { font-weight: 700; font-size: 16px; }
.summary-row { margin-bottom: 12px; }
</style>
