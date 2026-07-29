import { copyFileSync, existsSync } from "node:fs";
import { resolve } from "node:path";

const mode = (process.argv[2] ?? "").trim().toLowerCase();
if (mode !== "dev" && mode !== "prod") {
  console.error("Usage: node scripts/apply-config.mjs <dev|prod>");
  process.exit(1);
}

const src = resolve(process.cwd(), "public", `config.${mode}.json`);
const dest = resolve(process.cwd(), "dist", "config.json");

if (!existsSync(src)) {
  console.error(`Missing config file: ${src}`);
  process.exit(1);
}
if (!existsSync(resolve(process.cwd(), "dist"))) {
  console.error("dist/ not found — run vite build first");
  process.exit(1);
}

copyFileSync(src, dest);
console.log(`Wrote dist/config.json from public/config.${mode}.json`);
