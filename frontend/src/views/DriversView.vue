<template>
  <div class="page">
    <div class="page-header">
      <div>
        <h1 class="page-title">DRIVER MANAGEMENT</h1>
        <p class="page-sub">{{ drivers.length }} drivers registered</p>
      </div>
      <button v-if="auth.canDispatch" class="btn btn-primary" @click="openCreate">
        + Add Driver
      </button>
    </div>

    <!-- Filter Bar -->
    <div class="filter-bar">
      <input v-model="search" class="form-control" placeholder="Search drivers..." style="max-width:260px" />
      <select v-model="filterStatus" class="form-control" style="max-width:160px">
        <option value="">All Statuses</option>
        <option>Available</option><option>Assigned</option>
        <option>OffDuty</option><option>Suspended</option>
      </select>
      <select v-model="filterLicense" class="form-control" style="max-width:160px">
        <option value="">All Classes</option>
        <option>ClassA</option><option>ClassB</option>
      </select>
    </div>

    <div class="card">
      <div v-if="loading" class="spinner"></div>
      <div v-else-if="!filtered.length" class="empty-state">
        <div class="icon">👤</div><p>No drivers found</p>
      </div>
      <div v-else class="table-wrapper">
        <table>
          <thead>
            <tr>
              <th>ID</th><th>Name</th><th>License Class</th>
              <th>Expiry Date</th><th>License</th><th>Status</th><th>Actions</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="d in filtered" :key="d.id">
              <td><span class="id-chip">{{ d.id }}</span></td>
              <td><span class="driver-name">{{ d.name }}</span></td>
              <td><span class="license-class">{{ d.licenseClass }}</span></td>
              <td>{{ formatDate(d.licenseExpiry) }}</td>
              <td>
                <span class="badge" :class="d.isLicenseValid ? 'badge-success' : 'badge-danger'">
                  {{ d.isLicenseValid ? 'Valid' : 'EXPIRED' }}
                </span>
              </td>
              <td>
                <span class="badge" :class="statusBadge(d.status)">{{ d.status }}</span>
              </td>
              <td>
                <div class="action-row">
                  <button v-if="auth.canManage" class="btn btn-ghost btn-sm" @click="openEdit(d)">Edit</button>
                  <button v-if="auth.canManage" class="btn btn-danger btn-sm" @click="confirmDelete(d)">Del</button>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <!-- Create / Edit Modal -->
    <div v-if="showModal" class="modal-overlay" @click.self="showModal=false">
      <div class="modal">
        <div class="modal-title">{{ editing ? 'Edit Driver' : 'Add Driver' }}</div>
        <div class="grid-2">
          <div class="form-group" v-if="!editing">
            <label class="form-label">Driver ID</label>
            <input v-model="form.id" class="form-control" placeholder="D006" required />
          </div>
          <div class="form-group">
            <label class="form-label">Full Name</label>
            <input v-model="form.name" class="form-control" placeholder="Full name" required />
          </div>
          <div class="form-group">
            <label class="form-label">License Class</label>
            <select v-model="form.licenseClass" class="form-control">
              <option value="ClassA">Class A (Heavy)</option>
              <option value="ClassB">Class B (Medium)</option>
            </select>
          </div>
          <div class="form-group">
            <label class="form-label">License Expiry</label>
            <input v-model="form.licenseExpiry" class="form-control" type="date" required />
          </div>
          <div class="form-group" v-if="editing">
            <label class="form-label">Status</label>
            <select v-model="form.status" class="form-control">
              <option>Available</option><option>Assigned</option>
              <option>OffDuty</option><option>Suspended</option>
            </select>
          </div>
          <div class="form-group">
            <label class="form-label">Phone</label>
            <input v-model="form.phone" class="form-control" placeholder="9876543210" />
          </div>
          <div class="form-group" style="grid-column:1/-1">
            <label class="form-label">Email</label>
            <input v-model="form.email" class="form-control" placeholder="driver@example.com" type="email" />
          </div>
        </div>
        <div v-if="formError" class="error-msg mt-4">{{ formError }}</div>
        <div class="modal-footer">
          <button class="btn btn-ghost" @click="showModal=false">Cancel</button>
          <button class="btn btn-primary" @click="save" :disabled="saving">
            {{ saving ? 'Saving...' : 'Save' }}
          </button>
        </div>
      </div>
    </div>

    <!-- Delete confirm -->
    <div v-if="deleteTarget" class="modal-overlay" @click.self="deleteTarget=null">
      <div class="modal" style="max-width:380px">
        <div class="modal-title">Confirm Delete</div>
        <p>Delete driver <strong>{{ deleteTarget.name }}</strong>? This cannot be undone.</p>
        <div class="modal-footer">
          <button class="btn btn-ghost" @click="deleteTarget=null">Cancel</button>
          <button class="btn btn-danger" @click="doDelete">Delete</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useAuthStore } from '@/stores/auth'
