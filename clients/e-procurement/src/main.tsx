import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { App } from "@/App";
import { loadRuntimeConfig } from "@/env";
import "@/styles/index.css";

await loadRuntimeConfig();

const rootElement = document.getElementById("root");
if (!rootElement) {
  throw new Error("Root element '#root' not found");
}

createRoot(rootElement).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
