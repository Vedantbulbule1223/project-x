<template>
  <div class="page">
    <div class="page-header">
      <div>
        <h1 class="page-title">ASSIGNMENT MANAGEMENT</h1>
        <p class="page-sub">{{ assignments.length }} total assignments</p>
      </div>
      <button v-if="auth.canDispatch" class="btn btn-primary" @click="openCreate">+ New Assignment</button>
    </div>

    <div class="filter-bar">
      <input v-model="search" class="form-control" placeholder="Search driver or vehicle..." style="max-width:280px" />
      <select v-model="filterStatus" class="form-control" style="max-width:160px">
        <option value="">All Statuses</option>
        <option>Pending</option><option>Active</option>
        <option>Completed</option><option>Cancelled</option>
      </select>
    </div>

    <div class="card">
      <div v-if="loading" class="spinner"></div>
      <div v-else-if="!filtered.length" class="empty-state">
        <div class="icon">🗺️</div><p>No assignments found</p>
      </div>
      <div v-else class="table-wrapper">
        <table>
          <thead>
            <tr>
              <th>#</th><th>Route</th><th>Driver</th><th>Vehicle</th>
              <th>Scheduled</th><th>Status</th><th>Actions</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="a in filtered" :key="a.id">
              <td><span class="id-badge">#{{ a.id }}</span></td>
              <td>
                <div class="route-name">{{ a.route }}</div>
                <div class="route-path text-muted text-sm">{{ a.origin }} → {{ a.destination }}</div>
              </td>
              <td>{{ a.driverName }}</td>
              <td>
                <div>{{ a.vehicleVIN }}</div>
                <div class="text-muted text-sm">{{ a.vehicleModel }}</div>
              </td>
              <td>{{ formatDate(a.scheduledStart) }}</td>
              <td><span class="badge" :class="statusBadge(a.status)">{{ a.status }}</span></td>
              <td>
                <div class="action-row" v-if="auth.canDispatch">
                  <button v-if="a.status==='Pending'" class="btn btn-success btn-sm" @click="updateStatus(a,'Active')">Activate</button>
                  <button v-if="a.status==='Active'" class="btn btn-outline btn-sm" @click="updateStatus(a,'Completed')">Complete</button>
                  <button v-if="['Pending','Active'].includes(a.status)" class="btn btn-danger btn-sm" @click="updateStatus(a,'Cancelled')">Cancel</button>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <!-- Create Assignment Modal -->
    <div v-if="showModal" class="modal-overlay" @click.self="showModal=false">
      <div class="modal" style="max-width:560px">
        <div class="modal-title">New Assignment</div>

        <!-- Validation rules reminder -->
        <div class="rules-note">
          <div class="rule-item">✅ Only valid licenses allowed</div>
          <div class="rule-item">✅ Only Active vehicles allowed</div>
          <div class="rule-item">✅ No double-booking</div>
          <div class="rule-item">✅ License class must match</div>
        </div>

        <div class="grid-2" style="margin-top:16px">
          <div class="form-group">
            <label class="form-label">Driver</label>
            <select v-model="form.driverId" class="form-control">
              <option value="">-- Select Driver --</option>
              <option v-for="d in availableDrivers" :key="d.id" :value="d.id"
                :disabled="!d.isLicenseValid">
                {{ d.name }} ({{ d.licenseClass }}) {{ !d.isLicenseValid ? '⚠ EXPIRED' : '' }}
              </option>
            </select>
            <div v-if="selectedDriver && !selectedDriver.isLicenseValid" class="field-error">
              ⚠ This driver's license is expired!
            </div>
          </div>

          <div class="form-group">
            <label class="form-label">Vehicle</label>
            <select v-model="form.vehicleVIN" class="form-control">
              <option value="">-- Select Vehicle --</option>
              <option v-for="v in activeVehicles" :key="v.vIN" :value="v.vIN">
                {{ v.vIN }} – {{ v.model }} ({{ v.licenseClass }})
              </option>
            </select>
          </div>

          <div class="form-group" style="grid-column:1/-1">
            <label class="form-label">Route Name</label>
            <input v-model="form.route" class="form-control" placeholder="e.g. Mumbai–Pune Cold Chain" />
          </div>
          <div class="form-group">
            <label class="form-label">Origin</label>
            <input v-model="form.origin" class="form-control" placeholder="Mumbai Warehouse" />
          </div>
          <div class="form-group">
            <label class="form-label">Destination</label>
            <input v-model="form.destination" class="form-control" placeholder="Pune Distribution" />
          </div>
          <div class="form-group">
            <label class="form-label">Scheduled Start</label>
            <input v-model="form.scheduledStart" class="form-control" type="datetime-local" />
          </div>
          <div class="form-group">
            <label class="form-label">Scheduled End (optional)</label>
            <input v-model="form.scheduledEnd" class="form-control" type="datetime-local" />
          </div>
        </div>

        <div v-if="formError" class="error-msg mt-4">{{ formError }}</div>

        <div class="modal-footer">
          <button class="btn btn-ghost" @click="showModal=false">Cancel</button>
          <button class="btn btn-primary" @click="doCreate" :disabled="saving">
            {{ saving ? 'Creating...' : 'Create Assignment' }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useAuthStore } from '@/stores/auth'
