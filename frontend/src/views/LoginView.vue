<template>
  <div class="login-page">
    <div class="login-card">
      <div class="login-brand">
        <span class="login-icon">⚡</span>
        <div class="login-title">SWIFTROUTE</div>
        <div class="login-subtitle">PULSE COMMAND CENTER</div>
      </div>

      <form @submit.prevent="doLogin" class="login-form">
        <div class="form-group">
          <label class="form-label">Username</label>
          <input v-model="form.username" class="form-control" type="text"
            placeholder="Enter username" required autocomplete="username" />
        </div>
        <div class="form-group">
          <label class="form-label">Password</label>
          <input v-model="form.password" class="form-control" type="password"
            placeholder="••••••••" required autocomplete="current-password" />
        </div>

        <div v-if="error" class="error-msg">{{ error }}</div>

        <button type="submit" class="btn btn-primary w-full" :disabled="loading">
          {{ loading ? 'Authenticating...' : 'LOGIN' }}
        </button>
      </form>

      <div class="login-hint">
        <div class="hint-title">Test Accounts</div>
        <div v-for="acc in accounts" :key="acc.user" class="hint-row"
          @click="form.username = acc.user; form.password = acc.pass">
          <span class="hint-badge">{{ acc.role }}</span>
          <span>{{ acc.user }} / {{ acc.pass }}</span>
        </div>
      </div>
    </div>

    <div class="login-bg">
      <div class="bg-grid"></div>
    </div>
  </div>
</template>

<script setup>
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const auth   = useAuthStore()
const router = useRouter()
const form   = ref({ username: '', password: '' })
const loading = ref(false)
const error   = ref('')

const accounts = [
  { role: 'Admin',      user: 'admin',       pass: 'Admin@123' },
  { role: 'Dispatcher', user: 'dispatcher1', pass: 'Dispatch@123' },
  { role: 'Manager',    user: 'manager1',    pass: 'Manager@123' },
  { role: 'Technician', user: 'tech1',       pass: 'Tech@123' },
]

async function doLogin() {
  error.value = ''
  loading.value = true
  try {
    await auth.login(form.value.username, form.value.password)
    router.push('/')
  } catch (e) {
    error.value = e.response?.data?.message || 'Invalid credentials'
  } finally {
    loading.value = false
  }
}
</script>

<style scoped>
.login-page {
  min-height: 100vh; display: flex; align-items: center; justify-content: center;
  position: relative; overflow: hidden;
}
.login-bg {
  position: absolute; inset: 0; z-index: 0;
  background: radial-gradient(ellipse at 30% 50%, rgba(0,212,255,0.05) 0%, transparent 60%),
              radial-gradient(ellipse at 70% 20%, rgba(68,138,255,0.05) 0%, transparent 50%);
}
.bg-grid {
  width: 100%; height: 100%;
  background-image: linear-gradient(var(--border) 1px, transparent 1px),
                    linear-gradient(90deg, var(--border) 1px, transparent 1px);
  background-size: 40px 40px; opacity: 0.3;
}
.login-card {
  position: relative; z-index: 1;
  width: 100%; max-width: 400px;
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: var(--radius-lg);
  padding: 40px 32px;
  box-shadow: var(--shadow), var(--shadow-accent);
}
.login-brand { text-align: center; margin-bottom: 32px; }
.login-icon { font-size: 40px; display: block; margin-bottom: 8px; }
.login-title {
  font-family: var(--font-display); font-size: 28px; font-weight: 700;
  letter-spacing: 4px; color: var(--accent);
}
.login-subtitle {
  font-family: var(--font-display); font-size: 12px;
  letter-spacing: 3px; color: var(--text-muted); margin-top: 2px;
}
.login-form { display: flex; flex-direction: column; gap: 16px; }
.error-msg {
  background: var(--danger-bg); border: 1px solid var(--danger);
  color: var(--danger); border-radius: var(--radius); padding: 8px 12px;
  font-size: 13px;
}
.login-hint {
  margin-top: 24px; padding-top: 20px;
  border-top: 1px solid var(--border);
}
.hint-title {
  font-size: 10px; text-transform: uppercase; letter-spacing: 1px;
  color: var(--text-muted); margin-bottom: 8px;
}
.hint-row {
  display: flex; align-items: center; gap: 10px;
  padding: 6px 8px; border-radius: var(--radius);
  font-size: 12px; color: var(--text-secondary); cursor: pointer;
  transition: background 0.15s;
}
.hint-row:hover { background: var(--bg-hover); }
.hint-badge {
  font-size: 10px; font-weight: 600; letter-spacing: 0.5px;
  background: var(--info-bg); color: var(--info);
  padding: 2px 7px; border-radius: 10px;
}
</style>
