<template>
  <div class="page">
    <div class="page-header">
      <div>
        <h1 class="page-title">PULSE DASHBOARD</h1>
        <p class="page-sub">Live operational overview · {{ now }}</p>
      </div>
      <button class="btn btn-outline btn-sm" @click="refresh">⟳ Refresh</button>
    </div>

    <!-- Stat Cards -->
    <div class="stats-grid">
      <div class="stat-card" v-for="s in statCards" :key="s.label" :class="s.color">
        <div class="stat-icon">{{ s.icon }}</div>
        <div class="stat-value">{{ s.value }}</div>
        <div class="stat-label">{{ s.label }}</div>
      </div>
    </div>

    <!-- Row: Alerts + Recent Assignments -->
    <div class="dash-row">
      <!-- Maintenance Alerts -->
      <div class="card flex-1">
        <div class="card-header">
          <span class="card-title">⚠ Maintenance Alerts</span>
          <span class="badge badge-warning">{{ alerts.length }}</span>
        </div>
        <div v-if="loading" class="spinner"></div>
        <div v-else-if="!alerts.length" class="empty-state">
          <div class="icon">✅</div>
          <p>All vehicles are within service limits</p>
        </div>
        <div v-else class="alert-list">
          <div v-for="a in alerts" :key="a.vehicleVIN" class="alert-item">
            <div class="alert-item-left">
              <div class="alert-vin">{{ a.vehicleVIN }}</div>
              <div class="alert-model">{{ a.vehicleModel }}</div>
            </div>
            <div class="alert-item-right">
              <span class="badge" :class="a.kmSinceService >= 10000 ? 'badge-danger' : 'badge-warning'">
                {{ a.kmSinceService.toLocaleString() }} km overdue
              </span>
              <div class="alert-odo text-muted text-sm">Current: {{ a.currentOdometer.toLocaleString() }} km</div>
            </div>
          </div>
        </div>
      </div>

      <!-- Active Assignments -->
      <div class="card flex-1">
        <div class="card-header">
          <span class="card-title">🗺 Active Assignments</span>
          <router-link to="/assignments" class="btn btn-ghost btn-sm">View All</router-link>
        </div>
        <div v-if="!assignments.length" class="empty-state">
          <div class="icon">📋</div>
          <p>No active assignments</p>
        </div>
        <div v-else class="assignment-list">
          <div v-for="a in assignments" :key="a.id" class="assign-item">
            <div class="assign-route">{{ a.origin }} → {{ a.destination }}</div>
            <div class="assign-detail">
              <span>👤 {{ a.driverName }}</span>
              <span>🚛 {{ a.vehicleModel }}</span>
            </div>
            <span class="badge" :class="statusBadge(a.status)">{{ a.status }}</span>
          </div>
        </div>
      </div>
    </div>

    <!-- Driver & Vehicle status breakdown -->
    <div class="dash-row">
      <div class="card flex-1">
        <div class="card-header"><span class="card-title">👤 Driver Status</span></div>
        <div class="breakdown-list">
          <div class="breakdown-row" v-for="r in driverBreakdown" :key="r.label">
            <span class="breakdown-label">{{ r.label }}</span>
            <div class="breakdown-bar-wrap">
              <div class="breakdown-bar" :style="{ width: r.pct + '%', background: r.color }"></div>
            </div>
            <span class="breakdown-val">{{ r.val }}</span>
          </div>
        </div>
      </div>
      <div class="card flex-1">
        <div class="card-header"><span class="card-title">🚛 Fleet Status</span></div>
        <div class="breakdown-list">
          <div class="breakdown-row" v-for="r in vehicleBreakdown" :key="r.label">
            <span class="breakdown-label">{{ r.label }}</span>
            <div class="breakdown-bar-wrap">
              <div class="breakdown-bar" :style="{ width: r.pct + '%', background: r.color }"></div>
            </div>
            <span class="breakdown-val">{{ r.val }}</span>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { dashboardApi, assignmentsApi } from '@/services/api'
import { useSignalR } from '@/services/signalr'

const stats       = ref(null)
const alerts      = ref([])
const assignments = ref([])
const loading     = ref(false)
const now         = ref(new Date().toLocaleString())
const { onEvent, offEvent } = useSignalR()

const statCards = computed(() => {
  if (!stats.value) return []
  const s = stats.value
  return [
    { icon: '👤', label: 'Total Drivers',       value: s.totalDrivers,         color: 'blue' },
    { icon: '✅', label: 'Available Drivers',   value: s.availableDrivers,     color: 'green' },
    { icon: '🚛', label: 'Total Vehicles',      value: s.totalVehicles,        color: 'blue' },
    { icon: '🟢', label: 'Active Vehicles',     value: s.activeVehicles,       color: 'green' },
    { icon: '🗺️', label: 'Active Assignments',  value: s.activeAssignments,    color: 'info' },
    { icon: '🔧', label: 'Need Service',        value: s.vehiclesNeedingService, color: s.vehiclesNeedingService > 0 ? 'red' : 'green' },
    { icon: '⏳', label: 'Expired Licenses',    value: s.expiredLicenses,       color: s.expiredLicenses > 0 ? 'red' : 'green' },
    { icon: '⚙️', label: 'In Maintenance',       value: s.vehiclesInMaintenance, color: 'orange' },
  ]
})

