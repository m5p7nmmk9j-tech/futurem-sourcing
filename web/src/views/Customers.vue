<template>
  <div class="page">
    <div class="page-header">
      <div class="page-title">客户管理</div>
      <el-button type="primary" @click="openCreate">新增客户</el-button>
    </div>
    <div class="card">
      <div class="toolbar">
        <el-input v-model="keyword" placeholder="搜索客户 / 编号 / WhatsApp" clearable style="width: 320px" @keyup.enter="load" />
        <el-button @click="load">查询</el-button>
      </div>
      <el-table :data="rows" border stripe>
        <el-table-column prop="code" label="客户编号" width="180" />
        <el-table-column prop="name" label="客户名称" min-width="180" />
        <el-table-column prop="country" label="国家" width="120" />
        <el-table-column prop="contactName" label="联系人" width="140" />
        <el-table-column prop="whatsapp" label="WhatsApp" width="160" />
        <el-table-column prop="currency" label="币种" width="90" />
        <el-table-column label="操作" width="180" fixed="right">
          <template #default="scope">
            <el-button size="small" @click="openEdit(scope.row)">编辑</el-button>
            <el-button size="small" type="danger" @click="remove(scope.row.id)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>
    </div>
    <el-dialog v-model="dialogVisible" :title="form.id ? '编辑客户' : '新增客户'" width="560px">
      <el-form label-width="100px">
        <el-form-item label="客户名称"><el-input v-model="form.name" /></el-form-item>
        <el-form-item label="国家"><el-input v-model="form.country" /></el-form-item>
        <el-form-item label="目的港"><el-input v-model="form.port" /></el-form-item>
        <el-form-item label="联系人"><el-input v-model="form.contactName" /></el-form-item>
        <el-form-item label="电话"><el-input v-model="form.phone" /></el-form-item>
        <el-form-item label="WhatsApp"><el-input v-model="form.whatsapp" /></el-form-item>
        <el-form-item label="Email"><el-input v-model="form.email" /></el-form-item>
        <el-form-item label="币种"><el-input v-model="form.currency" /></el-form-item>
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
const rows = ref<any[]>([])
const keyword = ref('')
const dialogVisible = ref(false)
const form = reactive<any>({ id: 0, name: '', country: '', port: '', contactName: '', phone: '', whatsapp: '', email: '', currency: 'RMB', remark: '' })
async function load() { const res = await http.get('/customers', { params: { keyword: keyword.value } }); rows.value = res.data }
function reset() { Object.assign(form, { id: 0, name: '', country: '', port: '', contactName: '', phone: '', whatsapp: '', email: '', currency: 'RMB', remark: '' }) }
function openCreate() { reset(); dialogVisible.value = true }
function openEdit(row: any) { Object.assign(form, row); dialogVisible.value = true }
async function save() { if (!form.name) return ElMessage.warning('请输入客户名称'); form.id ? await http.put(`/customers/${form.id}`, form) : await http.post('/customers', form); dialogVisible.value = false; ElMessage.success('保存成功'); await load() }
async function remove(id: number) { await ElMessageBox.confirm('确认删除该客户？', '提示'); await http.delete(`/customers/${id}`); ElMessage.success('已删除'); await load() }
onMounted(load)
</script>
