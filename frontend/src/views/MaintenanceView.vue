<template>
  <div class="page">
    <div class="page-header">
      <div>
        <h1 class="page-title">MAINTENANCE CENTER</h1>
        <p class="page-sub">{{ alertVehicles.length }} vehicle(s) need attention</p>
      </div>
      <button v-if="auth.isTech" class="btn btn-primary" @click="openLog">+ Log Maintenance</button>
    </div>

    <!-- Alert vehicles -->
    <div v-if="alertVehicles.length" class="alert-section">
      <div class="section-label">⚠ Vehicles Requiring Maintenance</div>
      <div class="alert-grid">
        <div v-for="v in alertVehicles" :key="v.vIN" class="alert-card"
          :class="v.status === 'MaintenanceRequired' ? 'critical' : 'warning'">
          <div class="ac-vin">{{ v.vIN }}</div>
          <div class="ac-model">{{ v.model }}</div>
          <div class="ac-stat">
            <span>{{ v.kmSinceService.toLocaleString() }} km overdue</span>
            <span class="badge" :class="v.status==='MaintenanceRequired'?'badge-danger':'badge-warning'">
              {{ formatStatus(v.status) }}
            </span>
          </div>
          <div class="progress-wrap">
            <div class="progress-bar" :style="{ width: Math.min(v.kmSinceService/10000*100,100)+'%' }"></div>
          </div>
          <button v-if="auth.isTech" class="btn btn-primary btn-sm" style="margin-top:10px"
            @click="prefillLog(v)">Log Service →</button>
        </div>
      </div>
    </div>

    <!-- Maintenance History -->
    <div class="card">
      <div class="card-header">
        <span class="card-title">📋 Service History</span>
        <div style="display:flex;gap:10px">
          <select v-model="filterVIN" class="form-control" style="max-width:160px">
            <option value="">All Vehicles</option>
            <option v-for="v in vehicles" :key="v.vIN" :value="v.vIN">{{ v.vIN }}</option>
          </select>
        </div>
      </div>
      <div v-if="loading" class="spinner"></div>
      <div v-else-if="!filteredLogs.length" class="empty-state">
        <div class="icon">🔧</div><p>No maintenance records found</p>
      </div>
      <div v-else class="table-wrapper">
        <table>
          <thead>
            <tr>
              <th>VIN</th><th>Vehicle</th><th>Service Date</th>
              <th>Odometer</th><th>Description</th><th>Cost (₹)</th><th>Performed By</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="l in filteredLogs" :key="l.id">
              <td><span class="vin-chip">{{ l.vehicleVIN }}</span></td>
              <td>{{ l.vehicleModel }}</td>
              <td>{{ formatDate(l.serviceDate) }}</td>
              <td>{{ l.odometerAtService.toLocaleString() }} km</td>
              <td>{{ l.description }}</td>
              <td>₹{{ l.cost.toLocaleString() }}</td>
              <td>{{ l.performedByName }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <!-- Log Maintenance Modal -->
    <div v-if="showModal" class="modal-overlay" @click.self="showModal=false">
      <div class="modal">
        <div class="modal-title">Log Maintenance Service</div>
        <div class="restore-note">
          💡 Logging maintenance on a vehicle in <strong>Maintenance Required</strong> status
          will automatically restore it to <strong>Active</strong>.
        </div>
        <div class="grid-2" style="margin-top:16px">
          <div class="form-group" style="grid-column:1/-1">
            <label class="form-label">Vehicle</label>
            <select v-model="logForm.vehicleVIN" class="form-control">
              <option value="">-- Select Vehicle --</option>
              <option v-for="v in vehicles" :key="v.vIN" :value="v.vIN">
                {{ v.vIN }} – {{ v.model }} ({{ formatStatus(v.status) }})
              </option>
            </select>
          </div>
          <div class="form-group" style="grid-column:1/-1">
            <label class="form-label">Description of Service</label>
            <input v-model="logForm.description" class="form-control" placeholder="e.g. Engine oil change, filter replacement..." />
          </div>
          <div class="form-group">
            <label class="form-label">Odometer at Service (km)</label>
            <input v-model.number="logForm.odometerAtService" class="form-control" type="number" min="0" />
          </div>
          <div class="form-group">
            <label class="form-label">Cost (₹)</label>
            <input v-model.number="logForm.cost" class="form-control" type="number" min="0" />
          </div>
          <div class="form-group" style="grid-column:1/-1">
            <label class="form-label">Parts Replaced (optional)</label>
            <input v-model="logForm.parts" class="form-control" placeholder="e.g. Air filter, oil filter..." />
          </div>
        </div>
        <div v-if="formError" class="error-msg mt-4">{{ formError }}</div>
        <div class="modal-footer">
          <button class="btn btn-ghost" @click="showModal=false">Cancel</button>
          <button class="btn btn-primary" @click="doLog" :disabled="saving">{{ saving?'Saving...':'Log Service' }}</button>
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

const auth    = useAuthStore()
const toast   = useToast()
const vehicles = ref([])
const logs    = ref([])
const loading = ref(false)
const saving  = ref(false)
const formError = ref('')
const showModal = ref(false)
const filterVIN = ref('')

const logForm = ref({ vehicleVIN:'', description:'', odometerAtService:0, cost:0, parts:'' })

const alertVehicles = computed(() =>
  vehicles.value.filter(v => ['MaintenanceRequired','Maintenance'].includes(v.status) || v.needsService)
)
const filteredLogs = computed(() =>
  !filterVIN.value ? logs.value : logs.value.filter(l => l.vehicleVIN === filterVIN.value)
)

function formatStatus(s) {
  return { MaintenanceRequired:'Needs Service', OutOfService:'Out of Service', InTransit:'In Transit' }[s] || s
}
function formatDate(d) {
  return new Date(d).toLocaleString('en-IN', { day:'2-digit', month:'short', year:'numeric', hour:'2-digit', minute:'2-digit' })
}

async function load() {
  loading.value = true
  try {
    vehicles.value = (await vehiclesApi.getAll()).data
    // Fetch logs for all vehicles that have maintenance history
    const logResults = await Promise.all(
      vehicles.value.slice(0,10).map(v => vehiclesApi.getLogs(v.vIN).catch(() => ({ data:[] })))
    )
    logs.value = logResults.flatMap(r => r.data).sort((a,b) => new Date(b.serviceDate) - new Date(a.serviceDate))
  } finally { loading.value = false }
}

function openLog() { logForm.value = { vehicleVIN:'', description:'', odometerAtService:0, cost:0, parts:'' }; formError.value=''; showModal.value=true }
function prefillLog(v) { logForm.value.vehicleVIN=v.vIN; logForm.value.odometerAtService=v.currentOdometer; showModal.value=true }

async function doLog() {
  saving.value=true; formError.value=''
  try {
    await vehiclesApi.logMaintenance(logForm.value)
    toast.success('Maintenance logged! Vehicle status restored to Active.')
    showModal.value=false; load()
  } catch(e) { formError.value = e.response?.data?.message || 'Failed' }
  finally { saving.value=false }
}

onMounted(load)
</script>

<style scoped>
.page { padding: 24px; display: flex; flex-direction: column; gap: 20px; }
.page-header { display: flex; justify-content: space-between; align-items: flex-start; }
.page-title { font-family: var(--font-display); font-size: 22px; font-weight: 700; letter-spacing: 2px; }
.page-sub   { color: var(--text-muted); font-size: 12px; margin-top: 2px; }
.section-label { font-family: var(--font-display); font-size: 13px; font-weight: 700; letter-spacing: 1px; color: var(--warning); text-transform: uppercase; margin-bottom: 12px; }
.alert-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(240px, 1fr)); gap: 14px; }
.alert-card { background: var(--bg-card); border-radius: var(--radius-lg); padding: 16px; border: 1px solid var(--border); }
.alert-card.critical { border-color: var(--danger); }
.alert-card.warning  { border-color: var(--warning); }
.ac-vin { font-family: var(--font-display); font-size: 18px; font-weight: 700; color: var(--danger); }
.ac-model { font-size: 13px; color: var(--text-secondary); margin-bottom: 8px; }
.ac-stat { display: flex; justify-content: space-between; align-items: center; font-size: 12px; margin-bottom: 8px; }
.progress-wrap { height: 6px; background: var(--bg-secondary); border-radius: 3px; overflow: hidden; }
.progress-bar { height: 100%; background: var(--danger); border-radius: 3px; }
.vin-chip { font-family: var(--font-display); font-weight: 700; color: var(--accent); background: var(--accent-glow); padding: 2px 8px; border-radius: 4px; }
.restore-note { background: var(--info-bg); border: 1px solid var(--info); color: var(--info); border-radius: var(--radius); padding: 10px 14px; font-size: 13px; }
.error-msg { background: var(--danger-bg); border: 1px solid var(--danger); color: var(--danger); border-radius: var(--radius); padding: 8px 12px; font-size: 13px; }
</style>
