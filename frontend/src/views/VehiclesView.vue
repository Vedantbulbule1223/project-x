<template>
  <div class="page">
    <div class="page-header">
      <div>
        <h1 class="page-title">FLEET MANAGEMENT</h1>
        <p class="page-sub">{{ vehicles.length }} vehicles in fleet</p>
      </div>
      <button v-if="auth.canManage" class="btn btn-primary" @click="openCreate">+ Add Vehicle</button>
    </div>

    <div class="filter-bar">
      <input v-model="search" class="form-control" placeholder="Search by VIN or model..." style="max-width:280px" />
      <select v-model="filterStatus" class="form-control" style="max-width:200px">
        <option value="">All Statuses</option>
        <option>Active</option><option>MaintenanceRequired</option>
        <option>Maintenance</option><option>OutOfService</option><option>InTransit</option>
      </select>
    </div>

    <div class="card">
      <div v-if="loading" class="spinner"></div>
      <div v-else-if="!filtered.length" class="empty-state">
        <div class="icon">🚛</div><p>No vehicles found</p>
      </div>
      <div v-else class="table-wrapper">
        <table>
          <thead>
            <tr>
              <th>VIN</th><th>Model</th><th>Class</th>
              <th>Current Odo</th><th>Last Service</th><th>KM Since Service</th>
              <th>Status</th><th>Actions</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="v in filtered" :key="v.vIN" :class="{ 'row-danger': v.needsService }">
              <td><span class="vin-chip">{{ v.vIN }}</span></td>
              <td><span class="model-name">{{ v.model }}</span></td>
              <td><span class="license-class">{{ v.licenseClass }}</span></td>
              <td>{{ v.currentOdometer.toLocaleString() }} km</td>
              <td>{{ v.lastServiceOdometer.toLocaleString() }} km</td>
              <td>
                <div class="km-bar-wrap">
                  <div class="km-bar" :class="kmBarClass(v)" :style="{ width: kmPct(v) + '%' }"></div>
                </div>
                <span :class="v.needsService ? 'text-danger' : ''">
                  {{ v.kmSinceService.toLocaleString() }} km
                </span>
              </td>
              <td><span class="badge" :class="statusBadge(v.status)">{{ formatStatus(v.status) }}</span></td>
              <td>
                <div class="action-row">
                  <button v-if="auth.isTech" class="btn btn-outline btn-sm" @click="openOdo(v)">Odometer</button>
                  <button v-if="auth.canManage" class="btn btn-ghost btn-sm" @click="openStatus(v)">Status</button>
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <!-- Add Vehicle Modal -->
    <div v-if="showCreate" class="modal-overlay" @click.self="showCreate=false">
      <div class="modal">
        <div class="modal-title">Add Vehicle</div>
        <div class="grid-2">
          <div class="form-group">
            <label class="form-label">VIN</label>
            <input v-model="createForm.vIN" class="form-control" placeholder="V106" required />
          </div>
          <div class="form-group">
            <label class="form-label">Model</label>
            <input v-model="createForm.model" class="form-control" placeholder="BharatBenz 3523R" />
          </div>
          <div class="form-group">
            <label class="form-label">License Class</label>
            <select v-model="createForm.licenseClass" class="form-control">
              <option value="ClassA">Class A (Heavy)</option>
              <option value="ClassB">Class B (Medium)</option>
            </select>
          </div>
          <div class="form-group">
            <label class="form-label">Current Odometer (km)</label>
            <input v-model.number="createForm.currentOdometer" class="form-control" type="number" min="0" />
          </div>
          <div class="form-group">
            <label class="form-label">Last Service Odometer (km)</label>
            <input v-model.number="createForm.lastServiceOdometer" class="form-control" type="number" min="0" />
          </div>
          <div class="form-group" style="grid-column:1/-1">
            <label class="form-label">Notes</label>
            <input v-model="createForm.notes" class="form-control" placeholder="Optional notes" />
          </div>
        </div>
        <div v-if="formError" class="error-msg mt-4">{{ formError }}</div>
        <div class="modal-footer">
          <button class="btn btn-ghost" @click="showCreate=false">Cancel</button>
          <button class="btn btn-primary" @click="doCreate" :disabled="saving">{{ saving?'Saving...':'Add' }}</button>
        </div>
      </div>
    </div>

    <!-- Odometer Modal -->
    <div v-if="odoTarget" class="modal-overlay" @click.self="odoTarget=null">
      <div class="modal" style="max-width:380px">
        <div class="modal-title">Update Odometer</div>
        <p class="text-muted" style="margin-bottom:16px">
          Vehicle: <strong>{{ odoTarget.model }}</strong> ({{ odoTarget.vIN }})<br>
          Current: <strong>{{ odoTarget.currentOdometer.toLocaleString() }} km</strong>
        </p>
        <div class="form-group">
          <label class="form-label">New Odometer Reading (km)</label>
          <input v-model.number="odoValue" class="form-control" type="number"
            :min="odoTarget.currentOdometer" placeholder="Enter new reading" />
        </div>
        <div v-if="odoTarget.kmSinceService >= 8000" class="alert-banner warning mt-4">
          ⚠ Approaching 10,000 km service threshold
          ({{ odoTarget.kmSinceService.toLocaleString() }} km since last service)
        </div>
        <div v-if="formError" class="error-msg mt-4">{{ formError }}</div>
        <div class="modal-footer">
          <button class="btn btn-ghost" @click="odoTarget=null">Cancel</button>
          <button class="btn btn-primary" @click="doOdo" :disabled="saving">{{ saving?'Updating...':'Update' }}</button>
        </div>
      </div>
    </div>

    <!-- Status Modal -->
    <div v-if="statusTarget" class="modal-overlay" @click.self="statusTarget=null">
      <div class="modal" style="max-width:380px">
        <div class="modal-title">Change Status</div>
        <p class="text-muted" style="margin-bottom:16px">
          Vehicle: <strong>{{ statusTarget.model }}</strong>
        </p>
        <div class="form-group">
          <label class="form-label">New Status</label>
          <select v-model="statusValue" class="form-control">
            <option>Active</option><option>Maintenance</option>
            <option>OutOfService</option>
          </select>
        </div>
        <div v-if="formError" class="error-msg mt-4">{{ formError }}</div>
        <div class="modal-footer">
          <button class="btn btn-ghost" @click="statusTarget=null">Cancel</button>
          <button class="btn btn-primary" @click="doStatus" :disabled="saving">{{ saving?'Saving...':'Update' }}</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useAuthStore } from '@/stores/auth'
