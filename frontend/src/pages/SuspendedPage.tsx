import { Link } from "react-router-dom";
import { useEffect, useState } from "react";
import { ShieldAlert } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { useAuthStore } from "@/store/authStore";
import { useI18n } from "@/lib/i18n";
import { formatDateTime } from "@/lib/format";
import { securityApi, type UserSuspension } from "@/api/security";

/**
 * Landing page for accounts the SuspensionGuardMiddleware blocked. Shows the
 * reason, when it expires (if timed), and a "sign out" escape hatch — those
 * three things are exactly what a suspended user needs.
 */
export function SuspendedPage() {
  const { t } = useI18n();
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
        <CardHeader className="text-center space-y-2" role="alert">
          <div className="mx-auto h-12 w-12 rounded-full bg-destructive/10 flex items-center justify-center">
            <ShieldAlert aria-hidden="true" className="h-6 w-6 text-destructive" />
          </div>
          <CardTitle>{t("suspended.title")}</CardTitle>
          <p className="text-sm text-muted-foreground">
            {suspension?.isBan
              ? t("suspended.banned")
              : t("suspended.temporary")}
          </p>
        </CardHeader>
        <CardContent className="space-y-3 text-sm">
          {suspension?.reason && (
            <div>
              <p className="text-muted-foreground">{t("suspended.reason")}</p>
              <p className="font-medium">{suspension.reason}</p>
            </div>
          )}
          {suspension?.details && (
            <div>
              <p className="text-muted-foreground">{t("suspended.details")}</p>
              <p>{suspension.details}</p>
            </div>
          )}
          {suspension?.expiresAt && (
            <div>
              <p className="text-muted-foreground">{t("suspended.expires")}</p>
              <p>{formatDateTime(suspension.expiresAt)}</p>
            </div>
          )}
          {message && !suspension && (
            <p className="text-muted-foreground">{message}</p>
          )}

          <div className="pt-3 flex flex-col gap-2">
            <Button asChild variant="outline"><Link to="/support">{t("suspended.contactSupport")}</Link></Button>
            <Button variant="ghost" onClick={() => logout()}>{t("suspended.signOut")}</Button>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
