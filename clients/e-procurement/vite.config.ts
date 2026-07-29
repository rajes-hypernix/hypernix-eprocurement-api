import { defineConfig, loadEnv } from "vite";
import react from "@vitejs/plugin-react";
import path from "node:path";

export default defineConfig(({ mode, command }) => {
  const env = loadEnv(mode, process.cwd(), "");
  // Prefer HTTPS. HTTP :5030 issues a 307 → :7030 that strips Authorization on redirect
  // (browser + many proxies), which surfaces as 401 "No Authorization header".
  const apiBase = env.VITE_API_BASE_URL ?? "https://localhost:7030";
  // Relative base so IIS/virtual-directory deploys resolve ./assets/* next to index.html
  // instead of site-root /assets/* (common cause of 404 after manual publish).
  const base = env.VITE_BASE_PATH?.trim() || (command === "build" ? "./" : "/");

  return {
    base,
    plugins: [react()],
    resolve: {
      alias: {
        "@": path.resolve(__dirname, "./src"),
      },
    },
    server: {
      port: 5175,
      strictPort: true,
      proxy: {
        "/api": { target: apiBase, changeOrigin: true, secure: false },
        "/health": { target: apiBase, changeOrigin: true, secure: false },
        "/openapi": { target: apiBase, changeOrigin: true, secure: false },
        "/scalar": { target: apiBase, changeOrigin: true, secure: false },
      },
    },
  };
});
