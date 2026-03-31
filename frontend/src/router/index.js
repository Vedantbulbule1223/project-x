import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const routes = [
  { path: '/login',       name: 'Login',       component: () => import('@/views/LoginView.vue'),       meta: { public: true } },
  { path: '/',            name: 'Dashboard',   component: () => import('@/views/DashboardView.vue') },
  { path: '/drivers',     name: 'Drivers',     component: () => import('@/views/DriversView.vue') },
  { path: '/vehicles',    name: 'Vehicles',    component: () => import('@/views/VehiclesView.vue') },
  { path: '/assignments', name: 'Assignments', component: () => import('@/views/AssignmentsView.vue') },
  { path: '/maintenance', name: 'Maintenance', component: () => import('@/views/MaintenanceView.vue') },
  { path: '/:pathMatch(.*)*', redirect: '/' }
]

const router = createRouter({
  history: createWebHistory(),
  routes
})

router.beforeEach((to) => {
  const auth = useAuthStore()
  if (!to.meta.public && !auth.isLoggedIn) return '/login'
  if (to.path === '/login' && auth.isLoggedIn) return '/'
})

export default router
