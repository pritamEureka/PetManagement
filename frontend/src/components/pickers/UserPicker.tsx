import { usersApi, type PublicUser } from "@/api/users";
import { useI18n } from "@/lib/i18n";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import { RemotePicker, type PickerBaseProps } from "./RemotePicker";

/**
 * Debounced user directory search. Uses the existing authenticated
 * GET /api/v1/users?q= endpoint (min 2 chars, 10 results; the endpoint already
 * excludes the caller, suspended, inactive and unapproved accounts and returns
 * only id/displayName/avatarUrl/primaryRole), so no new endpoint was needed.
 */
export function UserPicker({ placeholder, ...rest }: PickerBaseProps<PublicUser>) {
  const { t } = useI18n();
  return (
    <RemotePicker<PublicUser>
      {...rest}
      queryKey="users"
      minChars={2}
      fetcher={(q) => usersApi.list({ q, pageSize: 10 }).then((r) => r.items)}
      getKey={(u) => u.id}
      getLabel={(u) => u.displayName}
      placeholder={placeholder ?? t("picker.user")}
      searchPlaceholder={t("picker.userSearch")}
      emptyText={t("picker.userEmpty")}
      renderItem={(u) => (
        <div className="flex items-center gap-2">
          <Avatar className="h-6 w-6">
            <AvatarImage src={u.avatarUrl ?? undefined} />
            <AvatarFallback>{u.displayName?.[0] ?? "?"}</AvatarFallback>
          </Avatar>
          <span className="truncate">{u.displayName}</span>
          {u.primaryRole && <span className="text-[10px] text-muted-foreground">{u.primaryRole}</span>}
        </div>
      )}
    />
  );
}
