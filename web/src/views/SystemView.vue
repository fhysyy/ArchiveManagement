<template>
  <div class="page">
    <h3 style="margin-top: 0">系统管理</h3>

    <el-tabs v-model="tab">
      <el-tab-pane label="运行状态" name="health">
        <el-descriptions border :column="2" style="max-width: 560px">
          <el-descriptions-item label="状态">
            <el-tag :type="health?.status === 'ok' ? 'success' : 'danger'">{{ health?.status }}</el-tag>
          </el-descriptions-item>
          <el-descriptions-item label="数据库">
            <el-tag :type="health?.dbOk ? 'success' : 'danger'">{{ health?.dbOk ? '已连接' : '未连接' }}</el-tag>
          </el-descriptions-item>
          <el-descriptions-item label="版本">{{ health?.version }}</el-descriptions-item>
          <el-descriptions-item label="服务端时间">{{ health?.time }}</el-descriptions-item>
        </el-descriptions>
        <div style="margin-top: 12px">
          <el-button type="primary" @click="onBackup" :loading="backing">触发备份</el-button>
          <span v-if="backupResult" style="margin-left: 12px; color: #475569">{{ backupResult.message }}</span>
        </div>
      </el-tab-pane>

      <el-tab-pane label="用户" name="users">
        <div style="margin-bottom: 12px"><el-button type="primary" @click="openUser">新增用户</el-button></div>
        <el-table :data="users" border>
          <el-table-column prop="username" label="账号" width="140" />
          <el-table-column prop="name" label="姓名" width="140" />
          <el-table-column prop="dept" label="部门" />
          <el-table-column prop="roleName" label="角色" width="140" />
          <el-table-column label="操作" width="100">
            <template #default="{ row }">
              <el-button size="small" type="danger" plain @click="onDelUser(row)">删除</el-button>
            </template>
          </el-table-column>
        </el-table>
      </el-tab-pane>

      <el-tab-pane label="角色" name="roles">
        <div style="margin-bottom: 12px"><el-button type="primary" @click="openRole">新增角色</el-button></div>
        <el-table :data="roles" border>
          <el-table-column prop="code" label="编码" width="200" />
          <el-table-column prop="name" label="名称" />
          <el-table-column label="操作" width="180">
            <template #default="{ row }">
              <el-button size="small" type="primary" plain @click="openPerm(row)">配置权限</el-button>
              <el-button size="small" type="danger" plain @click="onDelRole(row)">删除</el-button>
            </template>
          </el-table-column>
        </el-table>
      </el-tab-pane>
    </el-tabs>

    <!-- 五维权限配置 -->
    <el-dialog v-model="showPerm" :title="`配置权限 —— ${permRole?.name ?? ''}`" width="720px">
      <div style="margin-bottom: 8px">
        <el-select v-model="newPerm.categoryId" placeholder="门类(留空=全部)" clearable style="width: 180px; margin-right: 8px">
          <el-option v-for="c in categories" :key="c.id" :label="c.name" :value="c.id" />
        </el-select>
        <el-select v-model="newPerm.op" style="width: 140px; margin-right: 8px">
          <el-option v-for="o in opOpts" :key="o" :label="o" :value="o" />
        </el-select>
        <el-select v-model="newPerm.maxSecrecy" style="width: 140px; margin-right: 8px">
          <el-option v-for="s in secrecyOpts" :key="s" :label="s" :value="s" />
        </el-select>
        <el-button type="primary" @click="addPerm">添加</el-button>
      </div>
      <el-table :data="permList" size="small" border>
        <el-table-column prop="categoryName" label="门类" />
        <el-table-column prop="op" label="操作" width="90" />
        <el-table-column prop="maxSecrecy" label="最高密级" width="120" />
        <el-table-column label="操作" width="80">
          <template #default="{ $index }">
            <el-button size="small" type="danger" plain @click="removePerm($index)">移除</el-button>
          </template>
        </el-table-column>
      </el-table>
      <template #footer>
        <el-button @click="showPerm = false">取消</el-button>
        <el-button type="primary" @click="onSavePerm">保存（整量覆盖）</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="showUser" title="新增用户" width="420px">
      <el-form label-width="80px">
        <el-form-item label="账号"><el-input v-model="userForm.username" /></el-form-item>
        <el-form-item label="姓名"><el-input v-model="userForm.name" /></el-form-item>
        <el-form-item label="部门"><el-input v-model="userForm.dept" /></el-form-item>
        <el-form-item label="密码"><el-input v-model="userForm.password" show-password /></el-form-item>
        <el-form-item label="角色">
          <el-select v-model="userForm.roleCode" style="width: 100%">
            <el-option v-for="r in roles" :key="r.code" :label="r.name" :value="r.code" />
          </el-select>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showUser = false">取消</el-button>
        <el-button type="primary" @click="onSubmitUser">保存</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="showRole" title="新增角色" width="380px">
      <el-form label-width="80px">
        <el-form-item label="编码"><el-input v-model="roleForm.code" placeholder="如 ARCHIVIST" /></el-form-item>
        <el-form-item label="名称"><el-input v-model="roleForm.name" /></el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="showRole = false">取消</el-button>
        <el-button type="primary" @click="onSubmitRole">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, watch } from 'vue'
