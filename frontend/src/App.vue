<template>
  <div class="app-root">
    <template v-if="auth.isLoggedIn">
      <aside class="sidebar">
        <div class="sidebar-brand">
          <span class="brand-icon">⚡</span>
          <div>
            <div class="brand-name">SWIFTROUTE</div>
            <div class="brand-sub">PULSE COMMAND</div>
          </div>
        </div>

        <nav class="sidebar-nav">
          <router-link to="/"            class="nav-item"><span>📊</span> Dashboard</router-link>
          <router-link to="/assignments" class="nav-item"><span>🗺️</span> Assignments</router-link>
          <router-link to="/drivers"     class="nav-item"><span>👤</span> Drivers</router-link>
          <router-link to="/vehicles"    class="nav-item"><span>🚛</span> Vehicles</router-link>
          <router-link to="/maintenance" class="nav-item"><span>🔧</span> Maintenance</router-link>
        </nav>

        <div class="sidebar-footer">
          <div class="user-info">
            <div class="user-avatar">{{ auth.user?.fullName?.[0] }}</div>
            <div>
              <div class="user-name">{{ auth.user?.fullName }}</div>
              <div class="user-role">{{ auth.user?.role }}</div>
            </div>
          </div>
          <button class="btn btn-ghost btn-sm" @click="auth.logout(); $router.push('/login')">
            Logout
          </button>
        </div>

        <!-- Live status indicator -->
        <div class="live-indicator">
          <span class="live-dot"></span> LIVE
        </div>
      </aside>

      <main class="main-content">
        <!-- Maintenance alert banner -->
        <div v-if="alerts.length" class="alert-strip">
          <div class="alert-strip-inner">
            <span class="alert-strip-icon">⚠️</span>
            <span>{{ alerts.length }} vehicle{{ alerts.length > 1 ? 's' : '' }} require maintenance</span>
            <router-link to="/maintenance" class="alert-strip-link">View →</router-link>
          </div>
        </div>
        <router-view />
      </main>
    </template>

    <router-view v-else />
  </div>
</template>

<script setup>
import { ref, onMounted, onUnmounted } from 'vue'
import { useAuthStore } from '@/stores/auth'
import { dashboardApi } from '@/services/api'
import { useSignalR } from '@/services/signalr'
import { useToast } from 'vue-toastification'

const auth    = useAuthStore()
const toast   = useToast()
const alerts  = ref([])
const { onEvent, offEvent } = useSignalR()

async function fetchAlerts() {
  if (!auth.isLoggedIn) return
  try {
    const res = await dashboardApi.getAlerts()
    alerts.value = res.data
  } catch {}
}

function handleMaintenanceAlert(alert) {
  toast.warning(`🔧 ${alert.vehicleModel} needs service! (${alert.kmSinceService.toLocaleString()} km overdue)`)
  fetchAlerts()
}

onMounted(() => {
  fetchAlerts()
  onEvent('MaintenanceAlert', handleMaintenanceAlert)
  onEvent('VehicleUpdated', fetchAlerts)
})

onUnmounted(() => {
  offEvent('MaintenanceAlert', handleMaintenanceAlert)
  offEvent('VehicleUpdated', fetchAlerts)
})
</script>

<style>
.app-root { display: flex; height: 100vh; overflow: hidden; }

/* ── Sidebar ─────────────────────────────────────────────────────────────── */
.sidebar {
  width: 220px; min-width: 220px;
  background: var(--bg-secondary);
  border-right: 1px solid var(--border);
  display: flex; flex-direction: column;
  padding: 0; position: relative;
}
.sidebar-brand {
  display: flex; align-items: center; gap: 10px;
  padding: 20px 16px; border-bottom: 1px solid var(--border);
}
.brand-icon { font-size: 28px; line-height: 1; }
.brand-name {
  font-family: var(--font-display); font-size: 16px; font-weight: 700;
  letter-spacing: 2px; color: var(--accent);
}
.brand-sub {
  font-family: var(--font-display); font-size: 10px;
  letter-spacing: 2px; color: var(--text-muted);
}

.sidebar-nav {
  flex: 1; display: flex; flex-direction: column;
  padding: 12px 8px; gap: 2px;
}
.nav-item {
  display: flex; align-items: center; gap: 10px;
  padding: 10px 12px; border-radius: var(--radius);
  color: var(--text-secondary); font-size: 13px; font-weight: 500;
  transition: all 0.15s; text-decoration: none;
}
.nav-item:hover { background: var(--bg-hover); color: var(--text-primary); }
.nav-item.router-link-active {
  background: var(--accent-glow); color: var(--accent);
  border-left: 2px solid var(--accent); padding-left: 10px;
}

.sidebar-footer {
  padding: 12px; border-top: 1px solid var(--border);
  display: flex; flex-direction: column; gap: 10px;
}
.user-info { display: flex; align-items: center; gap: 10px; }
.user-avatar {
  width: 32px; height: 32px; border-radius: 50%;
  background: var(--accent-glow); border: 1px solid var(--accent);
  display: flex; align-items: center; justify-content: center;
  font-family: var(--font-display); font-weight: 700;
  color: var(--accent); font-size: 14px;
}
.user-name { font-size: 13px; font-weight: 500; }
.user-role {
  font-size: 10px; text-transform: uppercase;
  letter-spacing: 0.8px; color: var(--text-muted);
}

.live-indicator {
  position: absolute; top: 12px; right: 12px;
  font-family: var(--font-display); font-size: 10px;
  letter-spacing: 1px; color: var(--success);
  display: flex; align-items: center; gap: 4px;
}
.live-dot {
  width: 6px; height: 6px; border-radius: 50%;
  background: var(--success);
  animation: pulse 1.5s ease-in-out infinite;
}
@keyframes pulse {
  0%, 100% { opacity: 1; transform: scale(1); }
  50%       { opacity: 0.4; transform: scale(0.8); }
}

/* ── Main ────────────────────────────────────────────────────────────────── */
.main-content {
  flex: 1; overflow-y: auto;
  display: flex; flex-direction: column;
}

/* ── Alert Strip ─────────────────────────────────────────────────────────── */
.alert-strip {
  background: var(--warning-bg); border-bottom: 1px solid var(--warning);
  padding: 8px 24px;
}
.alert-strip-inner {
  display: flex; align-items: center; gap: 10px; font-size: 13px; color: var(--warning);
}
.alert-strip-link {
  margin-left: auto; color: var(--warning); font-weight: 600;
  text-decoration: underline;
}
</style>
