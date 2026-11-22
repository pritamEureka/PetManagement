import React from "react";
import ReactDOM from "react-dom/client";
import "./index.css";
import App from "./App";
import { registerPwa } from "@/lib/pwa";
import { ThemeProvider } from "@/components/theme/ThemeProvider";
import { Toaster } from "@/components/ui/sonner";
import { LiveRegionProvider } from "@/components/common/a11y";
import { TooltipProvider } from "@/components/ui/tooltip";

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <ThemeProvider>
      <TooltipProvider delayDuration={200}>
        <LiveRegionProvider><App /></LiveRegionProvider>
        <Toaster />
      </TooltipProvider>
    </ThemeProvider>
  </React.StrictMode>
);

registerPwa();
