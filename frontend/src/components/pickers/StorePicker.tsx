import { storesApi, type ApprovalStatus, type Store } from "@/api/marketplace";
import { useI18n } from "@/lib/i18n";
import { RemotePicker, type PickerBaseProps } from "./RemotePicker";

interface Props extends PickerBaseProps<Store> {
  /** Filter by approval status (admin screens may want any status). */
  status?: ApprovalStatus;
}

/** Store search by name (debounced). */
export function StorePicker({ status, placeholder, ...rest }: Props) {
  const { t } = useI18n();
  return (
    <RemotePicker<Store>
      {...rest}
      queryKey={`stores:${status ?? "any"}`}
      fetcher={(q) => storesApi.search({ search: q || undefined, status, pageSize: 10 }).then((r) => r.items)}
      getKey={(s) => s.id}
      getLabel={(s) => s.name}
      placeholder={placeholder ?? t("picker.store")}
      searchPlaceholder={t("picker.storeSearch")}
      emptyText={t("picker.storeEmpty")}
      renderItem={(s) => (
        <div className="flex items-center justify-between gap-2">
          <span className="truncate">{s.name}</span>
          {s.city && <span className="text-xs text-muted-foreground">{s.city}</span>}
        </div>
      )}
    />
  );
}
