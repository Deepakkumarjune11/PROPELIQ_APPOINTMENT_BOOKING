import http from 'http';
import https from 'https';
import path from 'path';
import { defineConfig, loadEnv } from "vite";
import react from "@vitejs/plugin-react";

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  // Load all env vars (including VITE_* ones) from .env / .env.local / .env.[mode].local
  // Use '' prefix to read non-VITE_ vars too (needed for proxy target).
  const env = loadEnv(mode, process.cwd(), '');

  // VITE_BACKEND_URL controls which host:port the dev-server proxy forwards to.
  // Docker / dotnet run (http):  http://127.0.0.1:8080  (default)
  // IIS Express HTTP:            http://localhost:60856
  // IIS Express HTTPS:           https://localhost:44397
  const backendTarget = env.VITE_BACKEND_URL || 'http://127.0.0.1:8080';
  const isHttps = backendTarget.startsWith('https://');

  const proxyEntry = {
    target: backendTarget,
    changeOrigin: true,
    // secure: false allows self-signed certs (IIS Express dev cert, Docker internal TLS)
    secure: false,
    // Use the correct agent for the protocol — Node throws if you send an https: target
    // through http.Agent ("Protocol 'https:' not supported. Expected 'http:'")
    // Force IPv4 on both to prevent ECONNREFUSED when localhost resolves to ::1 on Windows.
    agent: isHttps
      ? new https.Agent({ family: 4, rejectUnauthorized: false })
      : new http.Agent({ family: 4 }),
  };

  return {
  plugins: [react()],
  resolve: {
    // Mirror tsconfig.json paths — Rollup must know about @/ or it cannot bundle
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  server: {
    host: 'localhost',
    port: 3000,
    proxy: {
      // REST API
      '/api': proxyEntry,
      // SignalR WebSocket hub (US_017)
      '/hubs': { ...proxyEntry, ws: true },
    },
  },
  build: {
    outDir: "dist",
    sourcemap: false,
    minify: "esbuild",
    // MUI's full bundle is ~518 kB minified — suppress the size warning for vendor chunks.
    // Further reduction requires per-component tree-shaking (scope of a dedicated perf task).
    chunkSizeWarningLimit: 600,
    rollupOptions: {
      output: {
        manualChunks: {
          "react-vendor": ["react", "react-dom", "react-router-dom"],
          "query-vendor": ["@tanstack/react-query"],
          "mui-vendor": ["@mui/material", "@mui/icons-material", "@emotion/react", "@emotion/styled"],
          "chart-vendor": ["recharts"],
        },
      },
    },
  },
  };
});

 