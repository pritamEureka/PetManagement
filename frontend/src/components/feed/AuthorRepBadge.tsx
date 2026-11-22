import { ReputationBadge } from "@/components/communities/ReputationBadge";
import { useReputationBadges } from "@/hooks/useReputationBadges";

/** Reputation pill next to an author's name; renders nothing below level 1 or while loading. Requests are batched + cached. */
export function AuthorRepBadge({ userId }: { userId?: string | null }) {
  const badges = useReputationBadges([userId]);
  const b = userId ? badges[userId] : undefined;
  if (!b) return null;
  return <span className="ml-1 align-middle"><ReputationBadge level={b.level} levelName={b.levelName} total={b.total} /></span>;
}
