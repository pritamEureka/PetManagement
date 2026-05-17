import { Link, useLocation } from "react-router-dom";
import { Lock, ArrowLeft } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";

/**
 * Shown by ProtectedRoute when an authenticated user lacks the permission /
 * role for the page they tried to open. Distinguishes "not signed in" (handled
 * by route redirect to /login) from "signed in but forbidden" — the second
 * one should never trigger a logout, just a polite block.
 */
export function UnauthorizedPage() {
  const loc = useLocation();
  return (
    <div className="min-h-screen grid place-items-center p-4">
      <Card className="w-full max-w-md">
        <CardHeader className="text-center space-y-2">
          <div className="mx-auto h-12 w-12 rounded-full bg-muted flex items-center justify-center">
            <Lock className="h-6 w-6 text-muted-foreground" />
          </div>
          <CardTitle>Access denied</CardTitle>
          <p className="text-sm text-muted-foreground">
            You don't have permission to view this page. If you think this is a mistake, contact an administrator.
          </p>
        </CardHeader>
        <CardContent className="space-y-2">
          {(loc.state as { from?: { pathname: string } })?.from?.pathname && (
            <p className="text-xs text-muted-foreground text-center">
              From: <span className="font-mono">{(loc.state as any).from.pathname}</span>
            </p>
          )}
          <Button asChild className="w-full" variant="outline">
            <Link to="/home"><ArrowLeft className="h-4 w-4 mr-1" /> Back to home</Link>
          </Button>
        </CardContent>
      </Card>
    </div>
  );
}
