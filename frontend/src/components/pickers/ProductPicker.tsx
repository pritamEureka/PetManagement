import { productsV2Api, type ProductSummary } from "@/api/marketplace";
import { useI18n } from "@/lib/i18n";
import { formatCurrency } from "@/lib/format";
import { RemotePicker, type PickerBaseProps } from "./RemotePicker";

interface Props extends PickerBaseProps<ProductSummary> {
  /** Restrict to one store's catalogue. */
  storeId?: string;
}

/** Marketplace product search (public catalogue endpoint, debounced). */
export function ProductPicker({ storeId, placeholder, ...rest }: Props) {
  const { t } = useI18n();
  return (
    <RemotePicker<ProductSummary>
      {...rest}
      queryKey={`products:${storeId ?? "all"}`}
      minChars={2}
      fetcher={(q) => productsV2Api.list({ search: q, storeId, pageSize: 10 }).then((r) => r.items)}
      getKey={(p) => p.id}
      getLabel={(p) => p.name}
      placeholder={placeholder ?? t("picker.product")}
      searchPlaceholder={t("picker.productSearch")}
      emptyText={t("picker.productEmpty")}
      renderItem={(p) => (
        <div className="flex items-center justify-between gap-2">
          <span className="truncate">
            {p.name} <span className="text-xs text-muted-foreground">· {p.storeName}</span>
          </span>
          <span className="text-xs tabular-nums">{formatCurrency(p.discountPrice ?? p.price)}</span>
        </div>
      )}
    />
  );
}
