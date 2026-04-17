import path from 'path';
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';
// https://vitejs.dev/config/
export default defineConfig({
    plugins: [react()],
    resolve: {
        alias: {
            // Path alias: import '@/components/...' resolves to 'src/components/...'
            '@': path.resolve(__dirname, './src'),
        },
    },
    server: {
        port: 3000,
        // Proxy /api requests to the .NET backend during local development
        proxy: {
            '/api': {
                target: 'http://localhost:5000',
                changeOrigin: true,
            },
        },
    },
});