import { vehiclesApi } from '@/services/api'
import { useToast } from 'vue-toastification'

const auth     = useAuthStore()
const toast    = useToast()
const vehicles = ref([])
const loading  = ref(false)
const search   = ref('')
const filterStatus = ref('')
const saving   = ref(false)
const formError = ref('')

const showCreate = ref(false)
const createForm = ref({ vIN:'', model:'', licenseClass:'ClassA', currentOdometer:0, lastServiceOdometer:0, notes:'' })

const odoTarget  = ref(null)
const odoValue   = ref(0)
const statusTarget = ref(null)
const statusValue  = ref('Active')

const filtered = computed(() => vehicles.value.filter(v => {
  const q = search.value.toLowerCase()
  const matchQ = !q || v.vIN.toLowerCase().includes(q) || v.model.toLowerCase().includes(q)
  const matchS = !filterStatus.value || v.status === filterStatus.value
  return matchQ && matchS
}))

function formatStatus(s) {
  return { MaintenanceRequired:'Needs Service', OutOfService:'Out of Service', InTransit:'In Transit' }[s] || s
}
function statusBadge(s) {
  return { Active:'badge-success', MaintenanceRequired:'badge-danger', Maintenance:'badge-warning',
    OutOfService:'badge-muted', InTransit:'badge-info' }[s] || 'badge-muted'
}
function kmBarClass(v) {
  if (v.kmSinceService >= 10000) return 'bar-danger'
  if (v.kmSinceService >= 7000)  return 'bar-warning'
  return 'bar-safe'
}
function kmPct(v) { return Math.min((v.kmSinceService / 10000) * 100, 100) }

async function load() {
  loading.value = true
  try { vehicles.value = (await vehiclesApi.getAll()).data }
  finally { loading.value = false }
}

function openCreate() { createForm.value = { vIN:'', model:'', licenseClass:'ClassA', currentOdometer:0, lastServiceOdometer:0, notes:'' }; formError.value=''; showCreate.value=true }
async function doCreate() {
  saving.value=true; formError.value=''
  try { await vehiclesApi.create(createForm.value); toast.success('Vehicle added'); showCreate.value=false; load() }
  catch(e) { formError.value = e.response?.data?.message || 'Failed' }
  finally { saving.value=false }
}

function openOdo(v) { odoTarget.value=v; odoValue.value=v.currentOdometer; formError.value='' }
async function doOdo() {
  saving.value=true; formError.value=''
  try {
    await vehiclesApi.updateOdometer(odoTarget.value.vIN, odoValue.value)
    toast.success('Odometer updated'); odoTarget.value=null; load()
  } catch(e) { formError.value = e.response?.data?.message || 'Failed' }
  finally { saving.value=false }
}

function openStatus(v) { statusTarget.value=v; statusValue.value=v.status; formError.value='' }
async function doStatus() {
  saving.value=true; formError.value=''
  try {
    await vehiclesApi.updateStatus(statusTarget.value.vIN, statusValue.value)
    toast.success('Status updated'); statusTarget.value=null; load()
  } catch(e) { formError.value = e.response?.data?.message || 'Failed' }
  finally { saving.value=false }
}

onMounted(load)
</script>

<style scoped>
.page { padding: 24px; display: flex; flex-direction: column; gap: 16px; }
.page-header { display: flex; justify-content: space-between; align-items: flex-start; }
.page-title { font-family: var(--font-display); font-size: 22px; font-weight: 700; letter-spacing: 2px; }
.page-sub   { color: var(--text-muted); font-size: 12px; margin-top: 2px; }
.filter-bar { display: flex; gap: 10px; flex-wrap: wrap; }
.vin-chip { font-family: var(--font-display); font-size: 13px; font-weight: 700; color: var(--accent); background: var(--accent-glow); padding: 2px 8px; border-radius: 4px; }
.model-name { font-weight: 500; }
.license-class { font-family: var(--font-display); font-size: 12px; font-weight: 700; color: var(--info); }
.km-bar-wrap { height: 4px; background: var(--bg-secondary); border-radius: 2px; margin-bottom: 4px; width: 80px; }
.km-bar { height: 100%; border-radius: 2px; transition: width 0.4s; }
.bar-safe    { background: var(--success); }
.bar-warning { background: var(--warning); }
.bar-danger  { background: var(--danger); }
.text-danger { color: var(--danger); font-weight: 600; }
.action-row { display: flex; gap: 6px; }
.row-danger td { background: rgba(255,23,68,0.04); }
.error-msg { background: var(--danger-bg); border: 1px solid var(--danger); color: var(--danger); border-radius: var(--radius); padding: 8px 12px; font-size: 13px; }
</style>
