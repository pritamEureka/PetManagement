import { Component, type ErrorInfo, type ReactNode } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import { AlertTriangle, RotateCcw, ArrowLeft } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";

interface Props {
  children: ReactNode;
  // Bumping this key (e.g. with location.pathname) clears the error state on navigation,
  // so visiting another page doesn't stay stuck on the previous page's error.
  resetKey?: string;
}
interface State { error: Error | null }

class ErrorBoundaryInner extends Component<Props, State> {
  state: State = { error: null };

  static getDerivedStateFromError(error: Error): State { return { error }; }

  componentDidCatch(error: Error, info: ErrorInfo) {
    if (import.meta.env.DEV) {
      console.error("[ErrorBoundary]", error, info.componentStack);
    }
    // TODO: ship `error` + `info.componentStack` to a server-side error reporter
    // (Sentry, Datadog RUM, etc.) in production instead of writing to the
    // browser console where end users can read it.
  }

  componentDidUpdate(prev: Props) {
    if (prev.resetKey !== this.props.resetKey && this.state.error) {
      this.setState({ error: null });
    }
  }

  render() {
    if (!this.state.error) return this.props.children;
    return <ErrorFallback error={this.state.error} onReset={() => this.setState({ error: null })} />;
  }
}

function ErrorFallback({ error, onReset }: { error: Error; onReset: () => void }) {
  const nav = useNavigate();
  return (
    <Card className="max-w-2xl mx-auto mt-12">
      <CardContent className="pt-6 space-y-4">
        <div className="flex items-center gap-2 text-destructive">
          <AlertTriangle className="h-5 w-5" />
          <h2 className="text-lg font-semibold">Something went wrong on this page</h2>
        </div>
        <p className="text-sm text-muted-foreground">
          The page failed to render. Other pages still work — use the sidebar or go back.
        </p>
        {import.meta.env.DEV && (
          <pre className="text-xs bg-muted p-3 rounded-md overflow-auto max-h-64 whitespace-pre-wrap">
            {error.message}
            {error.stack ? `\n\n${error.stack}` : ""}
          </pre>
        )}
        <div className="flex gap-2">
          <Button variant="outline" size="sm" onClick={() => nav(-1)}>
            <ArrowLeft className="h-4 w-4 mr-1" /> Go back
          </Button>
          <Button size="sm" onClick={onReset}>
            <RotateCcw className="h-4 w-4 mr-1" /> Try again
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}

export function ErrorBoundary({ children }: { children: ReactNode }) {
  const location = useLocation();
  return <ErrorBoundaryInner resetKey={location.pathname}>{children}</ErrorBoundaryInner>;
}
