<template>
  <div class="page">
    <div class="page-header">
      <div class="page-title">RFQ 客户询价单</div>
      <el-button type="primary" @click="openCreate">新增 RFQ</el-button>
    </div>

    <div class="card">
      <div class="toolbar">
        <el-select v-model="customerId" placeholder="按客户筛选" clearable filterable style="width: 260px" @change="load">
          <el-option v-for="c in customers" :key="c.id" :label="c.name" :value="c.id" />
        </el-select>
        <el-button @click="load">刷新</el-button>
      </div>

      <el-table :data="rows" border stripe>
        <el-table-column label="RFQ单号" width="190">
          <template #default="scope"><el-button link type="primary" class="document-no" @click="openDocument(scope.row)">{{ scope.row.no }}</el-button></template>
        </el-table-column>
        <el-table-column prop="customerId" label="客户ID" width="100" />
        <el-table-column prop="status" label="状态" width="110" />
        <el-table-column prop="requestDate" label="询价日期" width="150" />
        <el-table-column prop="remark" label="备注" min-width="220" />
        <el-table-column label="操作" width="330" fixed="right">
          <template #default="scope">
            <el-button size="small" type="primary" @click="generateCo(scope.row.id)">生成CO</el-button>
            <el-button size="small" @click="openDocument(scope.row)">编辑</el-button>
            <el-button size="small" @click="copy(scope.row.id)">复制</el-button>
            <el-button size="small" type="danger" @click="remove(scope.row.id)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>
    </div>

    <el-dialog v-model="dialogVisible" :title="form.id ? `编辑 RFQ：${form.no || ''}` : '新增 RFQ'" width="92%" destroy-on-close>
      <el-alert title="RFQ 明细用于记录客户询价商品、数量、箱规、CBM 和重量。保存主单后即可添加商品明细。" type="info" show-icon style="margin-bottom: 12px" />
      <el-form label-width="100px">
        <el-form-item label="客户">
          <el-select v-model="form.customerId" filterable placeholder="选择客户" style="width: 100%">
            <el-option v-for="c in customers" :key="c.id" :label="c.name" :value="c.id" />
          </el-select>
        </el-form-item>
        <el-form-item label="询价日期"><el-date-picker v-model="form.requestDate" type="date" value-format="YYYY-MM-DD" style="width: 100%" /></el-form-item>
        <el-form-item label="状态"><el-input v-model="form.status" /></el-form-item>
        <el-form-item label="备注"><el-input v-model="form.remark" type="textarea" /></el-form-item>
      </el-form>
      <DocumentLinesEditor v-if="form.id" document-type="RFQ" :document-id="form.id" />
      <template #footer>
        <el-button @click="dialogVisible=false">关闭</el-button>
        <el-button type="primary" @click="save">保存主单</el-button>
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
const form = reactive<any>({ id: 0, no: '', customerId: null, requestDate: '', status: 'draft', remark: '' })

async function loadCustomers() { const res = await http.get('/customers'); customers.value = res.data }
async function load() { const params: any = {}; if (customerId.value) params.customerId = customerId.value; const res = await http.get('/rfqs', { params }); rows.value = res.data }
function reset() { Object.assign(form, { id: 0, no: '', customerId: null, requestDate: '', status: 'draft', remark: '' }) }
function openCreate() { reset(); dialogVisible.value = true }
function openDocument(row: any) { Object.assign(form, row); dialogVisible.value = true }
async function save() {
  if (!form.customerId) return ElMessage.warning('请选择客户')
  const res = form.id ? await http.put(`/rfqs/${form.id}`, form) : await http.post('/rfqs', form)
  if (res.data) Object.assign(form, res.data)
  ElMessage.success('主单保存成功')
  await load()
}
async function copy(id: number) { await http.post(`/rfqs/${id}/copy`); ElMessage.success('复制成功'); await load() }
async function generateCo(id: number) { const res = await http.post(`/rfqs/${id}/generate-co`); ElMessage.success(`已生成 CO：${res.data?.no || ''}`); await load() }
async function remove(id: number) { await ElMessageBox.confirm('确认删除该 RFQ？', '提示'); await http.delete(`/rfqs/${id}`); ElMessage.success('已删除'); await load() }

onMounted(async () => { await loadCustomers(); await load() })
</script>

<style scoped>
.document-no { font-weight: 700; }
</style>