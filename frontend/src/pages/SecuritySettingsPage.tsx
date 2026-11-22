import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ShieldCheck } from "lucide-react";

import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { PasswordInput } from "@/components/ui/password-input";
import { PageHeader } from "@/components/common/PageHeader";
import { FormField } from "@/components/common/a11y";
import { toast } from "@/components/ui/sonner";
import { securityPlusApi, passkeyApi, type RecoveryCodes } from "@/api/securityPlus";
import { Input } from "@/components/ui/input";
import { useAuthStore } from "@/store/authStore";
import { createPasskey, passkeysSupported } from "@/lib/webauthn";
import { useI18n } from "@/lib/i18n";
import { formatDateTime } from "@/lib/format";

export function SecuritySettingsPage() {
  const { t } = useI18n();
  const qc = useQueryClient();

  const sessions = useQuery({ queryKey: ["sec-sessions"], queryFn: securityPlusApi.sessions });
  const refreshToken = useAuthStore((s) => s.refreshToken);
  const currentSession = useQuery({
    queryKey: ["sec-current-session", refreshToken ? "present" : "none", sessions.dataUpdatedAt],
    queryFn: () => securityPlusApi.currentSession(refreshToken ?? ""),
    enabled: !!refreshToken && sessions.isSuccess,
  });
  const recovery = useQuery({ queryKey: ["sec-recovery"], queryFn: securityPlusApi.recoveryStatus });
  const consents = useQuery({ queryKey: ["sec-consents"], queryFn: securityPlusApi.consents });

  const [password, setPassword] = useState("");
  const [stepUpError, setStepUpError] = useState<string | undefined>();
  const [newCodes, setNewCodes] = useState<RecoveryCodes | null>(null);

  const revoke = useMutation({
    mutationFn: securityPlusApi.revoke,
    onSuccess: () => qc.invalidateQueries({ queryKey: ["sec-sessions"] }),
  });
  const revokeAll = useMutation({
    mutationFn: securityPlusApi.revokeAll,
    onSuccess: (r) => {
      toast.success(`${r.revoked}`);
      qc.invalidateQueries({ queryKey: ["sec-sessions"] });
    },
  });
  const trust = useMutation({
    mutationFn: (v: { id: string; trusted: boolean }) => securityPlusApi.setTrusted(v.id, v.trusted),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["sec-sessions"] }),
  });

  const regenerate = useMutation({
    mutationFn: async () => {
      setStepUpError(undefined);
      try {
        await securityPlusApi.stepUp({ password });
      } catch {
        setStepUpError(t("common.error"));
        throw new Error("step-up failed");
      }
      return securityPlusApi.regenerateRecovery();
    },
    onSuccess: (codes) => {
      setNewCodes(codes);
      setPassword("");
      qc.invalidateQueries({ queryKey: ["sec-recovery"] });
    },
  });

  return (
    <div className="mx-auto max-w-3xl space-y-6">
      <PageHeader title={t("security.title")} icon={ShieldCheck} />

      <Card>
        <CardHeader className="flex flex-row items-center justify-between">
          <CardTitle>{t("security.sessions")}</CardTitle>
          <Button variant="destructive" size="sm" onClick={() => revokeAll.mutate()} disabled={revokeAll.isPending}>
            {t("security.logoutEverywhere")}
          </Button>
        </CardHeader>
        <CardContent>
          {sessions.isLoading ? <p>{t("common.loading")}</p> : (
            <ul className="divide-y">
              {(sessions.data ?? []).map((s) => (
                <li key={s.id} className="flex flex-wrap items-center justify-between gap-2 py-3">
                  <div className="text-sm">
                    <p className="font-medium">{s.deviceLabel ?? s.ipAddress ?? "-"}
                      {currentSession.data === s.id ? (
                        <span className="ms-2 rounded bg-primary/10 px-1.5 py-0.5 text-xs font-normal text-primary">{t("security.thisDevice")}</span>
                      ) : null}
                    </p>
                    <p className="text-muted-foreground">
                      {formatDateTime(s.createdAt)}{s.ipAddress ? ` · ${s.ipAddress}` : ""}
                    </p>
                  </div>
                  <div className="flex gap-2">
                    {s.deviceId ? (
                      <label className="flex items-center gap-1 text-sm">
                        <input
                          type="checkbox"
                          checked={s.deviceTrusted}
                          onChange={(e) => trust.mutate({ id: s.deviceId!, trusted: e.target.checked })}
                        />
                        {t("security.trusted")}
                      </label>
                    ) : null}
                    <Button variant="outline" size="sm" onClick={() => revoke.mutate(s.id)}>
                      {t("security.revoke")}
                    </Button>
                  </div>
                </li>
              ))}
            </ul>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>{t("security.recoveryCodes")}</CardTitle></CardHeader>
        <CardContent className="space-y-4">
          {recovery.data ? (
            <p className="text-sm text-muted-foreground">
              {recovery.data.remaining} / {recovery.data.total}
            </p>
          ) : null}
          <FormField id="stepup-password" label={t("security.stepUp")} error={stepUpError}>
            {(p) => (
              <PasswordInput {...p} value={password} autoComplete="current-password"
                onChange={(e) => setPassword(e.target.value)} />
            )}
          </FormField>
          <Button onClick={() => regenerate.mutate()} disabled={!password || regenerate.isPending}>
            {t("security.regenerate")}
          </Button>
          {newCodes ? (
            <ul className="grid grid-cols-2 gap-2 rounded-md border p-3 font-mono text-sm" aria-label={t("security.recoveryCodes")}>
              {newCodes.codes.map((c) => <li key={c}>{c}</li>)}
            </ul>
          ) : null}
        </CardContent>
      </Card>

      <PasskeysCard />

      <Card>
        <CardHeader><CardTitle>{t("security.consentHistory")}</CardTitle></CardHeader>
        <CardContent>
          <ul className="divide-y text-sm">
            {(consents.data ?? []).map((c) => (
              <li key={c.id} className="flex justify-between py-2">
                <span>{c.consentType}{c.version ? ` v${c.version}` : ""} — {c.granted ? "granted" : "withdrawn"}</span>
                <span className="text-muted-foreground">{formatDateTime(c.recordedAt)}</span>
              </li>
            ))}
          </ul>
        </CardContent>
      </Card>
    </div>
  );
}

function PasskeysCard() {
  const qc = useQueryClient();
  const supported = passkeysSupported();
  const list = useQuery({ queryKey: ["sec-passkeys"], queryFn: passkeyApi.list });
  const [label, setLabel] = useState("");
  const [busy, setBusy] = useState(false);

  async function add() {
    setBusy(true);
    try {
      const options = await passkeyApi.registerOptions();
      const cred = await createPasskey(options);
      await passkeyApi.registerVerify(cred, label.trim() || undefined);
      setLabel("");
      toast.success("Passkey added.");
      qc.invalidateQueries({ queryKey: ["sec-passkeys"] });
    } catch (err: any) {
      // User dismissing the browser prompt is not an error worth shouting about.
      if (err?.name !== "NotAllowedError") {
        toast.error(err?.response?.data?.error?.message ?? "Could not add the passkey.");
      }
    } finally { setBusy(false); }
  }

  async function remove(id: string) {
    await passkeyApi.remove(id);
    qc.invalidateQueries({ queryKey: ["sec-passkeys"] });
  }

  return (
    <Card>
      <CardHeader><CardTitle>Passkeys</CardTitle></CardHeader>
      <CardContent className="space-y-4">
        <p className="text-sm text-muted-foreground">
          Sign in with your fingerprint, face or device PIN instead of a password. A passkey counts as strong authentication.
        </p>
        <ul className="divide-y text-sm">
          {(list.data ?? []).map((p) => (
            <li key={p.id} className="flex flex-wrap items-center justify-between gap-2 py-2">
              <div>
                <p className="font-medium">{p.label}</p>
                <p className="text-muted-foreground">
                  Added {formatDateTime(p.createdAt)}{p.lastUsedAt ? ` · last used ${formatDateTime(p.lastUsedAt)}` : ""}
                </p>
              </div>
              <Button variant="outline" size="sm" onClick={() => remove(p.id)}>Remove</Button>
            </li>
          ))}
          {list.data?.length === 0 && <li className="py-2 text-muted-foreground">No passkeys yet.</li>}
        </ul>
        {supported ? (
          <div className="flex flex-wrap items-end gap-2">
            <div className="flex-1 min-w-[10rem]">
              <label htmlFor="passkey-label" className="text-sm font-medium">Name (optional)</label>
              <Input id="passkey-label" placeholder="My laptop" maxLength={100} value={label}
                onChange={(e) => setLabel(e.target.value)} />
            </div>
            <Button onClick={add} disabled={busy}>{busy ? "Waiting..." : "Add a passkey"}</Button>
          </div>
        ) : (
          <p className="text-sm text-muted-foreground">This browser does not support passkeys.</p>
        )}
      </CardContent>
    </Card>
  );
}
