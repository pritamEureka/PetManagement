import { Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { Card } from "@/components/ui/card";
import { discoveryApi } from "@/api/shopPlus";
import { useAuthStore } from "@/store/authStore";

/** Horizontal strip of the signed-in user's recently viewed products. Renders nothing when empty. */
export function RecentlyViewed() {
  const signedIn = useAuthStore((s) => !!s.user?.id);
  const { data } = useQuery({
    queryKey: ["recently-viewed"], queryFn: () => discoveryApi.recentlyViewed(12), enabled: signedIn,
  });
  if (!signedIn || !data || data.length === 0) return null;

  return (
    <section className="space-y-2">
      <h2 className="text-lg font-semibold">Recently viewed</h2>
      <div className="flex gap-3 overflow-x-auto pb-2">
        {data.map((p) => (
          <Link key={p.id} to={`/store/products/${p.id}`} className="w-36 shrink-0">
            <Card className="overflow-hidden">
              <div className="aspect-square bg-muted">
                {p.imageUrls[0] && <img src={p.imageUrls[0]} alt={p.name} className="h-full w-full object-cover" />}
              </div>
              <div className="p-2">
                <div className="truncate text-sm">{p.name}</div>
                <div className="text-sm font-semibold">${(p.discountPrice ?? p.price).toFixed(2)}</div>
              </div>
            </Card>
          </Link>
        ))}
      </div>
    </section>
  );
}
