// Base64url + WebAuthn helpers. The API speaks JSON with base64url strings; the browser wants ArrayBuffers.

export function b64urlToBuffer(s: string): ArrayBuffer {
  const pad = "=".repeat((4 - (s.length % 4)) % 4);
  const bin = atob((s + pad).replace(/-/g, "+").replace(/_/g, "/"));
  const bytes = new Uint8Array(bin.length);
  for (let i = 0; i < bin.length; i++) bytes[i] = bin.charCodeAt(i);
  return bytes.buffer;
}

export function bufferToB64url(buf: ArrayBuffer): string {
  const bytes = new Uint8Array(buf);
  let bin = "";
  for (let i = 0; i < bytes.length; i++) bin += String.fromCharCode(bytes[i]);
  return btoa(bin).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}

export const passkeysSupported = () =>
  typeof window !== "undefined" && !!window.PublicKeyCredential && !!navigator.credentials;

export interface CreationOptionsJson {
  rp: { id: string; name: string };
  user: { id: string; name: string; displayName: string };
  challenge: string;
  pubKeyCredParams: { type: "public-key"; alg: number }[];
  timeout?: number;
  excludeCredentials?: { type: "public-key"; id: string }[];
  authenticatorSelection?: AuthenticatorSelectionCriteria;
  attestation?: AttestationConveyancePreference;
}

export interface RequestOptionsJson {
  challenge: string;
  timeout?: number;
  rpId?: string;
  allowCredentials?: { type: "public-key"; id: string }[];
  userVerification?: UserVerificationRequirement;
}

export async function createPasskey(o: CreationOptionsJson) {
  const cred = (await navigator.credentials.create({
    publicKey: {
      ...o,
      challenge: b64urlToBuffer(o.challenge),
      user: { ...o.user, id: b64urlToBuffer(o.user.id) },
      excludeCredentials: (o.excludeCredentials ?? []).map((c) => ({ type: c.type, id: b64urlToBuffer(c.id) })),
    },
  })) as PublicKeyCredential | null;
  if (!cred) throw new Error("No credential was created.");
  const r = cred.response as AuthenticatorAttestationResponse;
  return {
    id: cred.id,
    response: {
      clientDataJSON: bufferToB64url(r.clientDataJSON),
      attestationObject: bufferToB64url(r.attestationObject),
    },
  };
}

export async function getPasskey(o: RequestOptionsJson) {
  const cred = (await navigator.credentials.get({
    publicKey: {
      ...o,
      challenge: b64urlToBuffer(o.challenge),
      allowCredentials: (o.allowCredentials ?? []).map((c) => ({ type: c.type, id: b64urlToBuffer(c.id) })),
    },
  })) as PublicKeyCredential | null;
  if (!cred) throw new Error("No credential was returned.");
  const r = cred.response as AuthenticatorAssertionResponse;
  return {
    id: cred.id,
    response: {
      clientDataJSON: bufferToB64url(r.clientDataJSON),
      authenticatorData: bufferToB64url(r.authenticatorData),
      signature: bufferToB64url(r.signature),
      userHandle: r.userHandle ? bufferToB64url(r.userHandle) : undefined,
    },
  };
}
