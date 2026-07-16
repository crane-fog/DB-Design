import './assets/main.css'
import 'element-plus/dist/index.css'

import App from './App.vue'
import ElementPlus from 'element-plus'
import { createApp } from 'vue'
import { pinia } from './stores/pinia'
import { router } from './router'

createApp(App).use(pinia).use(router).use(ElementPlus).mount('#app')
