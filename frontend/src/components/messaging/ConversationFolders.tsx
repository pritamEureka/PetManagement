import { useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { Pencil, Trash2, Check, X, FolderCog } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { toast } from "@/components/ui/sonner";
import { cn } from "@/lib/utils";
import { messagesPlusApi, type ConversationFolder } from "@/api/messages";
import { useI18n } from "@/lib/i18n";

export const FOLDERS_QUERY_KEY = ["messaging", "folders"];
const errMsg = (e: any) => e?.response?.data?.error?.message ?? e?.response?.data?.detail ?? "Something went wrong.";

/** Filter chips above the conversation list: All + one chip per folder + manage button. */
export function FolderFilterBar({ folders, selected, onSelect, onManage }: {
  folders: ConversationFolder[]; selected: string | null; onSelect: (id: string | null) => void; onManage: () => void;
}) {
  const { t } = useI18n();
  const chip = (active: boolean) => cn(
    "shrink-0 rounded-full border px-2.5 py-1 text-xs transition-colors",
    active ? "bg-primary text-primary-foreground border-primary" : "hover:bg-muted"
  );
  return (
    <div className="flex items-center gap-1 overflow-x-auto pb-1" role="tablist" aria-label={t("adoptmsg.folders.title")}>
      <button type="button" className={chip(selected === null)} onClick={() => onSelect(null)}>{t("adoptmsg.folders.all")}</button>
      {folders.map((f) => (
        <button key={f.id} type="button" className={chip(selected === f.id)} onClick={() => onSelect(f.id)}>
          {f.name} <span className="opacity-70">{f.conversationIds.length}</span>
        </button>
      ))}
      <Button type="button" variant="ghost" size="icon" className="h-7 w-7 shrink-0" title={t("adoptmsg.folders.manage")} aria-label={t("adoptmsg.folders.manage")} onClick={onManage}>
        <FolderCog className="h-3.5 w-3.5" />
      </Button>
    </div>
  );
}

/** Create / rename / delete folders. */
export function FolderManageDialog({ open, onOpenChange, folders, onDeleted }: {
  open: boolean; onOpenChange: (o: boolean) => void; folders: ConversationFolder[]; onDeleted?: (id: string) => void;
}) {
  const { t } = useI18n();
  const qc = useQueryClient();
  const [name, setName] = useState("");
  const [editId, setEditId] = useState<string | null>(null);
  const [editName, setEditName] = useState("");
  const reload = () => qc.invalidateQueries({ queryKey: FOLDERS_QUERY_KEY });

  async function create() {
    if (!name.trim()) return;
    try { await messagesPlusApi.createFolder(name.trim()); setName(""); reload(); } catch (e) { toast.error(errMsg(e)); }
  }
  async function rename(id: string) {
    if (!editName.trim()) return;
    try { await messagesPlusApi.renameFolder(id, editName.trim()); setEditId(null); reload(); } catch (e) { toast.error(errMsg(e)); }
  }
  async function remove(f: ConversationFolder) {
    if (!window.confirm(t("adoptmsg.folders.confirmDelete", { name: f.name }))) return;
    try { await messagesPlusApi.deleteFolder(f.id); onDeleted?.(f.id); reload(); } catch (e) { toast.error(errMsg(e)); }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>{t("adoptmsg.folders.manage")}</DialogTitle>
          <DialogDescription>{t("adoptmsg.folders.hint")}</DialogDescription>
        </DialogHeader>
        <ul className="space-y-1">
          {folders.length === 0 && <li className="text-sm text-muted-foreground">{t("adoptmsg.folders.empty")}</li>}
          {folders.map((f) => (
            <li key={f.id} className="flex items-center gap-1">
              {editId === f.id ? (
                <>
                  <Input className="h-8" value={editName} maxLength={80} autoFocus onChange={(e) => setEditName(e.target.value)}
                         onKeyDown={(e) => { if (e.key === "Enter") rename(f.id); if (e.key === "Escape") setEditId(null); }} />
                  <Button size="icon" variant="ghost" className="h-8 w-8" onClick={() => rename(f.id)} aria-label={t("adoptmsg.folders.save")}><Check className="h-4 w-4" /></Button>
                  <Button size="icon" variant="ghost" className="h-8 w-8" onClick={() => setEditId(null)} aria-label={t("adoptmsg.folders.cancel")}><X className="h-4 w-4" /></Button>
                </>
              ) : (
                <>
                  <span className="flex-1 truncate text-sm">{f.name} <span className="text-xs text-muted-foreground">({f.conversationIds.length})</span></span>
                  <Button size="icon" variant="ghost" className="h-8 w-8" onClick={() => { setEditId(f.id); setEditName(f.name); }} aria-label={t("adoptmsg.folders.rename")}><Pencil className="h-4 w-4" /></Button>
                  <Button size="icon" variant="ghost" className="h-8 w-8" onClick={() => remove(f)} aria-label={t("adoptmsg.folders.delete")}><Trash2 className="h-4 w-4" /></Button>
                </>
              )}
            </li>
          ))}
        </ul>
        <div className="flex gap-2">
          <Input placeholder={t("adoptmsg.folders.newName")} value={name} maxLength={80} onChange={(e) => setName(e.target.value)}
                 onKeyDown={(e) => { if (e.key === "Enter") create(); }} />
          <Button onClick={create} disabled={!name.trim()}>{t("adoptmsg.folders.create")}</Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
