import { describe, expect, it } from "vitest";
import { E2EE_PREFIX, decryptText, deriveConversationKey, encryptText, exportPublicJwk, generateKeyPair, importPublicJwk, isE2eeEnvelope } from "./e2ee";

describe("e2ee helpers", () => {
  it("round-trips between two parties via ECDH + AES-GCM", async () => {
    const alice = await generateKeyPair();
    const bob = await generateKeyPair();
    const aliceKey = await deriveConversationKey(alice.privateKey, await importPublicJwk(await exportPublicJwk(bob.publicKey)));
    const bobKey = await deriveConversationKey(bob.privateKey, await importPublicJwk(await exportPublicJwk(alice.publicKey)));
    const env = await encryptText(aliceKey, "vaccine due friday");
    expect(isE2eeEnvelope(env)).toBe(true);
    expect(env.startsWith(E2EE_PREFIX)).toBe(true);
    expect(env).not.toContain("vaccine");
    expect(await decryptText(bobKey, env)).toBe("vaccine due friday");
  });

  it("fails with the wrong key and for plaintext", async () => {
    const a = await generateKeyPair();
    const b = await generateKeyPair();
    const c = await generateKeyPair();
    const kab = await deriveConversationKey(a.privateKey, b.publicKey);
    const kac = await deriveConversationKey(a.privateKey, c.publicKey);
    const env = await encryptText(kab, "secret");
    await expect(decryptText(kac, env)).rejects.toBeTruthy();
    await expect(decryptText(kab, "plain")).rejects.toBeTruthy();
  });

  it("never exports a private component in the public JWK", async () => {
    const p = await generateKeyPair();
    expect((await exportPublicJwk(p.publicKey)).d).toBeUndefined();
  });
});
