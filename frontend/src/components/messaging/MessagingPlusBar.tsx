import { useEffect, useState } from "react";
import { Search, Pin, Timer, Lock, LockOpen, X } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger
} from "@/components/ui/dropdown-menu";
import { messagesPlusApi, type ConversationSettings, type MessageSearchHit, type PinnedMessage } from "@/api/messages";
import { toast } from "@/components/ui/sonner";
import { useI18n } from "@/lib/i18n";

const TIMERS: { label: string; seconds: number | null }[] = [
  { label: "Off", seconds: null },
  { label: "1 hour", seconds: 3600 },
  { label: "24 hours", seconds: 86400 },
  { label: "7 days", seconds: 604800 },
  { label: "30 days", seconds: 2592000 },
];

interface Props {
  conversationId: string;
  settings?: ConversationSettings;
  pins: PinnedMessage[];
  decrypt: (c?: string | null) => Promise<string>;
  onSettingsChange: (s: ConversationSettings) => void;
  onUnpin: (messageId: string) => void;
  e2eeError?: string | null;
}

export function MessagingPlusBar({ conversationId, settings, pins, decrypt, onSettingsChange, onUnpin, e2eeError }: Props) {
  const { t } = useI18n();
  const [q, setQ] = useState("");
  const [open, setOpen] = useState(false);
  const [hits, setHits] = useState<MessageSearchHit[]>([]);

  useEffect(() => {
    setQ(""); setHits([]); setOpen(false);
  }, [conversationId]);

  useEffect(() => {
    if (q.trim().length < 2) { setHits([]); return; }
    const h = window.setTimeout(() => {
      messagesPlusApi.search(q.trim(), conversationId).then((r) => setHits(r.items)).catch(() => setHits([]));
    }, 300);
    return () => window.clearTimeout(h);
  }, [q, conversationId]);

  async function setTimer(seconds: number | null) {
    try { onSettingsChange(await messagesPlusApi.setDisappear(conversationId, seconds)); }
    catch (e: any) { toast.error(e?.response?.data?.error?.message ?? "Couldn't change timer."); }
  }
  async function toggleE2ee() {
    try { onSettingsChange(await messagesPlusApi.setE2ee(conversationId, !settings?.e2eeEnabled)); }
    catch (e: any) { toast.error(e?.response?.data?.error?.message ?? "Couldn't change encryption."); }
  }

  return (
    <div className="border-b text-sm">
      <div className="flex items-center gap-1 px-2 py-1">
        <Button variant="ghost" size="icon" className="h-7 w-7" aria-label={t("adoptmsg.search")} onClick={() => setOpen((v) => !v)}>
          {open ? <X className="h-4 w-4" /> : <Search className="h-4 w-4" />}
        </Button>
        {open && <Input autoFocus className="h-7" placeholder={t("adoptmsg.searchPlaceholder")} value={q} onChange={(e) => setQ(e.target.value)} />}
        <div className="ml-auto flex items-center gap-1">
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="ghost" size="sm" className="h-7 gap-1">
                <Timer className="h-4 w-4" />
                <span className="text-xs">{TIMERS.find((x) => x.seconds === (settings?.disappearAfterSeconds ?? null))?.label ?? "Custom"}</span>
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              {TIMERS.map((x) => <DropdownMenuItem key={x.label} onClick={() => setTimer(x.seconds)}>{x.label}</DropdownMenuItem>)}
            </DropdownMenuContent>
          </DropdownMenu>
          <Button variant="ghost" size="sm" className="h-7 gap-1" onClick={toggleE2ee}
                  title={t("adoptmsg.e2eeHint")}>
            {settings?.e2eeEnabled ? <Lock className="h-4 w-4 text-emerald-600" /> : <LockOpen className="h-4 w-4" />}
            <span className="text-xs">{settings?.e2eeEnabled ? t("adoptmsg.e2eeOn") : t("adoptmsg.e2eeOff")}</span>
          </Button>
        </div>
      </div>
      {e2eeError && settings?.e2eeEnabled && <p className="px-3 pb-1 text-xs text-destructive">{e2eeError}</p>}
      {open && hits.length > 0 && (
        <ul className="max-h-40 overflow-auto px-3 pb-2 space-y-1">
          {hits.map((h) => (
            <li key={h.messageId} className="text-xs"><span className="font-semibold">{h.senderName}</span>: {h.content}
              <span className="text-muted-foreground"> · {new Date(h.createdAt).toLocaleString()}</span></li>
          ))}
        </ul>
      )}
      {pins.length > 0 && (
        <div className="px-3 pb-2 space-y-1">
          {pins.map((p) => <PinRow key={p.messageId} p={p} decrypt={decrypt} onUnpin={onUnpin} />)}
        </div>
      )}
    </div>
  );
}

function PinRow({ p, decrypt, onUnpin }: { p: PinnedMessage; decrypt: (c?: string | null) => Promise<string>; onUnpin: (id: string) => void }) {
  const [text, setText] = useState(p.content ?? "");
  useEffect(() => { decrypt(p.content).then(setText); }, [p.content, decrypt]);
  return (
    <div className="flex items-center gap-2 rounded bg-muted px-2 py-1 text-xs">
      <Pin className="h-3 w-3 shrink-0" />
      <span className="truncate"><b>{p.senderName}:</b> {text}</span>
      <button className="ml-auto opacity-60 hover:opacity-100" aria-label="Unpin" onClick={() => onUnpin(p.messageId)}><X className="h-3 w-3" /></button>
    </div>
  );
}
