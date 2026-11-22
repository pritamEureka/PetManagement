import { useQuery } from "@tanstack/react-query";
import { policyApi } from "@/api/clinicPlus";

/** Shows what cancelling now costs under the doctor's policy (fee, refund). Renders nothing for fee-free cancellations. */
export function CancellationQuoteNote({ appointmentId }: { appointmentId: string }) {
  const { data } = useQuery({
    queryKey: ["appointments", appointmentId, "cancellation-quote"],
    queryFn: () => policyApi.quote(appointmentId),
    retry: false
  });
  if (!data) return null;
  if (!data.isLate && data.refund === 0) return null;
  return (
    <span className="block mt-2 text-xs">
      {data.isLate
        ? `Late cancellation (inside ${data.windowHours}h): fee ${data.fee.toFixed(2)}, deposit forfeited ${data.forfeited.toFixed(2)}, refund ${data.refund.toFixed(2)}${data.outstanding > 0 ? `, still owed ${data.outstanding.toFixed(2)}` : ""}.`
        : `Free cancellation. Deposit refund: ${data.refund.toFixed(2)}.`}
    </span>
  );
}
