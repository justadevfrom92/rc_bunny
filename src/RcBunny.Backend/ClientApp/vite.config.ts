import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// Dev: `npm run dev` serves the client on :5173 and forwards /api to the ASP.NET backend on :5200.
// Build: output goes to ../wwwroot, which the backend serves (hashed files under /assets are cached for a year).
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: { '/api': { target: 'http://localhost:5200', changeOrigin: false } },
  },
  build: {
    outDir: '../wwwroot',
    emptyOutDir: true,
    rollupOptions: {
      output: {
        // Libraries change less often than our code: a separate chunk stays cached across deploys.
        manualChunks: (id) => (id.includes('node_modules') ? 'vendor' : undefined),
      },
    },
  },
});
