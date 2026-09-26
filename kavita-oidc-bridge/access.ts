// Cryptographically verifies a Cloudflare Access JWT - proves a request
// actually came through Access's OTP login, rather than trusting the
// convenience `Cf-Access-Authenticated-User-Email` header on its own
// (safe only as long as this service is truly unreachable except through
// Access - a misconfigured Access app, a route left over from testing, a
// future change that reintroduces a raw *.workers.dev/*.pages.dev URL,
// all stop being caught if verification is skipped). This checks the
// actual signed assertion (`Cf-Access-Jwt-Assertion` header) against
// Cloudflare's own public keys instead.
//
// Copied verbatim from koodo-bridge's functions/lib/access.ts - that
// file's own comment says it's meant to be copied into every Iterverse
// service's own codebase the same way design-system/ is, not just the
// service that happened to build it first. Keep this in sync with the
// original if it ever changes there.

interface AccessJwk {
  kid: string;
  kty: string;
  n: string;
  e: string;
  alg?: string;
}

interface AccessJwtPayload {
  email?: string;
  aud?: string[] | string;
  exp?: number;
  iss?: string;
}

// Module-level, so a warm isolate reuses keys across requests instead of
// refetching Access's certs endpoint every single request. Cleared and
// refetched once on a kid miss, to ride out Cloudflare's own key
// rotation without needing a time-based TTL.
let cachedKeys: Map<string, CryptoKey> | null = null;
let cachedTeamDomain: string | null = null;

function base64UrlToBytes(b64url: string): Uint8Array {
  const padded = b64url + "=".repeat((4 - (b64url.length % 4)) % 4);
  const binary = atob(padded.replace(/-/g, "+").replace(/_/g, "/"));
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
  return bytes;
}

function decodeJson<T>(b64url: string): T {
  return JSON.parse(new TextDecoder().decode(base64UrlToBytes(b64url))) as T;
}

async function loadKeys(teamDomain: string): Promise<Map<string, CryptoKey>> {
  if (cachedKeys && cachedTeamDomain === teamDomain) return cachedKeys;
  const response = await fetch(`https://${teamDomain}/cdn-cgi/access/certs`);
  if (!response.ok) throw new Error(`Failed to fetch Access certs: ${response.status}`);
  const { keys } = await response.json<{ keys: AccessJwk[] }>();

  const map = new Map<string, CryptoKey>();
  for (const jwk of keys) {
    const key = await crypto.subtle.importKey(
      "jwk",
      jwk,
      { name: "RSASSA-PKCS1-v1_5", hash: "SHA-256" },
      false,
      ["verify"],
    );
    map.set(jwk.kid, key);
  }
  cachedKeys = map;
  cachedTeamDomain = teamDomain;
  return map;
}

/**
 * Verifies signature, expiry, issuer, and audience. Returns the
 * authenticated email on success, null on *any* failure (bad signature,
 * expired, wrong audience, malformed token, certs fetch failure) -
 * callers should treat null exactly like "no session" and never surface
 * which check failed, so a prober can't distinguish "expired" from
 * "wrong audience" from "not signed in at all".
 */
export async function verifyAccessJwt(jwt: string, teamDomain: string, expectedAud: string): Promise<string | null> {
  try {
    const parts = jwt.split(".");
    if (parts.length !== 3) return null;
    const [headerB64, payloadB64, sigB64] = parts;

    const header = decodeJson<{ kid?: string; alg?: string }>(headerB64);
    if (!header.kid || header.alg !== "RS256") return null;

    let keys = await loadKeys(teamDomain);
    let key = keys.get(header.kid);
    if (!key) {
      cachedKeys = null;
      keys = await loadKeys(teamDomain);
      key = keys.get(header.kid);
    }
    if (!key) return null;

    const signedData = new TextEncoder().encode(`${headerB64}.${payloadB64}`);
    const signature = base64UrlToBytes(sigB64);
    const valid = await crypto.subtle.verify("RSASSA-PKCS1-v1_5", key, signature, signedData);
    if (!valid) return null;

    const payload = decodeJson<AccessJwtPayload>(payloadB64);
    if (!payload.exp || payload.exp * 1000 < Date.now()) return null;
    if (payload.iss !== `https://${teamDomain}`) return null;

    const audList = Array.isArray(payload.aud) ? payload.aud : payload.aud ? [payload.aud] : [];
    if (!audList.includes(expectedAud)) return null;

    return payload.email ?? null;
  } catch {
    return null;
  }
}
