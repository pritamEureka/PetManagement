import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { MessageSquare } from "lucide-react";
import { Button, type ButtonProps } from "@/components/ui/button";
import { messagesApi } from "@/api/messages";
import { toast } from "@/components/ui/sonner";

interface Props {
  userId: string;
  /** Used only for the toast / fallback label. */
  userName?: string;
  /** Optional anchor for the conversation (e.g. "adoption" + listing id). */
  contextType?: string;
  contextRefId?: string;
  /** Render as icon-only (for tight rows like the conversation list). */
  iconOnly?: boolean;
  label?: string;
  variant?: ButtonProps["variant"];
  size?: ButtonProps["size"];
  className?: string;
}

/**
 * Opens (or creates) a 1-to-1 conversation with the given user and routes to
 * /messages?c=<id>. Reusable wherever we surface "Message this person":
 * profile pages, store pages, vet detail, etc. Replaces the older
 * adoption-only MessageOwnerButton for non-adoption call-sites.
 */
export function MessageUserButton({
  userId, userName, contextType, contextRefId,
  iconOnly = false, label = "Message", variant = "default", size, className
}: Props) {
  const nav = useNavigate();
  const [busy, setBusy] = useState(false);

  async function open(e?: React.MouseEvent) {
    e?.stopPropagation();
    if (busy) return;
    setBusy(true);
    try {
      const conv = await messagesApi.start(userId, contextType, contextRefId);
      nav(`/messages?c=${conv.id}`);
    } catch (err: any) {
      toast.error(
        err?.response?.data?.error?.message ??
        `Couldn't start a chat${userName ? ` with ${userName}` : ""}.`
      );
    } finally {
      setBusy(false);
    }
  }

  if (iconOnly) {
    return (
      <Button
        type="button"
        variant={variant === "default" ? "ghost" : variant}
        size={size ?? "icon"}
        disabled={busy}
        onClick={open}
        className={className}
        title={userName ? `Message ${userName}` : "Message"}
      >
        <MessageSquare className="h-4 w-4" />
      </Button>
    );
  }

  return (
    <Button type="button" variant={variant} size={size} disabled={busy} onClick={open} className={className}>
      <MessageSquare className="h-4 w-4 mr-2" /> {busy ? "Opening…" : label}
    </Button>
  );
}
