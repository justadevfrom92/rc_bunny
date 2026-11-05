import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// The admin's pages are under /admin, like ebunny's (/admin, /admin/corral, /admin/corral/customers/1…).
// `npm run dev` serves it on :5173/admin and sends /api to the backend on :5200.
// `npm run build` puts the finished files in ../wwwroot/admin, which the backend serves.
export default defineConfig({
  plugins: [react()],
  base: '/admin/',
  server: { port: 5173, proxy: { '/api': 'http://localhost:5200' } },
  build: { outDir: '../wwwroot/admin', emptyOutDir: true },
});
