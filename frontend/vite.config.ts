/// <reference types="vitest/config" />
import { defineConfig, loadEnv, type Connect, type Plugin } from 'vite';
import react from '@vitejs/plugin-react';
import { resolve } from 'node:path';
import { pageFor } from './src/shared/routes.ts';

// Same origin by default: an unset VITE_API_BASE_URL means "the API serves this site" (see .env.example).
process.env.VITE_API_BASE_URL ??= '';

function pageRoutes(): Plugin {
  const rewrite: Connect.NextHandleFunction = (req, _res, next) => {
    const page = req.url ? pageFor(req.url) : null;
    if (page) req.url = page + (req.url!.includes('?') ? req.url!.slice(req.url!.indexOf('?')) : '');
    next();
  };
  return {
    name: 'qoot-page-routes',
    configureServer: (server) => { server.middlewares.use(rewrite); },
    configurePreviewServer: (server) => { server.middlewares.use(rewrite); },
  };
}

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '');
  const api = env.QOOT_DEV_API ?? 'http://localhost:5080';
  const proxy = Object.fromEntries(['/api', '/media', '/sitemap.xml', '/robots.txt'].map((p) => [p, { target: api, changeOrigin: false }]));
  return {
    plugins: [react(), pageRoutes()],
    build: {
      outDir: env.QOOT_OUT_DIR ?? 'dist',
      emptyOutDir: true,
      assetsDir: 'static',
      sourcemap: false,
      rollupOptions: {
        input: {
          index: resolve(import.meta.dirname, 'index.html'),
          news: resolve(import.meta.dirname, 'news.html'),
          admin: resolve(import.meta.dirname, 'admin/index.html'),
        },
      },
    },
    server: { port: 5173, proxy },
    preview: { port: 4173, proxy },
    test: {
      environment: 'jsdom',
      include: ['src/**/*.test.{ts,tsx}'],
      restoreMocks: true,
    },
  };
});