import { getHealth, triggerBackup, listUsers, createUser, deleteUser, listRoles, createRole, deleteRole, getPermissions, setPermissions } from '@/api/system'
import { listCategories } from '@/api/category'
import type { HealthDto, BackupResultDto, UserDto, CreateUserDto, RoleDto, CreateRoleDto, PermissionDto, CategoryDto } from '@/types/models'
import { ElMessage, ElMessageBox } from 'element-plus'

const tab = ref('health')
const health = ref<HealthDto>()
const backupResult = ref<BackupResultDto>()
const backing = ref(false)
const users = ref<UserDto[]>([])
const roles = ref<RoleDto[]>([])
const showUser = ref(false)
const showRole = ref(false)
const userForm = reactive<CreateUserDto>({ username: '', name: '', dept: '', password: '', roleCode: 'USER' })
const roleForm = reactive<CreateRoleDto>({ code: '', name: '' })

// 五维权限
const showPerm = ref(false)
const permRole = ref<RoleDto | null>(null)
const permList = ref<PermissionDto[]>([])
const categories = ref<CategoryDto[]>([])
const opOpts = ['view', 'edit', 'audit', 'destroy']
const secrecyOpts = ['公开', '内部', '秘密', '机密', '绝密']
const newPerm = reactive<{ categoryId?: number; op: string; maxSecrecy: string }>({ categoryId: undefined, op: 'view', maxSecrecy: '公开' })

async function loadCategories() { const { data } = await listCategories(); categories.value = data.data }
async function openPerm(row: RoleDto) {
  permRole.value = row
  const { data } = await getPermissions(row.id)
  permList.value = data.data
  showPerm.value = true
}
function addPerm() {
  const cat = categories.value.find(c => c.id === newPerm.categoryId)
  permList.value.push({
    id: 0, roleId: permRole.value?.id ?? 0,
    categoryId: newPerm.categoryId, categoryName: cat?.name ?? '全部门类',
    op: newPerm.op, maxSecrecy: newPerm.maxSecrecy
  })
}
function removePerm(i: number) { permList.value.splice(i, 1) }
async function onSavePerm() {
  if (!permRole.value) return
  await setPermissions(permRole.value.id, { items: permList.value })
  showPerm.value = false
  ElMessage.success('权限已保存')
}

async function loadHealth() { const { data } = await getHealth(); health.value = data.data }
async function loadUsers() { const { data } = await listUsers(); users.value = data.data }
async function loadRoles() { const { data } = await listRoles(); roles.value = data.data }

async function onBackup() {
  backing.value = true
  try { const { data } = await triggerBackup(); backupResult.value = data.data } finally { backing.value = false }
}
function openUser() {
  Object.assign(userForm, { username: '', name: '', dept: '', password: '', roleCode: roles.value[0]?.code ?? 'USER' })
  showUser.value = true
}
async function onSubmitUser() {
  await createUser({ ...userForm })
  showUser.value = false
  ElMessage.success('用户已创建')
  loadUsers()
}
async function onDelUser(row: UserDto) {
  await ElMessageBox.confirm(`确认删除用户 ${row.username}？`, '提示', { type: 'warning' })
  await deleteUser(row.id)
  ElMessage.success('已删除')
  loadUsers()
}
function openRole() { Object.assign(roleForm, { code: '', name: '' }); showRole.value = true }
async function onSubmitRole() {
  await createRole({ ...roleForm })
  showRole.value = false
  ElMessage.success('角色已创建')
  loadRoles()
}
async function onDelRole(row: RoleDto) {
  await ElMessageBox.confirm(`确认删除角色 ${row.code}？`, '提示', { type: 'warning' })
  await deleteRole(row.id)
  ElMessage.success('已删除')
  loadRoles()
}

function refresh() {
  if (tab.value === 'health') loadHealth()
  else if (tab.value === 'users') { loadUsers(); loadRoles() }
  else loadRoles()
}
watch(tab, refresh)
onMounted(async () => { await loadHealth(); await loadRoles(); await loadCategories() })
</script>

<style scoped>
.page { padding: 16px; }
</style>
