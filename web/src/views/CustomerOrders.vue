<template>
  <div class="page">
    <div class="page-header">
      <div class="page-title">CO 客户订单</div>
      <el-button type="primary" @click="openCreate">新增 CO</el-button>
    </div>

    <div class="card">
      <div class="toolbar">
        <el-select v-model="customerId" placeholder="按客户筛选" clearable filterable style="width: 260px" @change="load">
          <el-option v-for="c in customers" :key="c.id" :label="c.name" :value="c.id" />
        </el-select>
        <el-button @click="load">刷新</el-button>
      </div>

      <el-table :data="rows" border stripe>
        <el-table-column prop="no" label="CO单号" width="190" />
        <el-table-column prop="customerId" label="客户ID" width="100" />
        <el-table-column prop="orderDate" label="订单日期" width="150" />
        <el-table-column prop="currency" label="币种" width="90" />
        <el-table-column prop="status" label="状态" width="110" />
        <el-table-column prop="remark" label="备注" min-width="220" />
        <el-table-column label="操作" width="300" fixed="right">
          <template #default="scope">
            <el-button size="small" type="success" @click="selectRow(scope.row)">明细</el-button>
            <el-button size="small" @click="openEdit(scope.row)">编辑</el-button>
            <el-button size="small" @click="copy(scope.row.id)">复制</el-button>
            <el-button size="small" type="danger" @click="remove(scope.row.id)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>

      <DocumentLinesEditor v-if="selectedId" document-type="CO" :document-id="selectedId" />
    </div>

    <el-dialog v-model="dialogVisible" :title="form.id ? '编辑 CO' : '新增 CO'" width="580px">
      <el-alert title="CO 是给客户看的订单，销售价和箱规在明细里填写。" type="info" show-icon style="margin-bottom: 12px" />
      <el-form label-width="100px">
        <el-form-item label="客户">
          <el-select v-model="form.customerId" filterable placeholder="选择客户" style="width: 100%">
            <el-option v-for="c in customers" :key="c.id" :label="c.name" :value="c.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="来源RFQ ID"><el-input-number v-model="form.rfqId" :min="0" style="width: 100%" /></el-form-item>
        <el-form-item label="订单日期"><el-date-picker v-model="form.orderDate" type="date" value-format="YYYY-MM-DD" style="width: 100%" /></el-form-item>
        <el-form-item label="币种"><el-input v-model="form.currency" /></el-form-item>
        <el-form-item label="状态"><el-input v-model="form.status" /></el-form-item>
        <el-form-item label="备注"><el-input v-model="form.remark" type="textarea" /></el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible=false">取消</el-button>
        <el-button type="primary" @click="save">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { http } from '../api/http'
import DocumentLinesEditor from '../components/DocumentLinesEditor.vue'

const rows = ref<any[]>([])
const customers = ref<any[]>([])
const customerId = ref<number | null>(null)
const dialogVisible = ref(false)
const selectedId = ref<number | null>(null)
const form = reactive<any>({ id: 0, customerId: null, rfqId: null, orderDate: '', currency: 'USD', status: 'draft', remark: '' })

async function loadCustomers() { const res = await http.get('/customers'); customers.value = res.data }
async function load() { const params: any = {}; if (customerId.value) params.customerId = customerId.value; const res = await http.get('/customer-orders', { params }); rows.value = res.data; if (!selectedId.value && rows.value.length) selectedId.value = rows.value[0].id }
function reset() { Object.assign(form, { id: 0, customerId: null, rfqId: null, orderDate: '', currency: 'USD', status: 'draft', remark: '' }) }
function openCreate() { reset(); dialogVisible.value = true }
function openEdit(row: any) { Object.assign(form, row); dialogVisible.value = true }
function selectRow(row: any) { selectedId.value = row.id }
async function save() { if (!form.customerId) return ElMessage.warning('请选择客户'); const res = form.id ? await http.put(`/customer-orders/${form.id}`, form) : await http.post('/customer-orders', form); dialogVisible.value = false; ElMessage.success('保存成功'); await load(); selectedId.value = res.data?.id || form.id || selectedId.value }
async function copy(id: number) { await http.post(`/customer-orders/${id}/copy`); ElMessage.success('复制成功'); await load() }
async function remove(id: number) { await ElMessageBox.confirm('确认删除该 CO？', '提示'); await http.delete(`/customer-orders/${id}`); if (selectedId.value === id) selectedId.value = null; ElMessage.success('已删除'); await load() }

onMounted(async () => { await loadCustomers(); await load() })
</script>
