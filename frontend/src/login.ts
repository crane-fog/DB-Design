import './assets/main.css'

import LoginPage from './pages/LoginPage.vue'
import { createApp } from 'vue'
import { pinia } from './stores/pinia'

createApp(LoginPage).use(pinia).mount('#login-app')
