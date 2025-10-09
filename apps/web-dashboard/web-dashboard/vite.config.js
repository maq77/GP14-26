import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
  root: "web-dashboard",                 // <- inner folder that has index.html + src/
  plugins: [react()],
  build: { outDir: "../dist", emptyOutDir: true },  // emit dist next to package.json
  server: { port: 5173 }
});
