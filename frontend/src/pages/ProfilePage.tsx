import { useEffect, useRef, useState } from "react";
import axios from "axios";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Camera, Loader2, User as UserIcon, KeyRound, Save } from "lucide-react";

import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { PasswordInput } from "@/components/ui/password-input";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Skeleton } from "@/components/ui/skeleton";
import { Separator } from "@/components/ui/separator";
import { PageHeader } from "@/components/common/PageHeader";
import { toast } from "@/components/ui/sonner";

import { api } from "@/api/client";
import { usersApi, type MyProfile } from "@/api/users";
import { useAuthStore } from "@/store/authStore";
import { useI18n } from "@/lib/i18n";
import { FormField, useAnnounce } from "@/components/common/a11y";
import { useDraft } from "@/hooks/useDraft";
import { confirmUpload } from "@/lib/uploadConfirm";
import { DraftBanner } from "@/components/common/DraftBanner";

const AVATAR_MAX_BYTES = 5 * 1024 * 1024;
const AVATAR_MIME = ["image/jpeg", "image/png", "image/webp", "image/gif"];

type FieldErrors = Record<string, string>;

function extractFieldErrors(err: unknown): FieldErrors {
  const data = (err as { response?: { data?: { error?: { details?: unknown; errors?: unknown; message?: string } } } })
    ?.response?.data?.error;
  const out: FieldErrors = {};
  // The API returns either { details: { field: [msgs] } } or { errors: { field: [msgs] } }.
  const bag = (data?.details ?? data?.errors) as Record<string, string[] | string> | undefined;
  if (bag && typeof bag === "object") {
    for (const [k, v] of Object.entries(bag)) {
      out[k] = Array.isArray(v) ? v.join(" ") : String(v);
    }
  }
  return out;
}

function topLevelMessage(err: unknown): string | null {
  const msg = (err as { response?: { data?: { error?: { message?: string } } } })?.response?.data?.error?.message;
  return typeof msg === "string" ? msg : null;
}

