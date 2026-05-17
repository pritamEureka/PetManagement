import { Check, CheckCheck, FileText, MoreHorizontal, Trash2, Flag } from "lucide-react";
import { formatDistanceToNow } from "date-fns";
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger
} from "@/components/ui/dropdown-menu";
import { Button } from "@/components/ui/button";
import type { ChatMessage } from "@/api/messages";

interface Props {
  msg: ChatMessage;
  mine: boolean;
  /** Per-message read state across recipients (delivered / read). */
  state?: "sending" | "sent" | "delivered" | "read";
  onDelete?: () => void;
  onReport?: () => void;
}

export function MessageBubble({ msg, mine, state, onDelete, onReport }: Props) {
  const text = msg.isDeletedForAll ? "Message deleted" : msg.content;

  return (
    <div className={`group flex ${mine ? "justify-end" : "justify-start"} gap-1`}>
      <div className={`max-w-[78%] rounded-2xl px-3 py-2 text-sm space-y-2 ${
        mine ? "bg-primary text-primary-foreground rounded-br-md" : "bg-muted rounded-bl-md"
      }`}>
        {!mine && <p className="text-[10px] font-semibold opacity-80">{msg.senderName}</p>}

        {msg.attachments.length > 0 && (
          <div className="grid grid-cols-2 gap-1.5">
            {msg.attachments.map((a, i) => (
              <Attachment key={i} att={a} />
            ))}
          </div>
        )}

        {text && <p className={msg.isDeletedForAll ? "italic opacity-70" : "whitespace-pre-wrap"}>{text}</p>}

        <p className={`text-[10px] flex items-center gap-1 ${mine ? "opacity-80" : "text-muted-foreground"} justify-end`}>
          {formatDistanceToNow(new Date(msg.createdAt), { addSuffix: true })}
          {mine && <ReceiptIcon state={state ?? "sent"} />}
        </p>
      </div>

      {/* Hover menu */}
      {(onDelete || onReport) && (
        <div className="opacity-0 group-hover:opacity-100 transition-opacity self-center">
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="ghost" size="icon" className="h-6 w-6"><MoreHorizontal className="h-3 w-3" /></Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align={mine ? "end" : "start"}>
              {mine && onDelete && (
                <DropdownMenuItem destructive onClick={onDelete}>
                  <Trash2 className="h-3.5 w-3.5 mr-2" /> Delete for everyone
                </DropdownMenuItem>
              )}
              {!mine && onReport && (
                <DropdownMenuItem destructive onClick={onReport}>
                  <Flag className="h-3.5 w-3.5 mr-2" /> Report
                </DropdownMenuItem>
              )}
            </DropdownMenuContent>
          </DropdownMenu>
        </div>
      )}
    </div>
  );
}

function Attachment({ att }: { att: { url: string; mimeType: string; fileName?: string | null } }) {
  if (att.mimeType.startsWith("image/")) {
    return <a href={att.url} target="_blank" rel="noreferrer"><img src={att.url} className="rounded-md w-full h-32 object-cover" /></a>;
  }
  if (att.mimeType.startsWith("video/")) {
    return <video src={att.url} controls className="rounded-md w-full h-32" />;
  }
  return (
    <a href={att.url} target="_blank" rel="noreferrer"
       className="flex items-center gap-2 rounded-md border border-current/20 px-2 py-1.5 text-xs">
      <FileText className="h-4 w-4" />
      <span className="truncate">{att.fileName ?? "Attachment"}</span>
    </a>
  );
}

function ReceiptIcon({ state }: { state: "sending" | "sent" | "delivered" | "read" }) {
  if (state === "sending") return <span>•</span>;
  if (state === "sent")      return <Check className="h-3 w-3" />;
  if (state === "delivered") return <CheckCheck className="h-3 w-3" />;
  return <CheckCheck className="h-3 w-3 text-emerald-300" />;
}