import { assignmentsApi, driversApi, vehiclesApi } from '@/services/api'
import { useToast } from 'vue-toastification'

const auth        = useAuthStore()
const toast       = useToast()
const assignments = ref([])
const drivers     = ref([])
const vehicles    = ref([])
const loading     = ref(false)
const saving      = ref(false)
const formError   = ref('')
const search      = ref('')
const filterStatus = ref('')
const showModal   = ref(false)

const form = ref({
  driverId:'', vehicleVIN:'', route:'',
  origin:'', destination:'', scheduledStart:'', scheduledEnd:''
})

const filtered = computed(() => assignments.value.filter(a => {
  const q = search.value.toLowerCase()
  const mQ = !q || a.driverName.toLowerCase().includes(q) ||
    a.vehicleVIN.toLowerCase().includes(q) || a.route.toLowerCase().includes(q)
  const mS = !filterStatus.value || a.status === filterStatus.value
  return mQ && mS
}))

const availableDrivers = computed(() => drivers.value)
const activeVehicles   = computed(() => vehicles.value.filter(v => v.status === 'Active'))
const selectedDriver   = computed(() => drivers.value.find(d => d.id === form.value.driverId))

function statusBadge(s) {
  return { Pending:'badge-warning', Active:'badge-success', Completed:'badge-muted', Cancelled:'badge-danger' }[s] || 'badge-muted'
}
function formatDate(d) {
  return new Date(d).toLocaleString('en-IN', { day:'2-digit', month:'short', hour:'2-digit', minute:'2-digit' })
}

async function load() {
  loading.value = true
  try {
    const [as_, dr, vh] = await Promise.all([
      assignmentsApi.getAll(), driversApi.getAll(), vehiclesApi.getAll()
    ])
    assignments.value = as_.data
    drivers.value = dr.data
    vehicles.value = vh.data
  } finally { loading.value = false }
}

function openCreate() {
  form.value = { driverId:'', vehicleVIN:'', route:'', origin:'', destination:'', scheduledStart:'', scheduledEnd:'' }
  formError.value = ''; showModal.value = true
}

async function doCreate() {
  saving.value = true; formError.value = ''
  try {
    const payload = {
      ...form.value,
      scheduledStart: new Date(form.value.scheduledStart).toISOString(),
      scheduledEnd: form.value.scheduledEnd ? new Date(form.value.scheduledEnd).toISOString() : null
    }
    await assignmentsApi.create(payload)
    toast.success('Assignment created successfully!')
    showModal.value = false; load()
  } catch(e) {
    formError.value = e.response?.data?.message || 'Assignment creation failed'
  } finally { saving.value = false }
}

async function updateStatus(a, status) {
  try {
    await assignmentsApi.updateStatus(a.id, status)
    toast.success(`Assignment ${status.toLowerCase()}`)
    load()
  } catch(e) {
    toast.error(e.response?.data?.message || 'Update failed')
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
.id-badge { font-family: var(--font-display); font-weight: 700; color: var(--text-muted); }
.route-name { font-weight: 500; }
.route-path { margin-top: 2px; }
.action-row { display: flex; gap: 6px; }
.rules-note {
  display: grid; grid-template-columns: 1fr 1fr; gap: 6px;
  background: var(--bg-secondary); border: 1px solid var(--border);
  border-radius: var(--radius); padding: 12px;
}
.rule-item { font-size: 12px; color: var(--success); }
.field-error { font-size: 12px; color: var(--danger); margin-top: 4px; }
.error-msg { background: var(--danger-bg); border: 1px solid var(--danger); color: var(--danger); border-radius: var(--radius); padding: 8px 12px; font-size: 13px; }
</style>
