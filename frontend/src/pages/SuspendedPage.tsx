import { Link } from "react-router-dom";
import { useEffect, useState } from "react";
import { ShieldAlert } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { useAuthStore } from "@/store/authStore";
import { securityApi, type UserSuspension } from "@/api/security";

/**
 * Landing page for accounts the SuspensionGuardMiddleware blocked. Shows the
 * reason, when it expires (if timed), and a "sign out" escape hatch — those
 * three things are exactly what a suspended user needs.
 */
export function SuspendedPage() {
  const message = useAuthStore((s) => s.suspensionMessage);
  const logout = useAuthStore((s) => s.logout);
  const [suspension, setSuspension] = useState<UserSuspension | null>(null);

  useEffect(() => {
    // /security/me is whitelisted by the middleware so we can still read our own state.
    securityApi.me()
      .then((me) => setSuspension(me.activeSuspension))
      .catch(() => { /* token may be gone — leave nulls */ });
  }, []);

  return (
    <div className="min-h-screen grid place-items-center p-4 bg-gradient-to-b from-destructive/5 to-background">
      <Card className="w-full max-w-md">
        <CardHeader className="text-center space-y-2">
          <div className="mx-auto h-12 w-12 rounded-full bg-destructive/10 flex items-center justify-center">
            <ShieldAlert className="h-6 w-6 text-destructive" />
          </div>
          <CardTitle>Account on hold</CardTitle>
          <p className="text-sm text-muted-foreground">
            {suspension?.isBan
              ? "Your account has been permanently banned."
              : "Your account has been temporarily suspended."}
          </p>
        </CardHeader>
        <CardContent className="space-y-3 text-sm">
          {suspension?.reason && (
            <div>
              <p className="text-muted-foreground">Reason</p>
              <p className="font-medium">{suspension.reason}</p>
            </div>
          )}
          {suspension?.details && (
            <div>
              <p className="text-muted-foreground">Details</p>
              <p>{suspension.details}</p>
            </div>
          )}
          {suspension?.expiresAt && (
            <div>
              <p className="text-muted-foreground">Expires</p>
              <p>{new Date(suspension.expiresAt).toLocaleString()}</p>
            </div>
          )}
          {message && !suspension && (
            <p className="text-muted-foreground">{message}</p>
          )}

          <div className="pt-3 flex flex-col gap-2">
            <Button asChild variant="outline"><Link to="/support">Contact support</Link></Button>
            <Button variant="ghost" onClick={() => logout()}>Sign out</Button>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
