import { useEffect, useRef, useState } from "react";
import axios from "axios";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Camera, Loader2, User as UserIcon, KeyRound, Save } from "lucide-react";

import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
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

  const saveProfile = useMutation({
    mutationFn: usersApi.updateMe,
    onSuccess: (updated: MyProfile) => {
      qc.setQueryData(["users", "me"], updated);
      patchUser({
        displayName: updated.displayName,
        email: updated.email,
        avatarUrl: updated.avatarUrl ?? null,
      });
      setProfileErrors({});
      toast.success("Profile updated.");
    },
    onError: (err) => {
      const fieldErrs = extractFieldErrors(err);
      setProfileErrors(fieldErrs);
      const top = topLevelMessage(err);
      toast.error(top ?? "Couldn't save your profile.");
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
      toast.info("No changes to save.");
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
      toast.success("Password changed. Other sessions have been signed out.");
    },
    onError: (err) => {
      const fieldErrs = extractFieldErrors(err);
      setPwErrors(fieldErrs);
      const top = topLevelMessage(err);
      toast.error(top ?? "Couldn't change your password.");
    },
  });

  function onChangePassword(e: React.FormEvent) {
    e.preventDefault();
    const errs: FieldErrors = {};
    if (!currentPassword) errs.currentPassword = "Current password is required.";
    if (!newPassword || newPassword.length < 8) errs.newPassword = "At least 8 characters.";
    else if (!/[A-Z]/.test(newPassword) || !/[a-z]/.test(newPassword) || !/[0-9]/.test(newPassword))
      errs.newPassword = "Must include upper, lower and a digit.";
    if (newPassword !== confirmPassword) errs.confirmPassword = "Passwords don't match.";
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
    if (file.size > AVATAR_MAX_BYTES) { toast.error("Image must be under 5 MB."); return; }
    if (file.type && !AVATAR_MIME.includes(file.type)) {
      toast.error("Use a JPEG, PNG, WebP or GIF image.");
      return;
    }

    setUploadingAvatar(true);
    try {
      const presign = await api
        .post("/media/presign", { fileName: file.name, contentType: file.type || "application/octet-stream" })
        .then((r) => r.data as { url: string; publicUrl: string });
      await axios.put(presign.url, file, { headers: { "Content-Type": file.type || "application/octet-stream" } });
      const updated = await usersApi.updateMe({ avatarUrl: presign.publicUrl });
      qc.setQueryData(["users", "me"], updated);
      patchUser({ avatarUrl: updated.avatarUrl ?? null });
      toast.success("Profile picture updated.");
    } catch {
      toast.error("Couldn't upload your picture. Try again.");
    } finally {
      setUploadingAvatar(false);
    }
  }

  async function removeAvatar() {
    try {
      const updated = await usersApi.updateMe({ avatarUrl: "" });
      qc.setQueryData(["users", "me"], updated);
      patchUser({ avatarUrl: null });
      toast.success("Profile picture removed.");
    } catch {
      toast.error("Couldn't remove your picture.");
    }
  }

  const headerAvatarUrl = profile?.avatarUrl ?? cachedUser?.avatarUrl ?? undefined;
  const headerName = profile?.displayName ?? cachedUser?.displayName ?? "";
  const initial = headerName ? headerName[0]?.toUpperCase() : "?";

  return (
    <div className="max-w-2xl mx-auto space-y-4">
      <PageHeader
        title="My profile"
        icon={UserIcon}
        description="Update your personal details, profile picture, and sign-in password."
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
              className="absolute -bottom-1 -right-1 rounded-full bg-primary text-primary-foreground p-1.5 shadow hover:bg-primary/90 disabled:opacity-50"
              aria-label="Change profile picture"
              title="Change profile picture"
            >
              {uploadingAvatar
                ? <Loader2 className="h-3.5 w-3.5 animate-spin" />
                : <Camera className="h-3.5 w-3.5" />}
            </button>
            <input
              ref={fileInputRef}
              type="file"
              accept="image/jpeg,image/png,image/webp,image/gif"
              className="hidden"
              onChange={onPickAvatar}
            />
          </div>
          <div className="flex-1 min-w-0 text-center sm:text-left">
            <h2 className="text-lg font-bold truncate">{headerName}</h2>
            <p className="text-xs text-muted-foreground truncate">{profile?.email ?? cachedUser?.email}</p>
            {profile?.roles?.length ? (
              <p className="text-xs text-muted-foreground mt-1">{profile.roles.join(" • ")}</p>
            ) : null}
            {profile?.avatarUrl && (
              <Button variant="link" size="sm" className="px-0 h-auto mt-2 text-destructive" onClick={removeAvatar}>
                Remove picture
              </Button>
            )}
          </div>
        </CardContent>
      </Card>

      <Tabs defaultValue="info">
        <TabsList className="grid w-full grid-cols-2">
          <TabsTrigger value="info">Personal info</TabsTrigger>
          <TabsTrigger value="password">Password</TabsTrigger>
        </TabsList>

        <TabsContent value="info">
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Personal information</CardTitle>
            </CardHeader>
            <CardContent>
              {isLoading ? (
                <div className="space-y-3">
                  {[...Array(4)].map((_, i) => <Skeleton key={i} className="h-10 w-full" />)}
                </div>
              ) : (
                <form onSubmit={onSaveProfile} className="space-y-4">
                  <div>
                    <Label htmlFor="displayName">Display name</Label>
                    <Input
                      id="displayName"
                      value={displayName}
                      onChange={(e) => setDisplayName(e.target.value)}
                      maxLength={128}
                      autoComplete="name"
                    />
                    {profileErrors.displayName && (
                      <p className="text-xs text-destructive mt-1">{profileErrors.displayName}</p>
                    )}
                  </div>

                  <div>
                    <Label htmlFor="email">Email</Label>
                    <Input
                      id="email"
                      type="email"
                      value={email}
                      onChange={(e) => setEmail(e.target.value)}
                      maxLength={256}
                      autoComplete="email"
                    />
                    {profileErrors.email && (
                      <p className="text-xs text-destructive mt-1">{profileErrors.email}</p>
                    )}
                    <p className="text-xs text-muted-foreground mt-1">
                      Changing your email will require re-verification.
                    </p>
                  </div>

                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                    <div>
                      <Label htmlFor="phoneNumber">Phone</Label>
                      <Input
                        id="phoneNumber"
                        value={phoneNumber}
                        onChange={(e) => setPhoneNumber(e.target.value)}
                        maxLength={32}
                        autoComplete="tel"
                        placeholder="Optional"
                      />
                      {profileErrors.phoneNumber && (
                        <p className="text-xs text-destructive mt-1">{profileErrors.phoneNumber}</p>
                      )}
                    </div>
                    <div>
                      <Label htmlFor="location">Location</Label>
                      <Input
                        id="location"
                        value={location}
                        onChange={(e) => setLocation(e.target.value)}
                        maxLength={128}
                        placeholder="City, country"
                      />
                    </div>
                  </div>

                  <div>
                    <Label htmlFor="bio">Bio</Label>
                    <Textarea
                      id="bio"
                      value={bio}
                      onChange={(e) => setBio(e.target.value)}
                      rows={3}
                      maxLength={500}
                      placeholder="Tell people a little about you (optional)"
                    />
                  </div>

                  <Separator />

                  <div className="flex justify-end">
                    <Button type="submit" disabled={saveProfile.isPending}>
                      {saveProfile.isPending
                        ? <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                        : <Save className="h-4 w-4 mr-2" />}
                      Save changes
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
                <KeyRound className="h-4 w-4 text-primary" /> Change password
              </CardTitle>
            </CardHeader>
            <CardContent>
              <form onSubmit={onChangePassword} className="space-y-4">
                <div>
                  <Label htmlFor="cur">Current password</Label>
                  <PasswordInput
                    id="cur"
                    autoComplete="current-password"
                    value={currentPassword}
                    onChange={(e) => setCurrentPassword(e.target.value)}
                  />
                  {pwErrors.currentPassword && (
                    <p className="text-xs text-destructive mt-1">{pwErrors.currentPassword}</p>
                  )}
                </div>

                <div>
                  <Label htmlFor="next">New password</Label>
                  <PasswordInput
                    id="next"
                    autoComplete="new-password"
                    value={newPassword}
                    onChange={(e) => setNewPassword(e.target.value)}
                  />
                  {pwErrors.newPassword && (
                    <p className="text-xs text-destructive mt-1">{pwErrors.newPassword}</p>
                  )}
                  <p className="text-xs text-muted-foreground mt-1">
                    Min 8 characters, with an upper, a lower and a digit.
                  </p>
                </div>

                <div>
                  <Label htmlFor="confirm">Confirm new password</Label>
                  <PasswordInput
                    id="confirm"
                    autoComplete="new-password"
                    value={confirmPassword}
                    onChange={(e) => setConfirmPassword(e.target.value)}
                  />
                  {pwErrors.confirmPassword && (
                    <p className="text-xs text-destructive mt-1">{pwErrors.confirmPassword}</p>
                  )}
                </div>

                <Separator />

                <div className="flex justify-end">
                  <Button type="submit" disabled={changePassword.isPending}>
                    {changePassword.isPending
                      ? <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                      : <KeyRound className="h-4 w-4 mr-2" />}
                    Update password
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
