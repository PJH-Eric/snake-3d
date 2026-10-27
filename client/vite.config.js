import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { fileURLToPath } from 'node:url';

const shared = fileURLToPath(new URL('../shared', import.meta.url));

/* GAME_SERVER_URL（GitHub Actions repository variable）在建置時注入，是前端唯一的連線設定。 */
export default defineConfig({
  base: './',
  plugins: [react()],
  envPrefix: ['VITE_', 'GAME_SERVER_'],
  resolve: { alias: { '@shared': shared } },
  server: { port: 5173, fs: { allow: ['..'] } },
  build: { target: 'es2020', chunkSizeWarningLimit: 900 },
  test: { environment: 'jsdom', setupFiles: ['./tests/setup.js'], include: ['tests/**/*.test.js'], globals: false }
});
