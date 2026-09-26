// Minimal JWT sign/verify built on WebCrypto only - no dependency needed
// for something this small, and Workers support crypto.subtle natively.

function base64url(bytes: Uint8Array): string {
  let bin = "";
  for (const b of bytes) bin += String.fromCharCode(b);
  return btoa(bin).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}

function base64urlToBytes(b64url: string): Uint8Array {
  const padded = b64url + "=".repeat((4 - (b64url.length % 4)) % 4);
  const bin = atob(padded.replace(/-/g, "+").replace(/_/g, "/"));
  const bytes = new Uint8Array(bin.length);
  for (let i = 0; i < bin.length; i++) bytes[i] = bin.charCodeAt(i);
  return bytes;
}

function encodeJson(value: unknown): string {
  return base64url(new TextEncoder().encode(JSON.stringify(value)));
}

let cachedPrivateKey: CryptoKey | null = null;
let cachedPublicKey: CryptoKey | null = null;

async function importPrivateKey(jwk: JsonWebKey): Promise<CryptoKey> {
  if (cachedPrivateKey) return cachedPrivateKey;
  cachedPrivateKey = await crypto.subtle.importKey(
    "jwk",
    jwk,
    { name: "RSASSA-PKCS1-v1_5", hash: "SHA-256" },
    false,
    ["sign"],
  );
  return cachedPrivateKey;
}

async function importPublicKey(jwk: JsonWebKey): Promise<CryptoKey> {
  if (cachedPublicKey) return cachedPublicKey;
  cachedPublicKey = await crypto.subtle.importKey(
    "jwk",
    jwk,
    { name: "RSASSA-PKCS1-v1_5", hash: "SHA-256" },
    false,
    ["verify"],
  );
  return cachedPublicKey;
}

export async function signJwt(
  payload: Record<string, unknown>,
  privateJwk: JsonWebKey,
  kid: string,
): Promise<string> {
  const header = { alg: "RS256", typ: "JWT", kid };
  const signingInput = `${encodeJson(header)}.${encodeJson(payload)}`;
  const key = await importPrivateKey(privateJwk);
  const sig = await crypto.subtle.sign(
    "RSASSA-PKCS1-v1_5",
    key,
    new TextEncoder().encode(signingInput),
  );
  return `${signingInput}.${base64url(new Uint8Array(sig))}`;
}

/**
 * Verifies one of our own tokens (the short-lived "code", not an Access
 * JWT - see access.ts for that). Returns the payload on success, null on
 * any failure (bad signature, expired, malformed).
 */
export async function verifyOwnJwt<T = Record<string, unknown>>(
  jwt: string,
  publicJwk: JsonWebKey,
): Promise<T | null> {
  try {
    const parts = jwt.split(".");
    if (parts.length !== 3) return null;
    const [headerB64, payloadB64, sigB64] = parts;

    const key = await importPublicKey(publicJwk);
    const signedData = new TextEncoder().encode(`${headerB64}.${payloadB64}`);
    const signature = base64urlToBytes(sigB64);
    const valid = await crypto.subtle.verify("RSASSA-PKCS1-v1_5", key, signature, signedData);
    if (!valid) return null;

    const payload = JSON.parse(new TextDecoder().decode(base64urlToBytes(payloadB64))) as T & {
      exp?: number;
    };
    if (!payload.exp || payload.exp * 1000 < Date.now()) return null;

    return payload;
  } catch {
    return null;
  }
}
