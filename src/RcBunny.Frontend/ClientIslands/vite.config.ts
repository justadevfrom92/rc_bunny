import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// Builds the website's React islands into ../wwwroot/islands with a manifest; the Razor layout reads the manifest
// to get the hashed file name (Services/IslandAssets). Hashed files are cached by browsers for a year.
// Dev: `npm run dev` and set Website:IslandsDevServer = http://localhost:5174 to get hot reload inside Razor pages.
export default defineConfig({
  plugins: [react()],
  base: '/islands/',
  server: { port: 5174, cors: true, origin: 'http://localhost:5174' },
  build: {
    outDir: '../wwwroot/islands',
    emptyOutDir: true,
    manifest: true,
    rollupOptions: { input: 'src/main.tsx' },
  },
});
