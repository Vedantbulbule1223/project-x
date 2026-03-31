import axios from 'axios'

const api = axios.create({ baseURL: '/api' })

// Attach JWT token to every request
api.interceptors.request.use(cfg => {
  const token = localStorage.getItem('token')
  if (token) cfg.headers.Authorization = `Bearer ${token}`
  return cfg
})

// Global 401 → redirect to login
api.interceptors.response.use(
  res => res,
  err => {
    if (err.response?.status === 401) {
      localStorage.removeItem('token')
      window.location.href = '/login'
    }
    return Promise.reject(err)
  }
)

export default api

// ── Auth ─────────────────────────────────────────────────────────────────────
export const authApi = {
  login: (data) => api.post('/auth/login', data),
}

// ── Dashboard ─────────────────────────────────────────────────────────────────
export const dashboardApi = {
  getStats:  () => api.get('/dashboard/stats'),
  getAlerts: () => api.get('/dashboard/alerts'),
}

// ── Drivers ───────────────────────────────────────────────────────────────────
export const driversApi = {
  getAll:   ()       => api.get('/drivers'),
  getById:  (id)     => api.get(`/drivers/${id}`),
  create:   (data)   => api.post('/drivers', data),
  update:   (id, d)  => api.put(`/drivers/${id}`, d),
  delete:   (id)     => api.delete(`/drivers/${id}`),
}

// ── Vehicles ──────────────────────────────────────────────────────────────────
export const vehiclesApi = {
  getAll:          ()        => api.get('/vehicles'),
  getById:         (vin)     => api.get(`/vehicles/${vin}`),
  create:          (data)    => api.post('/vehicles', data),
  updateOdometer:  (vin, km) => api.patch(`/vehicles/${vin}/odometer`, { newOdometer: km }),
  updateStatus:    (vin, s)  => api.patch(`/vehicles/${vin}/status`, { status: s }),
  getLogs:         (vin)     => api.get(`/vehicles/${vin}/logs`),
  logMaintenance:  (data)    => api.post('/vehicles/maintenance', data),
}

// ── Assignments ───────────────────────────────────────────────────────────────
export const assignmentsApi = {
  getAll:        ()        => api.get('/assignments'),
  getById:       (id)      => api.get(`/assignments/${id}`),
  create:        (data)    => api.post('/assignments', data),
  updateStatus:  (id, s)   => api.patch(`/assignments/${id}/status`, { status: s }),
}
