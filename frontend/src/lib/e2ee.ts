/**
 * Opt-in end-to-end encryption helpers (WebCrypto): ECDH P-256 key agreement + AES-GCM.
 * Scope: single device. The private key lives in this browser's IndexedDB (non-extractable);
 * there is no multi-device sync or key backup. The server only stores public keys and
 * ciphertext envelopes ("e2ee:v1:<base64url(json)>") and never decrypts.
 */

export const E2EE_PREFIX = "e2ee:v1:";
const DB_NAME = "pawzaroo-e2ee";
const STORE = "keys";

const subtle = () => globalThis.crypto.subtle;

function b64u(buf: ArrayBuffer | Uint8Array): string {
  const bytes = buf instanceof Uint8Array ? buf : new Uint8Array(buf);
  let s = "";
  bytes.forEach((b) => (s += String.fromCharCode(b)));
  return btoa(s).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}

function fromB64u(s: string): Uint8Array {
  const pad = "=".repeat((4 - (s.length % 4)) % 4);
  const bin = atob(s.replace(/-/g, "+").replace(/_/g, "/") + pad);
  return Uint8Array.from(bin, (c) => c.charCodeAt(0));
}

export const isE2eeEnvelope = (content?: string | null) => !!content && content.startsWith(E2EE_PREFIX);

export async function generateKeyPair(): Promise<CryptoKeyPair> {
  // Private key non-extractable; public key is always exportable.
  return subtle().generateKey({ name: "ECDH", namedCurve: "P-256" }, false, ["deriveKey"]);
}

export async function exportPublicJwk(pub: CryptoKey): Promise<JsonWebKey> {
  return subtle().exportKey("jwk", pub);
}

export async function importPublicJwk(jwk: JsonWebKey): Promise<CryptoKey> {
  const { d: _d, ...pub } = jwk as JsonWebKey & { d?: string };
  return subtle().importKey("jwk", pub, { name: "ECDH", namedCurve: "P-256" }, true, []);
}

export async function deriveConversationKey(myPrivate: CryptoKey, theirPublic: CryptoKey): Promise<CryptoKey> {
  return subtle().deriveKey({ name: "ECDH", public: theirPublic }, myPrivate, { name: "AES-GCM", length: 256 }, false, ["encrypt", "decrypt"]);
}

export async function encryptText(key: CryptoKey, plaintext: string): Promise<string> {
  const iv = globalThis.crypto.getRandomValues(new Uint8Array(12));
  const ct = await subtle().encrypt({ name: "AES-GCM", iv }, key, new TextEncoder().encode(plaintext));
  return E2EE_PREFIX + b64u(new TextEncoder().encode(JSON.stringify({ v: 1, iv: b64u(iv), ct: b64u(ct) })));
}

export async function decryptText(key: CryptoKey, envelope: string): Promise<string> {
  if (!isE2eeEnvelope(envelope)) throw new Error("Not an E2EE envelope");
  const { iv, ct } = JSON.parse(new TextDecoder().decode(fromB64u(envelope.slice(E2EE_PREFIX.length))));
  const pt = await subtle().decrypt({ name: "AES-GCM", iv: fromB64u(iv) as unknown as BufferSource }, key, fromB64u(ct) as unknown as BufferSource);
  return new TextDecoder().decode(pt);
}

// ---- IndexedDB key storage (best effort) ----

function openDb(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const req = indexedDB.open(DB_NAME, 1);
    req.onupgradeneeded = () => req.result.createObjectStore(STORE);
    req.onsuccess = () => resolve(req.result);
    req.onerror = () => reject(req.error);
  });
}

export async function saveKeyPair(userId: string, pair: CryptoKeyPair): Promise<void> {
  const db = await openDb();
  await new Promise<void>((resolve, reject) => {
    const tx = db.transaction(STORE, "readwrite");
    tx.objectStore(STORE).put(pair, userId);
    tx.oncomplete = () => resolve();
    tx.onerror = () => reject(tx.error);
  });
}

export async function loadKeyPair(userId: string): Promise<CryptoKeyPair | null> {
  const db = await openDb();
  return new Promise((resolve, reject) => {
    const req = db.transaction(STORE, "readonly").objectStore(STORE).get(userId);
    req.onsuccess = () => resolve((req.result as CryptoKeyPair) ?? null);
    req.onerror = () => reject(req.error);
  });
}

export async function getOrCreateKeyPair(userId: string): Promise<{ pair: CryptoKeyPair; created: boolean }> {
  const existing = await loadKeyPair(userId);
  if (existing) return { pair: existing, created: false };
  const pair = await generateKeyPair();
  await saveKeyPair(userId, pair);
  return { pair, created: true };
}
