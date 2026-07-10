<template>
  <div class="page">
    <div class="page-header">
      <div class="page-title">Bank Accounts 资金账户</div>
      <el-button type="primary" @click="openCreate">新增账户</el-button>
    </div>

    <div class="card">
      <div class="toolbar">
        <el-input v-model="keyword" placeholder="搜索账户 / 银行 / 编号" clearable style="width: 280px" @keyup.enter="load" />
        <el-input v-model="currency" placeholder="币种" clearable style="width: 120px" @keyup.enter="load" />
        <el-button @click="load">查询</el-button>
      </div>

      <el-table :data="rows" border stripe>
        <el-table-column prop="code" label="账户编号" width="180" />
        <el-table-column prop="name" label="账户名称" min-width="180" />
        <el-table-column prop="bankName" label="银行/平台" width="160" />
        <el-table-column prop="accountNo" label="账号" width="180" />
        <el-table-column prop="currency" label="币种" width="90" />
        <el-table-column prop="openingBalance" label="初始余额" width="120" />
        <el-table-column prop="currentBalance" label="当前余额" width="120" />
        <el-table-column prop="isDefault" label="默认" width="80" />
        <el-table-column prop="isActive" label="启用" width="80" />
        <el-table-column label="操作" width="180" fixed="right">
          <template #default="scope">
            <el-button size="small" @click="openEdit(scope.row)">编辑</el-button>
            <el-button size="small" type="danger" @click="remove(scope.row.id)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>
    </div>

    <el-dialog v-model="dialogVisible" :title="form.id ? '编辑资金账户' : '新增资金账户'" width="620px">
      <el-form label-width="110px">
        <el-form-item label="账户名称"><el-input v-model="form.name" /></el-form-item>
        <el-form-item label="银行/平台"><el-input v-model="form.bankName" /></el-form-item>
        <el-form-item label="账号"><el-input v-model="form.accountNo" /></el-form-item>
        <el-form-item label="币种"><el-input v-model="form.currency" /></el-form-item>
        <el-form-item label="初始余额"><el-input-number v-model="form.openingBalance" :precision="2" style="width:100%" /></el-form-item>
        <el-form-item label="当前余额"><el-input-number v-model="form.currentBalance" :precision="2" style="width:100%" /></el-form-item>
        <el-form-item label="默认账户"><el-switch v-model="form.isDefault" /></el-form-item>
        <el-form-item label="启用"><el-switch v-model="form.isActive" /></el-form-item>
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
const keyword = ref('')
const currency = ref('')
const dialogVisible = ref(false)
const form = reactive<any>({ id: 0, name: '', bankName: '', accountNo: '', currency: 'RMB', openingBalance: 0, currentBalance: 0, isDefault: false, isActive: true, remark: '' })

async function load() { const res = await http.get('/bank-accounts', { params: { keyword: keyword.value, currency: currency.value } }); rows.value = res.data }
function reset() { Object.assign(form, { id: 0, name: '', bankName: '', accountNo: '', currency: 'RMB', openingBalance: 0, currentBalance: 0, isDefault: false, isActive: true, remark: '' }) }
function openCreate() { reset(); dialogVisible.value = true }
function openEdit(row: any) { Object.assign(form, row); dialogVisible.value = true }
async function save() { if (!form.name) return ElMessage.warning('请输入账户名称'); if (form.id) await http.put(`/bank-accounts/${form.id}`, form); else await http.post('/bank-accounts', form); dialogVisible.value = false; ElMessage.success('保存成功'); await load() }
async function remove(id: number) { await ElMessageBox.confirm('确认删除该账户？', '提示'); await http.delete(`/bank-accounts/${id}`); ElMessage.success('已删除'); await load() }

onMounted(load)
</script>
