import { AlertTriangle, RefreshCw } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";

interface Props {
  title?: string;
  message?: string;
  onRetry?: () => void;
}

export function ErrorState({ title = "Something went wrong", message, onRetry }: Props) {
  return (
    <Card className="border-destructive/40">
      <CardContent className="py-10 text-center space-y-3">
        <AlertTriangle className="h-10 w-10 text-destructive mx-auto" />
        <p className="font-medium">{title}</p>
        {message && <p className="text-sm text-muted-foreground max-w-md mx-auto">{message}</p>}
        {onRetry && (
          <Button variant="outline" size="sm" onClick={onRetry}>
            <RefreshCw className="h-4 w-4 mr-2" /> Try again
          </Button>
        )}
      </CardContent>
    </Card>
  );
}
