<template>
  <div class="page">
    <div class="page-header">
      <div class="page-title">Finance 财务</div>
      <el-button type="primary" @click="openCreate">新增财务记录</el-button>
    </div>

    <div class="card">
      <div class="toolbar">
        <el-select v-model="recordType" placeholder="收/付款类型" clearable style="width: 160px" @change="load">
          <el-option label="应收" value="receivable" />
          <el-option label="应付" value="payable" />
          <el-option label="费用" value="expense" />
          <el-option label="收入" value="income" />
        </el-select>
        <el-select v-model="customerId" placeholder="按客户筛选" clearable filterable style="width: 240px" @change="load">
          <el-option v-for="c in customers" :key="c.id" :label="c.name" :value="c.id" />
        </el-select>
        <el-select v-model="supplierId" placeholder="按供应商筛选" clearable filterable style="width: 240px" @change="load">
          <el-option v-for="s in suppliers" :key="s.id" :label="s.name" :value="s.id" />
        </el-select>
        <el-button @click="load">刷新</el-button>
      </div>

      <el-table :data="rows" border stripe>
        <el-table-column prop="no" label="财务单号" width="190" />
        <el-table-column prop="recordType" label="类型" width="110" />
        <el-table-column prop="targetType" label="来源类型" width="120" />
        <el-table-column prop="targetId" label="来源ID" width="90" />
        <el-table-column prop="customerId" label="客户ID" width="90" />
        <el-table-column prop="supplierId" label="供应商ID" width="100" />
        <el-table-column prop="currency" label="币种" width="90" />
        <el-table-column prop="amount" label="金额" width="120" />
        <el-table-column prop="paidAmount" label="已付/已收" width="120" />
        <el-table-column prop="recordDate" label="日期" width="140" />
        <el-table-column prop="status" label="状态" width="110" />
        <el-table-column label="操作" width="180" fixed="right">
          <template #default="scope">
            <el-button size="small" @click="openEdit(scope.row)">编辑</el-button>
            <el-button size="small" type="danger" @click="remove(scope.row.id)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>
    </div>

    <el-dialog v-model="dialogVisible" :title="form.id ? '编辑财务记录' : '新增财务记录'" width="680px">
      <el-alert title="客户收款按 SO，供应商付款按 PO。后续会增加自动生成应收/应付、利润统计和对账。" type="info" show-icon style="margin-bottom: 12px" />
      <el-form label-width="110px">
        <el-form-item label="类型"><el-select v-model="form.recordType" style="width:100%"><el-option label="应收" value="receivable"/><el-option label="应付" value="payable"/><el-option label="费用" value="expense"/><el-option label="收入" value="income"/></el-select></el-form-item>
        <el-form-item label="来源类型"><el-select v-model="form.targetType" style="width:100%"><el-option label="SO" value="SO"/><el-option label="PO" value="PO"/><el-option label="Container" value="CONTAINER"/><el-option label="Shipment" value="SHIPMENT"/><el-option label="Manual" value="MANUAL"/></el-select></el-form-item>
        <el-form-item label="来源ID"><el-input-number v-model="form.targetId" :min="0" style="width:100%" /></el-form-item>
        <el-form-item label="客户"><el-select v-model="form.customerId" clearable filterable style="width:100%"><el-option v-for="c in customers" :key="c.id" :label="c.name" :value="c.id" /></el-select></el-form-item>
        <el-form-item label="供应商"><el-select v-model="form.supplierId" clearable filterable style="width:100%"><el-option v-for="s in suppliers" :key="s.id" :label="s.name" :value="s.id" /></el-select></el-form-item>
        <el-form-item label="币种"><el-input v-model="form.currency" /></el-form-item>
        <el-form-item label="金额"><el-input-number v-model="form.amount" :min="0" :precision="2" style="width:100%" /></el-form-item>
        <el-form-item label="已付/已收"><el-input-number v-model="form.paidAmount" :min="0" :precision="2" style="width:100%" /></el-form-item>
        <el-form-item label="日期"><el-date-picker v-model="form.recordDate" type="date" value-format="YYYY-MM-DD" style="width:100%" /></el-form-item>
        <el-form-item label="状态"><el-select v-model="form.status" style="width:100%"><el-option label="待处理" value="pending"/><el-option label="部分" value="partial"/><el-option label="完成" value="done"/></el-select></el-form-item>
        <el-form-item label="备注"><el-input v-model="form.remark" type="textarea" /></el-form-item>
      </el-form>
      <template #footer><el-button @click="dialogVisible=false">取消</el-button><el-button type="primary" @click="save">保存</el-button></template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { http } from '../api/http'

const rows = ref<any[]>([])
const customers = ref<any[]>([])
const suppliers = ref<any[]>([])
const recordType = ref<string | null>(null)
const customerId = ref<number | null>(null)
const supplierId = ref<number | null>(null)
const dialogVisible = ref(false)
const form = reactive<any>({ id: 0, recordType: 'receivable', targetType: 'MANUAL', targetId: 0, customerId: null, supplierId: null, currency: 'USD', amount: 0, paidAmount: 0, recordDate: '', status: 'pending', remark: '' })

async function loadCustomers() { const res = await http.get('/customers'); customers.value = res.data }
async function loadSuppliers() { const res = await http.get('/suppliers'); suppliers.value = res.data }
async function load() { const params: any = {}; if (recordType.value) params.recordType = recordType.value; if (customerId.value) params.customerId = customerId.value; if (supplierId.value) params.supplierId = supplierId.value; const res = await http.get('/finance-records', { params }); rows.value = res.data }
function reset() { Object.assign(form, { id: 0, recordType: 'receivable', targetType: 'MANUAL', targetId: 0, customerId: null, supplierId: null, currency: 'USD', amount: 0, paidAmount: 0, recordDate: '', status: 'pending', remark: '' }) }
function openCreate() { reset(); dialogVisible.value = true }
function openEdit(row: any) { Object.assign(form, row); dialogVisible.value = true }
async function save() { if (!form.recordType) return ElMessage.warning('请选择类型'); if (form.id) await http.put(`/finance-records/${form.id}`, form); else await http.post('/finance-records', form); dialogVisible.value = false; ElMessage.success('保存成功'); await load() }
async function remove(id: number) { await ElMessageBox.confirm('确认删除该财务记录？', '提示'); await http.delete(`/finance-records/${id}`); ElMessage.success('已删除'); await load() }

onMounted(async () => { await loadCustomers(); await loadSuppliers(); await load() })
</script>
