import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// `npm run dev` serves the admin on :5173 and sends /api to the backend on :5200.
// `npm run build` puts the finished files in ../wwwroot, which the backend serves.
export default defineConfig({
  plugins: [react()],
  server: { port: 5173, proxy: { '/api': 'http://localhost:5200' } },
  build: { outDir: '../wwwroot', emptyOutDir: true },
});
