import { AlertTriangle, RefreshCw } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { useI18n } from "@/lib/i18n";

interface Props {
  title?: string;
  message?: string;
  onRetry?: () => void;
}

export function ErrorState({ title, message, onRetry }: Props) {
  const { t } = useI18n();
  return (
    <Card className="border-destructive/40" role="alert">
      <CardContent className="py-10 text-center space-y-3">
        <AlertTriangle aria-hidden="true" className="h-10 w-10 text-destructive mx-auto" />
        <p className="font-medium">{title ?? t("state.errorTitle")}</p>
        {message && <p className="text-sm text-muted-foreground max-w-md mx-auto">{message}</p>}
        {onRetry && (
          <Button variant="outline" size="sm" onClick={onRetry}>
            <RefreshCw aria-hidden="true" className="h-4 w-4 me-2" /> {t("common.tryAgain")}
          </Button>
        )}
      </CardContent>
    </Card>
  );
}
