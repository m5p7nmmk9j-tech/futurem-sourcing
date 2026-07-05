<template>
  <div class="page">
    <div class="page-header"><div class="page-title">权限管理 RBAC</div><div class="toolbar"><el-button type="primary" @click="seed">初始化权限</el-button><el-button @click="loadAll">刷新</el-button></div></div>

    <el-tabs v-model="activeTab" class="card">
      <el-tab-pane label="角色管理" name="roles">
        <div class="toolbar"><el-button type="primary" @click="openRoleCreate">新增角色</el-button></div>
        <el-table :data="roles" border stripe>
          <el-table-column prop="code" label="编码" width="180"/><el-table-column prop="name" label="名称" width="180"/><el-table-column prop="dataScope" label="数据范围" width="140"/><el-table-column prop="isSystem" label="系统角色" width="100"><template #default="s">{{ s.row.isSystem?'是':'否' }}</template></el-table-column><el-table-column prop="remark" label="备注"/>
          <el-table-column label="操作" width="260" fixed="right"><template #default="s"><el-button size="small" @click="openRoleEdit(s.row)">编辑</el-button><el-button size="small" type="primary" @click="openAssign(s.row)">授权</el-button><el-button size="small" type="danger" @click="deleteRole(s.row.id)">删除</el-button></template></el-table-column>
        </el-table>
      </el-tab-pane>

      <el-tab-pane label="权限管理" name="permissions">
        <div class="toolbar"><el-button type="primary" @click="openPermCreate">新增权限</el-button></div>
        <el-table :data="permissions" border stripe>
          <el-table-column prop="module" label="模块" width="140"/><el-table-column prop="code" label="权限编码" width="220"/><el-table-column prop="name" label="名称" width="220"/><el-table-column prop="permissionType" label="类型" width="120"/><el-table-column prop="remark" label="备注"/>
          <el-table-column label="操作" width="160" fixed="right"><template #default="s"><el-button size="small" @click="openPermEdit(s.row)">编辑</el-button><el-button size="small" type="danger" @click="deletePermission(s.row.id)">删除</el-button></template></el-table-column>
        </el-table>
      </el-tab-pane>

      <el-tab-pane label="用户管理" name="users">
        <div class="toolbar"><el-button type="primary" @click="openUserCreate">新增用户</el-button></div>
        <el-table :data="users" border stripe>
          <el-table-column prop="username" label="用户名" width="140"/><el-table-column prop="displayName" label="姓名" width="140"/><el-table-column prop="email" label="邮箱" width="180"/><el-table-column prop="mobile" label="手机" width="140"/><el-table-column prop="roleId" label="角色ID" width="90"/><el-table-column prop="companyId" label="公司" width="80"/><el-table-column prop="storeId" label="店铺" width="80"/><el-table-column prop="warehouseId" label="仓库" width="80"/><el-table-column prop="status" label="状态" width="100"/>
          <el-table-column label="操作" width="220" fixed="right"><template #default="s"><el-button size="small" @click="openUserEdit(s.row)">编辑</el-button><el-button size="small" type="success" @click="viewProfile(s.row)">权限画像</el-button><el-button size="small" type="danger" @click="deleteUser(s.row.id)">删除</el-button></template></el-table-column>
        </el-table>
      </el-tab-pane>
    </el-tabs>

    <el-dialog v-model="roleDialog" :title="roleForm.id?'编辑角色':'新增角色'" width="560px"><el-form label-width="100px"><el-form-item label="编码"><el-input v-model="roleForm.code"/></el-form-item><el-form-item label="名称"><el-input v-model="roleForm.name"/></el-form-item><el-form-item label="数据范围"><el-select v-model="roleForm.dataScope" style="width:100%"><el-option label="全部" value="all"/><el-option label="公司" value="company"/><el-option label="店铺" value="store"/><el-option label="仓库" value="warehouse"/><el-option label="本人" value="self"/></el-select></el-form-item><el-form-item label="系统角色"><el-switch v-model="roleForm.isSystem"/></el-form-item><el-form-item label="备注"><el-input v-model="roleForm.remark" type="textarea"/></el-form-item></el-form><template #footer><el-button @click="roleDialog=false">取消</el-button><el-button type="primary" @click="saveRole">保存</el-button></template></el-dialog>

    <el-dialog v-model="permDialog" :title="permForm.id?'编辑权限':'新增权限'" width="560px"><el-form label-width="100px"><el-form-item label="模块"><el-input v-model="permForm.module"/></el-form-item><el-form-item label="编码"><el-input v-model="permForm.code"/></el-form-item><el-form-item label="名称"><el-input v-model="permForm.name"/></el-form-item><el-form-item label="类型"><el-select v-model="permForm.permissionType" style="width:100%"><el-option label="页面" value="page"/><el-option label="按钮" value="button"/><el-option label="API" value="api"/></el-select></el-form-item><el-form-item label="备注"><el-input v-model="permForm.remark" type="textarea"/></el-form-item></el-form><template #footer><el-button @click="permDialog=false">取消</el-button><el-button type="primary" @click="savePermission">保存</el-button></template></el-dialog>

    <el-dialog v-model="userDialog" :title="userForm.id?'编辑用户':'新增用户'" width="680px"><el-form label-width="100px"><el-form-item label="用户名"><el-input v-model="userForm.username"/></el-form-item><el-form-item label="姓名"><el-input v-model="userForm.displayName"/></el-form-item><el-form-item label="邮箱"><el-input v-model="userForm.email"/></el-form-item><el-form-item label="手机"><el-input v-model="userForm.mobile"/></el-form-item><el-form-item label="角色"><el-select v-model="userForm.roleId" clearable style="width:100%"><el-option v-for="r in roles" :key="r.id" :label="`${r.name} (${r.code})`" :value="r.id"/></el-select></el-form-item><el-form-item label="公司/店铺/仓库"><el-row :gutter="8" style="width:100%"><el-col :span="8"><el-input-number v-model="userForm.companyId" :min="0" style="width:100%"/></el-col><el-col :span="8"><el-input-number v-model="userForm.storeId" :min="0" style="width:100%"/></el-col><el-col :span="8"><el-input-number v-model="userForm.warehouseId" :min="0" style="width:100%"/></el-col></el-row></el-form-item><el-form-item label="状态"><el-select v-model="userForm.status" style="width:100%"><el-option label="启用" value="active"/><el-option label="禁用" value="disabled"/><el-option label="锁定" value="locked"/></el-select></el-form-item><el-form-item label="备注"><el-input v-model="userForm.remark" type="textarea"/></el-form-item></el-form><template #footer><el-button @click="userDialog=false">取消</el-button><el-button type="primary" @click="saveUser">保存</el-button></template></el-dialog>

    <el-dialog v-model="assignDialog" title="角色授权" width="760px"><el-alert v-if="assignRole" :title="`当前角色：${assignRole.name} / ${assignRole.code}`" type="info" show-icon style="margin-bottom:12px"/><el-checkbox-group v-model="checkedPermissions"><el-row :gutter="8"><el-col :span="8" v-for="p in permissions" :key="p.id"><el-checkbox :label="p.id">{{ p.code }}</el-checkbox></el-col></el-row></el-checkbox-group><template #footer><el-button @click="assignDialog=false">取消</el-button><el-button type="primary" @click="saveAssign">保存授权</el-button></template></el-dialog>

    <el-dialog v-model="profileDialog" title="用户权限画像" width="760px"><el-descriptions border :column="2" v-if="profile.user"><el-descriptions-item label="用户">{{ profile.user.displayName }} / {{ profile.user.username }}</el-descriptions-item><el-descriptions-item label="角色">{{ profile.role?.name || '-' }}</el-descriptions-item><el-descriptions-item label="数据范围">{{ profile.dataScope }}</el-descriptions-item><el-descriptions-item label="权限数">{{ profile.permissions?.length || 0 }}</el-descriptions-item></el-descriptions><el-table :data="profile.permissions || []" border stripe style="margin-top:12px"><el-table-column label="权限编码"><template #default="s">{{ s.row }}</template></el-table-column></el-table></el-dialog>
  </div>