const driverBreakdown = computed(() => {
  if (!stats.value) return []
  const s = stats.value
  const total = s.totalDrivers || 1
  return [
    { label: 'Available', val: s.availableDrivers, pct: (s.availableDrivers/total*100), color: 'var(--success)' },
    { label: 'Assigned',  val: s.assignedDrivers,  pct: (s.assignedDrivers/total*100),  color: 'var(--info)' },
    { label: 'Other',     val: total - s.availableDrivers - s.assignedDrivers,
      pct: ((total - s.availableDrivers - s.assignedDrivers)/total*100), color: 'var(--text-muted)' },
  ]
})

const vehicleBreakdown = computed(() => {
  if (!stats.value) return []
  const s = stats.value
  const total = s.totalVehicles || 1
  return [
    { label: 'Active',       val: s.activeVehicles,        pct: (s.activeVehicles/total*100),        color: 'var(--success)' },
    { label: 'Maintenance',  val: s.vehiclesInMaintenance,  pct: (s.vehiclesInMaintenance/total*100),  color: 'var(--warning)' },
    { label: 'Need Service', val: s.vehiclesNeedingService, pct: (s.vehiclesNeedingService/total*100), color: 'var(--danger)' },
  ]
})

function statusBadge(s) {
  return { Active: 'badge-success', Pending: 'badge-warning', Completed: 'badge-muted', Cancelled: 'badge-danger' }[s] || 'badge-muted'
}

async function refresh() {
  loading.value = true
  now.value = new Date().toLocaleString()
  try {
    const [st, al, as_] = await Promise.all([
      dashboardApi.getStats(),
      dashboardApi.getAlerts(),
      assignmentsApi.getAll()
    ])
    stats.value = st.data
    alerts.value = al.data
    assignments.value = as_.data.filter(a => ['Active','Pending'].includes(a.status)).slice(0, 5)
  } finally { loading.value = false }
}

function onUpdate() { refresh() }

onMounted(() => {
  refresh()
  onEvent('AssignmentCreated', onUpdate)
  onEvent('AssignmentUpdated', onUpdate)
  onEvent('VehicleUpdated',    onUpdate)
})
onUnmounted(() => {
  offEvent('AssignmentCreated', onUpdate)
  offEvent('AssignmentUpdated', onUpdate)
  offEvent('VehicleUpdated',    onUpdate)
})
</script>

<style scoped>
.page { padding: 24px; display: flex; flex-direction: column; gap: 20px; }
.page-header { display: flex; align-items: flex-start; justify-content: space-between; }
.page-title { font-family: var(--font-display); font-size: 24px; font-weight: 700; letter-spacing: 2px; }
.page-sub   { color: var(--text-muted); font-size: 12px; margin-top: 2px; }

.stats-grid {
  display: grid; grid-template-columns: repeat(4, 1fr); gap: 14px;
}
@media (max-width: 1000px) { .stats-grid { grid-template-columns: repeat(2, 1fr); } }

.stat-card {
  background: var(--bg-card); border: 1px solid var(--border);
  border-radius: var(--radius-lg); padding: 18px;
  display: flex; flex-direction: column; gap: 4px;
  transition: transform 0.15s;
}
.stat-card:hover { transform: translateY(-2px); }
.stat-card.blue   { border-top: 2px solid var(--info); }
.stat-card.green  { border-top: 2px solid var(--success); }
.stat-card.red    { border-top: 2px solid var(--danger); }
.stat-card.orange { border-top: 2px solid var(--warning); }
.stat-card.info   { border-top: 2px solid var(--accent); }

.stat-icon { font-size: 20px; }
.stat-value { font-family: var(--font-display); font-size: 32px; font-weight: 700; line-height: 1; }
.stat-label { font-size: 11px; text-transform: uppercase; letter-spacing: 0.8px; color: var(--text-muted); }

.dash-row { display: flex; gap: 20px; }
@media (max-width: 768px) { .dash-row { flex-direction: column; } }

.alert-list { display: flex; flex-direction: column; gap: 10px; }
.alert-item {
  display: flex; justify-content: space-between; align-items: center;
  padding: 12px; background: var(--bg-secondary); border-radius: var(--radius);
  border-left: 3px solid var(--warning);
}
.alert-vin { font-family: var(--font-display); font-size: 15px; font-weight: 700; color: var(--warning); }
.alert-model { font-size: 12px; color: var(--text-secondary); }
.alert-item-right { text-align: right; }
.alert-odo { margin-top: 4px; }

.assignment-list { display: flex; flex-direction: column; gap: 10px; }
.assign-item {
  padding: 12px; background: var(--bg-secondary); border-radius: var(--radius);
}
.assign-route { font-weight: 600; font-size: 14px; margin-bottom: 4px; }
.assign-detail { display: flex; gap: 16px; font-size: 12px; color: var(--text-secondary); margin-bottom: 6px; }

.breakdown-list { display: flex; flex-direction: column; gap: 12px; }
.breakdown-row { display: flex; align-items: center; gap: 10px; }
.breakdown-label { min-width: 90px; font-size: 12px; color: var(--text-secondary); }
.breakdown-bar-wrap { flex: 1; height: 6px; background: var(--bg-secondary); border-radius: 3px; overflow: hidden; }
.breakdown-bar { height: 100%; border-radius: 3px; transition: width 0.5s ease; }
.breakdown-val { min-width: 24px; text-align: right; font-family: var(--font-display); font-weight: 700; }
</style>
