import { petsApi, accessRank, type Pet, type PetAccessLevel } from "@/api/pets";
import { useI18n } from "@/lib/i18n";
import { RemotePicker, type PickerBaseProps } from "./RemotePicker";

interface Props extends PickerBaseProps<Pet> {
  /** Only show pets where the caller has at least this access (default: Viewer). */
  minAccess?: PetAccessLevel;
}

/** Picks one of my pets or a pet shared with me (loaded once, filtered locally). */
export function PetPicker({ minAccess = "Viewer", placeholder, ...rest }: Props) {
  const { t } = useI18n();
  return (
    <RemotePicker<Pet>
      {...rest}
      queryKey="pets"
      local
      fetcher={() => petsApi.mine()}
      getKey={(p) => p.id}
      getLabel={(p) => p.name}
      filter={(p) => accessRank[p.myAccess] >= accessRank[minAccess]}
      placeholder={placeholder ?? t("picker.pet")}
      searchPlaceholder={t("picker.petSearch")}
      emptyText={t("picker.petEmpty")}
      renderItem={(p) => (
        <div className="flex items-center justify-between gap-2">
          <span className="truncate">
            {p.name} <span className="text-xs text-muted-foreground">· {p.animalType}</span>
          </span>
          {p.myAccess !== "Owner" && <span className="text-[10px] rounded border px-1 text-muted-foreground">{t("picker.shared")}</span>}
        </div>
      )}
    />
  );
}