</template>
<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { http } from '../api/http'
const activeTab=ref('roles'), roles=ref<any[]>([]), permissions=ref<any[]>([]), users=ref<any[]>([])
const roleDialog=ref(false), permDialog=ref(false), userDialog=ref(false), assignDialog=ref(false), profileDialog=ref(false)
const roleForm=reactive<any>({id:0,code:'',name:'',dataScope:'all',isSystem:false,remark:''})
const permForm=reactive<any>({id:0,module:'',code:'',name:'',permissionType:'page',remark:''})
const userForm=reactive<any>({id:0,username:'',displayName:'',email:'',mobile:'',roleId:null,companyId:null,storeId:null,warehouseId:null,status:'active',remark:''})
const assignRole=ref<any>(null), checkedPermissions=ref<number[]>([]), profile=ref<any>({})
async function loadAll(){roles.value=(await http.get('/rbac/roles')).data; permissions.value=(await http.get('/rbac/permissions')).data; users.value=(await http.get('/rbac/users')).data}
async function seed(){const r=await http.post('/rbac/seed'); ElMessage.success(`初始化完成：角色${r.data.roles} 权限${r.data.permissions}`); await loadAll()}
function openRoleCreate(){Object.assign(roleForm,{id:0,code:'',name:'',dataScope:'all',isSystem:false,remark:''}); roleDialog.value=true}
function openRoleEdit(row:any){Object.assign(roleForm,row); roleDialog.value=true}
async function saveRole(){roleForm.id?await http.put(`/rbac/roles/${roleForm.id}`,roleForm):await http.post('/rbac/roles',roleForm); roleDialog.value=false; ElMessage.success('保存成功'); await loadAll()}
async function deleteRole(id:number){await ElMessageBox.confirm('确认删除角色？','提示'); await http.delete(`/rbac/roles/${id}`); ElMessage.success('已删除'); await loadAll()}
function openPermCreate(){Object.assign(permForm,{id:0,module:'',code:'',name:'',permissionType:'page',remark:''}); permDialog.value=true}
function openPermEdit(row:any){Object.assign(permForm,row); permDialog.value=true}
async function savePermission(){permForm.id?await http.put(`/rbac/permissions/${permForm.id}`,permForm):await http.post('/rbac/permissions',permForm); permDialog.value=false; ElMessage.success('保存成功'); await loadAll()}
async function deletePermission(id:number){await ElMessageBox.confirm('确认删除权限？','提示'); await http.delete(`/rbac/permissions/${id}`); ElMessage.success('已删除'); await loadAll()}
function openUserCreate(){Object.assign(userForm,{id:0,username:'',displayName:'',email:'',mobile:'',roleId:null,companyId:null,storeId:null,warehouseId:null,status:'active',remark:''}); userDialog.value=true}
function openUserEdit(row:any){Object.assign(userForm,row); userDialog.value=true}
async function saveUser(){userForm.id?await http.put(`/rbac/users/${userForm.id}`,userForm):await http.post('/rbac/users',userForm); userDialog.value=false; ElMessage.success('保存成功'); await loadAll()}
async function deleteUser(id:number){await ElMessageBox.confirm('确认删除用户？','提示'); await http.delete(`/rbac/users/${id}`); ElMessage.success('已删除'); await loadAll()}
async function openAssign(row:any){assignRole.value=row; const res=await http.get(`/rbac/roles/${row.id}/permissions`); checkedPermissions.value=res.data.permissionIds||[]; assignDialog.value=true}
async function saveAssign(){await http.post(`/rbac/roles/${assignRole.value.id}/permissions`,{permissionIds:checkedPermissions.value}); assignDialog.value=false; ElMessage.success('授权成功')}
async function viewProfile(row:any){profile.value=(await http.get(`/rbac/users/${row.id}/profile`)).data; profileDialog.value=true}
onMounted(loadAll)
</script>
<style scoped>.toolbar{display:flex;gap:8px;align-items:center;margin-bottom:12px}.card{padding:12px}</style>
