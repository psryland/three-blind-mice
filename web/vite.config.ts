import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import basicSSL from '@vitejs/plugin-basic-ssl'

export default defineConfig({
	plugins: [react(), basicSSL()],
	server: {
		port: 53100,
		https: true,
	},
})