import { driversApi } from '@/services/api'
import { useToast } from 'vue-toastification'

const auth        = useAuthStore()
const toast       = useToast()
const drivers     = ref([])
const loading     = ref(false)
const search      = ref('')
const filterStatus  = ref('')
const filterLicense = ref('')
const showModal   = ref(false)
const editing     = ref(false)
const saving      = ref(false)
const formError   = ref('')
const deleteTarget = ref(null)

const emptyForm = () => ({
  id: '', name: '', licenseClass: 'ClassA',
  licenseExpiry: '', status: 'Available', phone: '', email: ''
})
const form = ref(emptyForm())

const filtered = computed(() => drivers.value.filter(d => {
  const q = search.value.toLowerCase()
  const matchQ = !q || d.name.toLowerCase().includes(q) || d.id.toLowerCase().includes(q)
  const matchS = !filterStatus.value  || d.status === filterStatus.value
  const matchL = !filterLicense.value || d.licenseClass === filterLicense.value
  return matchQ && matchS && matchL
}))

function statusBadge(s) {
  return { Available:'badge-success', Assigned:'badge-info', OffDuty:'badge-muted', Suspended:'badge-danger' }[s] || 'badge-muted'
}

function formatDate(d) {
  return new Date(d).toLocaleDateString('en-IN', { day:'2-digit', month:'short', year:'numeric' })
}

async function load() {
  loading.value = true
  try { drivers.value = (await driversApi.getAll()).data }
  finally { loading.value = false }
}

function openCreate() {
  editing.value = false; form.value = emptyForm()
  formError.value = ''; showModal.value = true
}

function openEdit(d) {
  editing.value = true
  form.value = {
    id: d.id, name: d.name, licenseClass: d.licenseClass,
    licenseExpiry: d.licenseExpiry.split('T')[0],
    status: d.status, phone: d.phone || '', email: d.email || ''
  }
  formError.value = ''; showModal.value = true
}

async function save() {
  saving.value = true; formError.value = ''
  try {
    if (editing.value) {
      await driversApi.update(form.value.id, form.value)
      toast.success('Driver updated')
    } else {
      await driversApi.create(form.value)
      toast.success('Driver added')
    }
    showModal.value = false; load()
  } catch(e) {
    formError.value = e.response?.data?.message || 'Save failed'
  } finally { saving.value = false }
}

function confirmDelete(d) { deleteTarget.value = d }

async function doDelete() {
  try {
    await driversApi.delete(deleteTarget.value.id)
    toast.success('Driver deleted'); deleteTarget.value = null; load()
  } catch(e) {
    toast.error(e.response?.data?.message || 'Delete failed')
  }
}

onMounted(load)
</script>

<style scoped>
.page { padding: 24px; display: flex; flex-direction: column; gap: 16px; }
.page-header { display: flex; justify-content: space-between; align-items: flex-start; }
.page-title { font-family: var(--font-display); font-size: 22px; font-weight: 700; letter-spacing: 2px; }
.page-sub   { color: var(--text-muted); font-size: 12px; margin-top: 2px; }
.filter-bar { display: flex; gap: 10px; flex-wrap: wrap; }
.id-chip {
  font-family: var(--font-display); font-size: 13px; font-weight: 700;
  color: var(--accent); background: var(--accent-glow);
  padding: 2px 8px; border-radius: 4px;
}
.driver-name { font-weight: 500; }
.license-class {
  font-family: var(--font-display); font-size: 12px;
  font-weight: 700; letter-spacing: 0.5px; color: var(--info);
}
.action-row { display: flex; gap: 6px; }
.error-msg {
  background: var(--danger-bg); border: 1px solid var(--danger);
  color: var(--danger); border-radius: var(--radius); padding: 8px 12px; font-size: 13px;
}
</style>
