import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { MessageSquare } from "lucide-react";
import { Button } from "@/components/ui/button";
import { messagesApi } from "@/api/messages";
import { toast } from "@/components/ui/sonner";

interface Props {
  ownerId: string;
  listingId: string;
  variant?: "default" | "outline" | "secondary";
}

export function MessageOwnerButton({ ownerId, listingId, variant = "default" }: Props) {
  const nav = useNavigate();
  const [busy, setBusy] = useState(false);

  async function open() {
    setBusy(true);
    try {
      const conv = await messagesApi.start(ownerId, "adoption", listingId);
      nav(`/messages?c=${conv.id}`);
    } catch (err: any) {
      toast.error(err?.response?.data?.error?.message ?? "Couldn't open conversation.");
    } finally { setBusy(false); }
  }

  return (
    <Button variant={variant} onClick={open} disabled={busy}>
      <MessageSquare className="h-4 w-4 mr-2" /> Message owner
    </Button>
  );
}
