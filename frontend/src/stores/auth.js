import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { authApi } from '@/services/api'
import { useSignalR } from '@/services/signalr'

export const useAuthStore = defineStore('auth', () => {
  const token    = ref(localStorage.getItem('token') || null)
  const user     = ref(JSON.parse(localStorage.getItem('user') || 'null'))
  const { connect, disconnect } = useSignalR()

  const isLoggedIn = computed(() => !!token.value)
  const role       = computed(() => user.value?.role || '')
  const canDispatch = computed(() => ['Admin','Manager','Dispatcher'].includes(role.value))
  const canManage   = computed(() => ['Admin','Manager'].includes(role.value))
  const isTech      = computed(() => ['Admin','Manager','Technician'].includes(role.value))

  async function login(username, password) {
    const res = await authApi.login({ username, password })
    token.value = res.data.token
    user.value  = { id: res.data.userId, fullName: res.data.fullName, role: res.data.role }
    localStorage.setItem('token', token.value)
    localStorage.setItem('user', JSON.stringify(user.value))
    connect(token.value)
  }

  function logout() {
    disconnect()
    token.value = null
    user.value  = null
    localStorage.removeItem('token')
    localStorage.removeItem('user')
  }

  // Re-connect SignalR if already logged in on page load
  if (token.value) connect(token.value)

  return { token, user, isLoggedIn, role, canDispatch, canManage, isTech, login, logout }
})
