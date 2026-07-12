<template>
  <div class="page">
    <div class="page-header">
      <div>
        <div class="page-title">客户进口商资料</div>
        <div class="subtitle">每个客户可维护多个进口商，默认进口商会自动带入新客户订单。</div>
      </div>
      <el-button type="primary" @click="openCreate">新增进口商</el-button>
    </div>

    <div class="card">
      <div class="toolbar">
        <el-select v-model="customerId" clearable filterable placeholder="按客户筛选" style="width:260px" @change="load">
          <el-option v-for="item in customers" :key="item.id" :label="item.name" :value="item.id" />
        </el-select>
        <el-button @click="load">刷新</el-button>
      </div>
      <el-table :data="rows" border stripe>
        <el-table-column prop="name" label="资料名称" width="180" />
        <el-table-column prop="companyName" label="进口商公司" min-width="220" />
        <el-table-column prop="taxIdOrRfc" label="RFC / 税号" width="160" />
        <el-table-column prop="address" label="地址" min-width="260" show-overflow-tooltip />
        <el-table-column prop="contactName" label="联系人" width="120" />
        <el-table-column prop="phone" label="电话" width="140" />
        <el-table-column label="默认" width="90"><template #default="scope"><el-tag v-if="scope.row.isDefault" type="success">默认</el-tag></template></el-table-column>
        <el-table-column prop="status" label="状态" width="100" />
        <el-table-column label="操作" width="170" fixed="right">
          <template #default="scope">
            <el-button size="small" @click="openEdit(scope.row)">编辑</el-button>
            <el-button size="small" type="danger" @click="remove(scope.row.id)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>
    </div>

    <el-dialog v-model="visible" :title="form.id ? '编辑进口商资料' : '新增进口商资料'" width="760px">
      <el-form label-width="110px">
        <el-row :gutter="14">
          <el-col :span="12"><el-form-item label="客户"><el-select v-model="form.customerId" :disabled="!!form.id" filterable style="width:100%"><el-option v-for="item in customers" :key="item.id" :label="item.name" :value="item.id" /></el-select></el-form-item></el-col>
          <el-col :span="12"><el-form-item label="资料名称"><el-input v-model="form.name" placeholder="例如：墨西哥进口商 A" /></el-form-item></el-col>
          <el-col :span="12"><el-form-item label="公司名称"><el-input v-model="form.companyName" /></el-form-item></el-col>
          <el-col :span="12"><el-form-item label="RFC / 税号"><el-input v-model="form.taxIdOrRfc" /></el-form-item></el-col>
          <el-col :span="24"><el-form-item label="公司地址"><el-input v-model="form.address" type="textarea" :rows="3" /></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="联系人"><el-input v-model="form.contactName" /></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="电话"><el-input v-model="form.phone" /></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="邮箱"><el-input v-model="form.email" /></el-form-item></el-col>
          <el-col :span="24"><el-form-item label="Logo URL"><el-input v-model="form.logoUrl" /></el-form-item></el-col>
          <el-col :span="12"><el-form-item label="原产地文字"><el-input v-model="form.defaultOriginText" /></el-form-item></el-col>
          <el-col :span="6"><el-form-item label="设为默认"><el-switch v-model="form.isDefault" /></el-form-item></el-col>
          <el-col :span="6"><el-form-item label="状态"><el-select v-model="form.status" style="width:100%"><el-option label="启用" value="active" /><el-option label="停用" value="inactive" /></el-select></el-form-item></el-col>
          <el-col :span="24"><el-form-item label="备注"><el-input v-model="form.remark" type="textarea" /></el-form-item></el-col>
        </el-row>
      </el-form>
      <template #footer><el-button @click="visible=false">取消</el-button><el-button type="primary" @click="save">保存</el-button></template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { http } from '../api/http'

const rows = ref<any[]>([])
const customers = ref<any[]>([])
const customerId = ref<number | null>(null)
const visible = ref(false)
const empty = () => ({ id: 0, customerId: null as number | null, name: '', companyName: '', taxIdOrRfc: '', address: '', contactName: '', phone: '', email: '', logoUrl: '', defaultOriginText: 'Made in China', defaultLabelTemplateId: null, defaultMarkTemplateId: null, isDefault: false, status: 'active', remark: '' })
const form = reactive<any>(empty())

async function load() {
  const response = await http.get('/customer-importer-profiles', { params: { customerId: customerId.value || undefined } })
  rows.value = response.data || []
}
function openCreate() { Object.assign(form, empty(), { customerId: customerId.value }); visible.value = true }
function openEdit(row: any) { Object.assign(form, empty(), row); visible.value = true }
async function save() {
  if (!form.customerId) return ElMessage.warning('请选择客户')
  if (!form.name.trim()) return ElMessage.warning('请输入资料名称')
  if (!form.companyName.trim()) return ElMessage.warning('请输入公司名称')
  if (!form.address.trim()) return ElMessage.warning('请输入公司地址')
  if (form.id) await http.put(`/customer-importer-profiles/${form.id}`, form)
  else await http.post('/customer-importer-profiles', form)
  visible.value = false
  ElMessage.success('保存成功')
  await load()
}
async function remove(id: number) {
  await ElMessageBox.confirm('确认删除该进口商资料？已被订单引用的资料不能删除。', '提示')
  await http.delete(`/customer-importer-profiles/${id}`)
  ElMessage.success('已删除')
  await load()
}

onMounted(async () => {
  customers.value = (await http.get('/customers')).data || []
  await load()
})
</script>

<style scoped>
.subtitle { color:#64748b; margin-top:4px; font-size:13px; }
.toolbar { display:flex; gap:10px; margin-bottom:14px; }
</style>
