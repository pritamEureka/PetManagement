import { Toaster as SonnerToaster, toast } from "sonner";
import { useTheme } from "@/components/theme/ThemeProvider";

export function Toaster() {
  const { resolved } = useTheme();
  return (
    <SonnerToaster
      theme={resolved}
      position="top-right"
      richColors
      closeButton
      toastOptions={{ classNames: { toast: "rounded-md border" } }}
    />
  );
}

export { toast };
