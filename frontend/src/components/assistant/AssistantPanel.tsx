import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { AlertTriangle, Bot, ClipboardList, Loader2, Send, User as UserIcon, X } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Textarea } from "@/components/ui/textarea";
import { cn } from "@/lib/utils";
import { assistantApi, type AssistantCitation, type AssistantMessage, type TriageResult } from "@/api/assistant";
import { FileUploadButton } from "@/components/common/FileUploadButton";
import { SymptomChecklistDialog } from "./SymptomChecklistDialog";

const DEFAULT_DISCLAIMER =
  "General information only - not veterinary advice. In an emergency contact a vet immediately.";

const SOURCE_LABEL: Record<string, string> = { knowledge: "Guide", pet: "Your pet", product: "Product", vet: "Vet" };

interface Props {
  conversationId?: string | null;
  petId?: string | null;
  onConversationChange?: (id: string | null) => void;
  className?: string;
}

function Citations({ items }: { items: AssistantCitation[] }) {
  if (!items.length) return null;
  return (
    <div className="mt-2 flex flex-wrap gap-1.5" aria-label="Sources">
      <span className="text-xs text-muted-foreground mr-1">Sources:</span>
      {items.map((c, i) => {
        const label = `${SOURCE_LABEL[c.sourceType] ?? c.sourceType}: ${c.title}`;
        return c.url ? (
          <Link key={`${c.sourceType}-${c.id}-${i}`} to={c.url}>
            <Badge variant="outline" className="hover:bg-accent cursor-pointer">{label}</Badge>
          </Link>
        ) : (
          <Badge key={`${c.sourceType}-${c.id}-${i}`} variant="muted">{label}</Badge>
        );
      })}
    </div>
  );
}

