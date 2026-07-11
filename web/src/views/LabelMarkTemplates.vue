<template>
  <div class="page">
    <div class="page-header">
      <div>
        <div class="page-title">标签与唛头模板</div>
        <div class="subtitle">每个客户可维护多个模板；订单确认后保存模板快照，不受后续模板修改影响。</div>
      </div>
      <el-button type="primary" @click="openCreate">新增模板</el-button>
    </div>

    <div class="card">
      <div class="toolbar">
        <el-select v-model="filters.customerId" clearable filterable placeholder="客户" style="width:240px" @change="load">
          <el-option v-for="item in customers" :key="item.id" :label="item.name" :value="item.id" />
        </el-select>
        <el-select v-model="filters.templateType" clearable placeholder="模板类型" style="width:180px" @change="load">
          <el-option label="商品标签" value="product_label" />
          <el-option label="外箱唛头" value="carton_mark" />
        </el-select>
        <el-button @click="load">刷新</el-button>
      </div>
      <el-table :data="rows" border stripe>
        <el-table-column prop="code" label="编码" width="150" />
        <el-table-column prop="name" label="模板名称" min-width="180" />
        <el-table-column label="类型" width="130"><template #default="scope">{{ scope.row.templateType === 'product_label' ? '商品标签' : '外箱唛头' }}</template></el-table-column>
        <el-table-column label="设计模式" width="110"><template #default="scope">{{ scope.row.designerMode === 'visual' ? '可视化' : '固定模板' }}</template></el-table-column>
        <el-table-column label="尺寸" width="130"><template #default="scope">{{ scope.row.paperWidthMm }} × {{ scope.row.paperHeightMm }} mm</template></el-table-column>
        <el-table-column prop="orientation" label="方向" width="90" />
        <el-table-column label="默认" width="80"><template #default="scope"><el-tag v-if="scope.row.isDefault" type="success">默认</el-tag></template></el-table-column>
        <el-table-column prop="status" label="状态" width="90" />
        <el-table-column label="操作" width="170" fixed="right"><template #default="scope"><el-button size="small" @click="openEdit(scope.row)">编辑</el-button><el-button size="small" type="danger" @click="remove(scope.row.id)">删除</el-button></template></el-table-column>
      </el-table>
    </div>

    <el-dialog v-model="visible" :title="form.id ? '编辑模板' : '新增模板'" width="900px" destroy-on-close>
      <el-form label-width="105px">
        <el-row :gutter="14">
          <el-col :span="8"><el-form-item label="客户"><el-select v-model="form.customerId" :disabled="!!form.id" filterable style="width:100%"><el-option v-for="item in customers" :key="item.id" :label="item.name" :value="item.id" /></el-select></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="模板类型"><el-select v-model="form.templateType" :disabled="!!form.id" style="width:100%"><el-option label="商品标签" value="product_label" /><el-option label="外箱唛头" value="carton_mark" /></el-select></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="设计模式"><el-select v-model="form.designerMode" style="width:100%"><el-option label="固定模板" value="fixed" /><el-option label="可视化布局" value="visual" /></el-select></el-form-item></el-col>
          <el-col :span="8"><el-form-item label="模板编码"><el-input v-model="form.code" /></el-form-item></el-col>
          <el-col :span="16"><el-form-item label="模板名称"><el-input v-model="form.name" /></el-form-item></el-col>
          <el-col :span="6"><el-form-item label="宽度 mm"><el-input-number v-model="form.paperWidthMm" :min="1" :precision="2" style="width:100%" /></el-form-item></el-col>
          <el-col :span="6"><el-form-item label="高度 mm"><el-input-number v-model="form.paperHeightMm" :min="1" :precision="2" style="width:100%" /></el-form-item></el-col>
          <el-col :span="6"><el-form-item label="方向"><el-select v-model="form.orientation" style="width:100%"><el-option label="纵向" value="portrait" /><el-option label="横向" value="landscape" /></el-select></el-form-item></el-col>
          <el-col :span="6"><el-form-item label="设为默认"><el-switch v-model="form.isDefault" /></el-form-item></el-col>
          <el-col :span="24" v-if="form.designerMode === 'fixed'"><el-form-item label="HTML 模板"><el-input v-model="form.body" type="textarea" :rows="10" placeholder="支持 {{CustomerBarcode}}、{{CustomerItemNo}}、{{ProductName}}、{{BatchCode}}、{{ImporterCompany}} 等变量" /></el-form-item></el-col>
          <el-col :span="24" v-else><el-form-item label="布局 JSON"><el-input v-model="form.layoutJson" type="textarea" :rows="10" placeholder='{"elements":[]}' /></el-form-item></el-col>
          <el-col :span="12"><el-form-item label="语言"><el-input v-model="form.language" /></el-form-item></el-col>
          <el-col :span="12"><el-form-item label="状态"><el-select v-model="form.status" style="width:100%"><el-option label="启用" value="active" /><el-option label="停用" value="inactive" /></el-select></el-form-item></el-col>
          <el-col :span="24"><el-form-item label="备注"><el-input v-model="form.remark" type="textarea" /></el-form-item></el-col>
        </el-row>
      </el-form>
      <template #footer><el-button @click="visible=false">取消</el-button><el-button type="primary" @click="save">保存模板</el-button></template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { http } from '../api/http'

const rows = ref<any[]>([])
const customers = ref<any[]>([])
const visible = ref(false)
const filters = reactive({ customerId: null as number | null, templateType: '' })
const empty = () => ({ id: 0, customerId: filters.customerId, importerProfileId: null, code: '', name: '', documentType: 'CO', templateType: filters.templateType || 'product_label', designerMode: 'fixed', language: 'en', paperSize: 'CUSTOM', paperWidthMm: 100, paperHeightMm: 50, orientation: 'portrait', layoutJson: '{"elements":[]}', body: '<div>{{ProductName}}</div><div>{{CustomerBarcode}}</div>', isDefault: false, status: 'active', remark: '' })
const form = reactive<any>(empty())

async function load() {
  const response = await http.get('/label-mark-templates', { params: { customerId: filters.customerId || undefined, templateType: filters.templateType || undefined, status: '' } })
  rows.value = response.data || []
}
function openCreate() { Object.assign(form, empty()); visible.value = true }
function openEdit(row: any) { Object.assign(form, empty(), row); visible.value = true }
async function save() {
  if (!form.customerId) return ElMessage.warning('请选择客户')
  if (!form.code.trim()) return ElMessage.warning('请输入模板编码')
  if (!form.name.trim()) return ElMessage.warning('请输入模板名称')
  if (form.designerMode === 'visual') {
    try { JSON.parse(form.layoutJson || '{}') } catch { return ElMessage.warning('布局 JSON 格式错误') }
  }
  if (form.id) await http.put(`/label-mark-templates/${form.id}`, form)
  else await http.post('/label-mark-templates', form)
  visible.value = false
  ElMessage.success('模板保存成功')
  await load()
}
async function remove(id: number) {
  await ElMessageBox.confirm('确认删除该模板？已被订单引用的模板不能删除。', '提示')
  await http.delete(`/label-mark-templates/${id}`)
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
