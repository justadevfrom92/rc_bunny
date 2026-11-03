import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// Builds every island into one file: ../wwwroot/js/islands.js. Pages that use an island add a <script> tag for it.
export default defineConfig({
  plugins: [react()],
  build: {
    outDir: '../wwwroot/js',
    emptyOutDir: true,
    rollupOptions: {
      input: 'src/islands.tsx',
      output: { entryFileNames: 'islands.js' },
    },
  },
});
