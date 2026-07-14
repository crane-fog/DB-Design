import './assets/main.css'

import RegisterPage from './pages/RegisterPage.vue'
import { createApp } from 'vue'
import { pinia } from './stores/pinia'

createApp(RegisterPage).use(pinia).mount('#register-app')
