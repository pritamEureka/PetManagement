import { useEffect, useState } from "react";
import { decryptText, deriveConversationKey, encryptText, exportPublicJwk, getOrCreateKeyPair, importPublicJwk, isE2eeEnvelope } from "@/lib/e2ee";
import { messagesPlusApi } from "@/api/messages";

/**
 * Opt-in E2EE for a 1:1 conversation. Single device only: the private key stays in this browser's IndexedDB.
 * Derives a shared AES-GCM key from my private key and the other participant's active registered public key.
 */
export function useE2ee(enabled: boolean, myId?: string, otherUserId?: string) {
  const [key, setKey] = useState<CryptoKey | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setKey(null); setError(null);
    if (!enabled || !myId || !otherUserId) return;
    let cancelled = false;
    (async () => {
      try {
        const { pair } = await getOrCreateKeyPair(myId);
        // Always (re)register: server keeps one active key per user and ignores nothing — registering the same key is idempotent for peers.
        const mine = await messagesPlusApi.userKeys(myId);
        const jwk = JSON.stringify(await exportPublicJwk(pair.publicKey));
        if (!mine.some((k) => k.publicKeyJwk === jwk)) await messagesPlusApi.registerKey(jwk);
        const theirs = await messagesPlusApi.userKeys(otherUserId);
        if (theirs.length === 0) throw new Error("The other person hasn't enabled encryption on any device yet.");
        const their = await importPublicJwk(JSON.parse(theirs[0].publicKeyJwk));
        const k = await deriveConversationKey(pair.privateKey, their);
        if (!cancelled) setKey(k);
      } catch (e: any) {
        if (!cancelled) setError(e?.message ?? "Couldn't set up encryption.");
      }
    })();
    return () => { cancelled = true; };
  }, [enabled, myId, otherUserId]);

  return {
    ready: !!key,
    error,
    encrypt: (text: string) => (key ? encryptText(key, text) : Promise.reject(new Error("E2EE key not ready"))),
    decrypt: async (content?: string | null) => {
      if (!isE2eeEnvelope(content)) return content ?? "";
      if (!key) return "🔒 Encrypted message";
      try { return await decryptText(key, content!); } catch { return "🔒 Can't decrypt on this device"; }
    }
  };
}
