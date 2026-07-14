import './assets/main.css'

import App from './App.vue'
import { createApp } from 'vue'
import { pinia } from './stores/pinia'
import { router } from './router'

createApp(App).use(pinia).use(router).mount('#app')