export function AssistantPanel({ conversationId = null, petId = null, onConversationChange, className }: Props) {
  const qc = useQueryClient();
  const [convId, setConvId] = useState<string | null>(conversationId);
  const [messages, setMessages] = useState<AssistantMessage[]>([]);
  const [input, setInput] = useState("");
  const [disclaimer, setDisclaimer] = useState(DEFAULT_DISCLAIMER);
  const [error, setError] = useState<string | null>(null);
  const [images, setImages] = useState<string[]>([]);
  const [checkOpen, setCheckOpen] = useState(false);
  const bottomRef = useRef<HTMLDivElement>(null);

  // Load an existing thread when the parent selects one; clear for "new chat".
  // If the parent echoes back the id we just created, keep the local messages.
  useEffect(() => {
    if (conversationId === convId && messages.length > 0) return;
    setConvId(conversationId);
    setError(null);
    if (!conversationId) { setMessages([]); return; }
    let cancelled = false;
    assistantApi.get(conversationId)
      .then(c => { if (!cancelled) setMessages(c.messages); })
      .catch(() => { if (!cancelled) setError("Could not load this conversation."); });
    return () => { cancelled = true; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [conversationId]);

  useEffect(() => { bottomRef.current?.scrollIntoView({ behavior: "smooth" }); }, [messages.length]);

  const send = useMutation({
    mutationFn: (v: { text: string; imageUrls: string[] }) => assistantApi.send(v.text, convId, petId, v.imageUrls),
    onSuccess: (ex) => {
      setMessages(m => [...m, ex.assistantMessage]);
      setDisclaimer(ex.disclaimer || DEFAULT_DISCLAIMER);
      if (ex.conversationId !== convId) {
        setConvId(ex.conversationId);
        onConversationChange?.(ex.conversationId);
      }
      qc.invalidateQueries({ queryKey: ["assistant-conversations"] });
    },
    onError: () => setError("Message could not be sent. Please try again in a moment.")
  });

  const submit = () => {
    const text = input.trim();
    if (!text || send.isPending) return;
    setError(null);
    setInput("");
    // Optimistic user bubble; the assistant reply is appended on success.
    setMessages(m => [...m, {
      id: `local-${Date.now()}`, role: "user", content: text, citations: [], escalated: false,
      intent: null, createdAt: new Date().toISOString(), attachments: images.length ? images : null
    }]);
    const imageUrls = images;
    setImages([]);
    send.mutate({ text, imageUrls });
  };

  return (
    <div className={cn("flex flex-col rounded-lg border bg-card h-[70vh] min-h-[420px]", className)}>
      <div className="flex-1 overflow-y-auto p-4 space-y-4" role="log" aria-live="polite">
        {messages.length === 0 && (
          <div className="text-center text-sm text-muted-foreground py-10">
            <Bot className="h-8 w-8 mx-auto mb-2 text-primary" />
            Ask about vaccines, food, grooming, behaviour, products or finding a vet.
          </div>
        )}

        {messages.map(m => (
          <div key={m.id} className={cn("flex gap-2", m.role === "user" && "flex-row-reverse")}>
            <div className="h-7 w-7 shrink-0 rounded-full bg-muted flex items-center justify-center">
              {m.role === "user" ? <UserIcon className="h-4 w-4" /> : <Bot className="h-4 w-4" />}
            </div>
            <div className={cn("max-w-[85%] rounded-lg px-3 py-2 text-sm",
              m.role === "user" ? "bg-primary text-primary-foreground" : "bg-muted")}>
              {m.escalated && (
                <div role="alert" className="mb-2 flex items-start gap-2 rounded-md border border-destructive bg-destructive/10 p-2 text-destructive">
                  <AlertTriangle className="h-4 w-4 mt-0.5 shrink-0" />
                  <div className="font-medium">
                    Possible emergency - contact a vet now.{" "}
                    <Link to="/vets" className="underline">Find a vet</Link>
                  </div>
                </div>
              )}
              <p className="whitespace-pre-wrap">{m.content}</p>
              {!!m.attachments?.length && (
                <div className="mt-2 flex flex-wrap gap-1.5">
                  {m.attachments.map((u, i) => (
                    <a key={u} href={u} target="_blank" rel="noreferrer">
                      <img src={u} alt={`Attached photo ${i + 1}`} className="h-16 w-16 rounded object-cover border" />
                    </a>
                  ))}
                </div>
              )}
              {m.role === "user" && !!m.attachments?.length && (
                <p className="mt-1 text-xs opacity-80">Photo needs human vet review.</p>
              )}
              {m.role === "assistant" && <Citations items={m.citations} />}
            </div>
          </div>
        ))}

        {send.isPending && (
          <div className="flex items-center gap-2 text-sm text-muted-foreground">
            <Loader2 className="h-4 w-4 animate-spin" /> Thinking...
          </div>
        )}
        {error && <p role="alert" className="text-sm text-destructive">{error}</p>}
        <div ref={bottomRef} />
      </div>

      <div className="border-t p-3 space-y-2">
        <form className="flex gap-2 items-end" onSubmit={(e) => { e.preventDefault(); submit(); }}>
          <Textarea
            value={input}
            onChange={(e) => setInput(e.target.value)}
            onKeyDown={(e) => { if (e.key === "Enter" && !e.shiftKey) { e.preventDefault(); submit(); } }}
            placeholder="Ask a question about your pet..."
            rows={2}
            maxLength={2000}
            aria-label="Message"
            className="resize-none"
          />
          <Button type="submit" disabled={!input.trim() || send.isPending} aria-label="Send message">
            <Send className="h-4 w-4" />
          </Button>
        </form>
        {images.length > 0 && (
          <div className="flex flex-wrap items-center gap-2">
            {images.map((u, i) => (
              <span key={u} className="relative">
                <img src={u} alt={`Attached photo ${i + 1}`} className="h-12 w-12 rounded object-cover border" />
                <button type="button" aria-label="Remove photo" onClick={() => setImages(p => p.filter(x => x !== u))}
                  className="absolute -top-1 -right-1 rounded-full bg-background border p-0.5">
                  <X className="h-3 w-3" />
                </button>
              </span>
            ))}
            <span className="text-xs text-muted-foreground">Photo needs human vet review - the assistant cannot interpret images.</span>
          </div>
        )}
        <div className="flex flex-wrap items-center gap-2">
          <FileUploadButton label="Attach photo" variant="ghost" disabled={images.length >= 4 || send.isPending}
            onUploaded={(url) => setImages(p => [...p, url].slice(0, 4))} />
          <Button type="button" size="sm" variant="ghost" onClick={() => setCheckOpen(true)}>
            <ClipboardList className="h-3.5 w-3.5 mr-1" /> Symptom checklist
          </Button>
        </div>
        <p className="text-xs text-muted-foreground">{disclaimer}</p>
        <SymptomChecklistDialog
          open={checkOpen}
          onOpenChange={setCheckOpen}
          conversationId={convId}
          petId={petId}
          onResult={(r: TriageResult) => {
            setMessages(m => [...m, r.assistantMessage]);
            if (r.conversationId !== convId) {
              setConvId(r.conversationId);
              onConversationChange?.(r.conversationId);
            }
            qc.invalidateQueries({ queryKey: ["assistant-conversations"] });
          }}
        />
      </div>
    </div>
  );
}