export function ProfilePage() {
  const { t } = useI18n();
  const announce = useAnnounce();
  const qc = useQueryClient();
  const patchUser = useAuthStore((s) => s.patchUser);
  const cachedUser = useAuthStore((s) => s.user);

  const { data: profile, isLoading } = useQuery({
    queryKey: ["users", "me"],
    queryFn: usersApi.me,
  });

  // Local copies of the editable fields. Re-seed whenever the loaded profile
  // changes so refetches after a save show the freshly-persisted values.
  const [displayName, setDisplayName] = useState("");
  const [email, setEmail] = useState("");
  const [phoneNumber, setPhoneNumber] = useState("");
  const [bio, setBio] = useState("");
  const [location, setLocation] = useState("");
  const [profileErrors, setProfileErrors] = useState<FieldErrors>({});

  useEffect(() => {
    if (!profile) return;
    setDisplayName(profile.displayName);
    setEmail(profile.email);
    setPhoneNumber(profile.phoneNumber ?? "");
    setBio(profile.bio ?? "");
    setLocation(profile.location ?? "");
  }, [profile]);

  // Autosave unsaved edits (only when they differ from the saved profile; never touches passwords).
  const profileForm = { displayName, email, phoneNumber, bio, location };
  const profileDirty = !!profile && (
    displayName !== profile.displayName || email !== profile.email ||
    phoneNumber !== (profile.phoneNumber ?? "") || bio !== (profile.bio ?? "") || location !== (profile.location ?? "")
  );
  const draft = useDraft("profile-edit", profileForm, { enabled: profileDirty });

  const saveProfile = useMutation({
    mutationFn: usersApi.updateMe,
    onSuccess: (updated: MyProfile) => {
      draft.clear();
      qc.setQueryData(["users", "me"], updated);
      patchUser({
        displayName: updated.displayName,
        email: updated.email,
        avatarUrl: updated.avatarUrl ?? null,
      });
      setProfileErrors({});
      toast.success(t("profile.updated"));
      announce(t("profile.updated"));
    },
    onError: (err) => {
      const fieldErrs = extractFieldErrors(err);
      setProfileErrors(fieldErrs);
      const top = topLevelMessage(err);
      toast.error(top ?? t("profile.saveError"));
    },
  });

  function onSaveProfile(e: React.FormEvent) {
    e.preventDefault();
    // Only send fields that actually changed to keep PUT payloads minimal and
    // so a no-op email submit doesn't trigger an EmailConfirmed reset.
    const body: Parameters<typeof usersApi.updateMe>[0] = {};
    if (!profile) return;
    if (displayName.trim() !== profile.displayName) body.displayName = displayName;
    if (email.trim().toLowerCase() !== profile.email.toLowerCase()) body.email = email;
    if ((phoneNumber || null) !== (profile.phoneNumber ?? null)) body.phoneNumber = phoneNumber;
    if ((bio || null) !== (profile.bio ?? null)) body.bio = bio;
    if ((location || null) !== (profile.location ?? null)) body.location = location;
    if (Object.keys(body).length === 0) {
      toast.info(t("profile.noChanges"));
      return;
    }
    saveProfile.mutate(body);
  }

  // Password change
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [pwErrors, setPwErrors] = useState<FieldErrors>({});

  const changePassword = useMutation({
    mutationFn: ({ cur, next }: { cur: string; next: string }) =>
      usersApi.changePassword(cur, next),
    onSuccess: () => {
      setCurrentPassword("");
      setNewPassword("");
      setConfirmPassword("");
      setPwErrors({});
      toast.success(t("profile.passwordChanged"));
      announce(t("profile.passwordChanged"));
    },
    onError: (err) => {
      const fieldErrs = extractFieldErrors(err);
      setPwErrors(fieldErrs);
      const top = topLevelMessage(err);
      toast.error(top ?? t("profile.passwordError"));
    },
  });

  function onChangePassword(e: React.FormEvent) {
    e.preventDefault();
    const errs: FieldErrors = {};
    if (!currentPassword) errs.currentPassword = t("profile.err.currentRequired");
    if (!newPassword || newPassword.length < 8) errs.newPassword = t("profile.err.min8");
    else if (!/[A-Z]/.test(newPassword) || !/[a-z]/.test(newPassword) || !/[0-9]/.test(newPassword))
      errs.newPassword = t("profile.err.complexity");
    if (newPassword !== confirmPassword) errs.confirmPassword = t("profile.err.mismatch");
    if (Object.keys(errs).length > 0) { setPwErrors(errs); return; }
    changePassword.mutate({ cur: currentPassword, next: newPassword });
  }

  // Avatar upload — presign → PUT → PATCH avatarUrl on profile.
  const fileInputRef = useRef<HTMLInputElement | null>(null);
  const [uploadingAvatar, setUploadingAvatar] = useState(false);

  async function onPickAvatar(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    e.target.value = "";
    if (!file) return;
    if (file.size > AVATAR_MAX_BYTES) { toast.error(t("profile.err.imageSize")); return; }
    if (file.type && !AVATAR_MIME.includes(file.type)) {
      toast.error(t("profile.err.imageType"));
      return;
    }

    setUploadingAvatar(true);
    try {
      const presign = await api
        .post("/media/presign", { fileName: file.name, contentType: file.type || "application/octet-stream" })
        .then((r) => (r.data?.data ?? r.data) as { url: string; publicUrl: string; key: string });
      await axios.put(presign.url, file, { headers: { "Content-Type": file.type || "application/octet-stream" } });
      const confirmedUrl = await confirmUpload(presign.key, file.type || "application/octet-stream");
      const updated = await usersApi.updateMe({ avatarUrl: confirmedUrl });
      qc.setQueryData(["users", "me"], updated);
      patchUser({ avatarUrl: updated.avatarUrl ?? null });
      toast.success(t("profile.pictureUpdated"));
    } catch {
      toast.error(t("profile.err.upload"));
    } finally {
      setUploadingAvatar(false);
    }
  }

  async function removeAvatar() {
    try {
      const updated = await usersApi.updateMe({ avatarUrl: "" });
      qc.setQueryData(["users", "me"], updated);
      patchUser({ avatarUrl: null });
      toast.success(t("profile.pictureRemoved"));
    } catch {
      toast.error(t("profile.err.remove"));
    }
  }

  const headerAvatarUrl = profile?.avatarUrl ?? cachedUser?.avatarUrl ?? undefined;
  const headerName = profile?.displayName ?? cachedUser?.displayName ?? "";
  const initial = headerName ? headerName[0]?.toUpperCase() : "?";

  return (
    <div className="max-w-2xl mx-auto space-y-4">
      <PageHeader
        title={t("profile.title")}
        icon={UserIcon}
        description={t("profile.subtitle")}
      />

      <Card>
        <CardContent className="pt-6 flex flex-col sm:flex-row items-center gap-4">
          <div className="relative">
            <Avatar className="h-20 w-20 sm:h-24 sm:w-24">
              <AvatarImage src={headerAvatarUrl} alt={headerName} />
              <AvatarFallback className="text-2xl">{initial}</AvatarFallback>
            </Avatar>
            <button
              type="button"
              onClick={() => fileInputRef.current?.click()}
              disabled={uploadingAvatar}
              className="absolute -bottom-1 -end-1 rounded-full bg-primary text-primary-foreground p-1.5 shadow hover:bg-primary/90 disabled:opacity-50"
              aria-label={t("profile.changePicture")}
              title={t("profile.changePicture")}
            >
              {uploadingAvatar
                ? <Loader2 aria-hidden="true" className="h-3.5 w-3.5 animate-spin" />
                : <Camera aria-hidden="true" className="h-3.5 w-3.5" />}
            </button>
            <input
              ref={fileInputRef}
              type="file"
              accept="image/jpeg,image/png,image/webp,image/gif"
              className="hidden"
              onChange={onPickAvatar}
            />
          </div>
          <div className="flex-1 min-w-0 text-center sm:text-start">
            <h2 className="text-lg font-bold truncate">{headerName}</h2>
            <p className="text-xs text-muted-foreground truncate">{profile?.email ?? cachedUser?.email}</p>
            {profile?.roles?.length ? (
              <p className="text-xs text-muted-foreground mt-1">{profile.roles.join(" • ")}</p>
            ) : null}
            {profile?.avatarUrl && (
              <Button variant="link" size="sm" className="px-0 h-auto mt-2 text-destructive" onClick={removeAvatar}>
                {t("profile.removePicture")}
              </Button>
            )}
          </div>
        </CardContent>
      </Card>

      <Tabs defaultValue="info">
        <TabsList className="grid w-full grid-cols-2">
          <TabsTrigger value="info">{t("profile.tabInfo")}</TabsTrigger>
          <TabsTrigger value="password">{t("auth.password")}</TabsTrigger>
        </TabsList>

        <TabsContent value="info">
          <Card>
            <CardHeader>
              <CardTitle className="text-base">{t("profile.personalInfo")}</CardTitle>
            </CardHeader>
            <CardContent>
              {isLoading ? (
                <div className="space-y-3" aria-busy="true" role="status" aria-label={t("common.loading")}>
                  {[...Array(4)].map((_, i) => <Skeleton key={i} className="h-10 w-full" />)}
                </div>
              ) : (
                <form onSubmit={onSaveProfile} className="space-y-4">
                  <DraftBanner<typeof profileForm>
                    draft={draft}
                    onRestore={(d) => {
                      setDisplayName(d.displayName ?? "");
                      setEmail(d.email ?? "");
                      setPhoneNumber(d.phoneNumber ?? "");
                      setBio(d.bio ?? "");
                      setLocation(d.location ?? "");
                    }}
                  />
                  <FormField id="displayName" label={t("register.displayName")} error={profileErrors.displayName}>
                    {(c) => (
                      <Input
                        {...c}
                        value={displayName}
                        onChange={(e) => setDisplayName(e.target.value)}
                        maxLength={128}
                        autoComplete="name"
                      />
                    )}
                  </FormField>

                  <FormField id="email" label={t("auth.email")} error={profileErrors.email} hint={t("profile.emailHint")}>
                    {(c) => (
                      <Input
                        {...c}
                        type="email"
                        value={email}
                        onChange={(e) => setEmail(e.target.value)}
                        maxLength={256}
                        autoComplete="email"
                      />
                    )}
                  </FormField>

                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                    <FormField id="phoneNumber" label={t("profile.phone")} error={profileErrors.phoneNumber}>
                      {(c) => (
                        <Input
                          {...c}
                          value={phoneNumber}
                          onChange={(e) => setPhoneNumber(e.target.value)}
                          maxLength={32}
                          autoComplete="tel"
                          placeholder={t("profile.optional")}
                        />
                      )}
                    </FormField>
                    <FormField id="location" label={t("profile.location")}>
                      {(c) => (
                        <Input
                          {...c}
                          value={location}
                          onChange={(e) => setLocation(e.target.value)}
                          maxLength={128}
                          placeholder={t("profile.locationPlaceholder")}
                        />
                      )}
                    </FormField>
                  </div>

                  <FormField id="bio" label={t("profile.bio")}>
                    {(c) => (
                      <Textarea
                        {...c}
                        value={bio}
                        onChange={(e) => setBio(e.target.value)}
                        rows={3}
                        maxLength={500}
                        placeholder={t("profile.bioPlaceholder")}
                      />
                    )}
                  </FormField>

                  <Separator />

                  <div className="flex justify-end">
                    <Button type="submit" disabled={saveProfile.isPending}>
                      {saveProfile.isPending
                        ? <Loader2 aria-hidden="true" className="h-4 w-4 me-2 animate-spin" />
                        : <Save aria-hidden="true" className="h-4 w-4 me-2" />}
                      {t("profile.saveChanges")}
                    </Button>
                  </div>
                </form>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="password">
          <Card>
            <CardHeader>
              <CardTitle className="text-base flex items-center gap-2">
                <KeyRound aria-hidden="true" className="h-4 w-4 text-primary" /> {t("profile.changePassword")}
              </CardTitle>
            </CardHeader>
            <CardContent>
              <form onSubmit={onChangePassword} className="space-y-4">
                <FormField id="cur" label={t("profile.currentPassword")} error={pwErrors.currentPassword}>
                  {(c) => (
                    <PasswordInput
                      {...c}
                      autoComplete="current-password"
                      value={currentPassword}
                      onChange={(e) => setCurrentPassword(e.target.value)}
                    />
                  )}
                </FormField>

                <FormField id="next" label={t("profile.newPassword")} error={pwErrors.newPassword} hint={t("profile.passwordHint")}>
                  {(c) => (
                    <PasswordInput
                      {...c}
                      autoComplete="new-password"
                      value={newPassword}
                      onChange={(e) => setNewPassword(e.target.value)}
                    />
                  )}
                </FormField>

                <FormField id="confirm" label={t("profile.confirmPassword")} error={pwErrors.confirmPassword}>
                  {(c) => (
                    <PasswordInput
                      {...c}
                      autoComplete="new-password"
                      value={confirmPassword}
                      onChange={(e) => setConfirmPassword(e.target.value)}
                    />
                  )}
                </FormField>

                <Separator />

                <div className="flex justify-end">
                  <Button type="submit" disabled={changePassword.isPending}>
                    {changePassword.isPending
                      ? <Loader2 aria-hidden="true" className="h-4 w-4 me-2 animate-spin" />
                      : <KeyRound aria-hidden="true" className="h-4 w-4 me-2" />}
                    {t("profile.updatePassword")}
                  </Button>
                </div>
              </form>
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
    </div>
  );
}
