// A minimal OIDC provider that sits between Cloudflare Access and Kavita.
//
// Why this exists: Cloudflare Access's own OIDC SaaS-application feature
// can authenticate a user into Kavita, but has no way to hand Kavita a
// role claim derived from Access's own policies/groups (see
// docs/kavita-fork-investigation.md in the main koodo-bridge repo for the
// full research trail - the short version is that Access's per-IdP claim
// mapping only pulls from an *external* connected identity provider,
// which this account doesn't have; Access's own native policies/groups
// aren't otherwise exposed as an OIDC claim).
//
// This Worker fills that gap: it IS the OIDC provider Kavita talks to
// (its `authority`), but the actual authentication is still 100%
// Cloudflare Access - this Worker's own `/authorize` route sits behind a
// normal Access Application (OTP), the exact same pattern koodo-bridge's
// own functions/api/auth/access.ts uses for Reader's login. After Access
// has verified who someone is, this Worker does two things Access alone
// can't: (1) checks the Iterverse roster service for an active
// enrollment - same `checkRosterEntitlement` rule Reader's own
// access.ts enforces, via roster.ts copied verbatim from there - since
// Access authenticating someone only proves they control an email
// address, not that they're a real enrolled BTECH person; and (2) looks
// up whether they're on the same "Instructor Dashboard Users" admin list
// Iterverse already uses elsewhere, putting that into the token as a
// role claim Kavita understands out of the box (its default RolesClaim).
//
// Known limitation, deliberate for now: ADMIN_EMAILS (below) is a
// separate, manually-maintained copy of the "Instructor Dashboard Users"
// Access policy's email list, not a live link to it - Access's own API
// could be queried at request time instead, but that's a real
// dependency (a scoped Cloudflare API token, an extra network call in
// the auth path, cache invalidation) that wasn't justified for a spike.
// If this becomes real infrastructure, that's the first thing to fix.

import { verifyAccessJwt } from "../access";
import { checkRosterEntitlement } from "../roster";
import { signJwt, verifyOwnJwt } from "./jwt";

export interface Env {
  /** Cloudflare Access team domain, e.g. dawn-mountain-9c54.cloudflareaccess.com */
  ACCESS_TEAM_DOMAIN: string;
  /** AUD tag of the Access Application gating this Worker's own /authorize route */
  ACCESS_AUD: string;
  /** This Worker's own public URL, e.g. https://kavita-oidc-bridge.<subdomain>.workers.dev */
  ISSUER: string;
  /** The client_id Kavita's oidcConfig is configured with */
  CLIENT_ID: string;
  /** The client_secret Kavita's oidcConfig is configured with (Worker secret) */
  CLIENT_SECRET: string;
  /** The exact redirect_uri Kavita will send - e.g. https://reader-test.iterverse.net/signin-oidc */
  ALLOWED_REDIRECT_URI: string;
  /** kid for the signing key below */
  SIGNING_KID: string;
  /** JSON-stringified private JWK (Worker secret) */
  SIGNING_KEY_PRIVATE_JWK: string;
  /** JSON-stringified public JWK - not sensitive, but kept as a var for symmetry */
  SIGNING_KEY_PUBLIC_JWK: string;
  /** JSON-stringified array of admin emails (Worker secret - see note above) */
  ADMIN_EMAILS: string;
  /** Iterverse roster service base URL, e.g. https://roster-api.iterverse.net */
  ROSTER_API_URL: string;
  /** Bearer key shared with the roster service's own SERVICE_KEY (Worker secret) */
  ROSTER_SERVICE_KEY: string;
}

const ROLES_CLAIM = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";
const CODE_TTL_SECONDS = 60;
const ID_TOKEN_TTL_SECONDS = 3600;

function json(data: unknown, status = 200): Response {
  return new Response(JSON.stringify(data), {
    status,
    headers: { "content-type": "application/json" },
  });
}

function safeEqual(a: string, b: string): boolean {
  if (a.length !== b.length) return false;
  let diff = 0;
  for (let i = 0; i < a.length; i++) diff |= a.charCodeAt(i) ^ b.charCodeAt(i);
  return diff === 0;
}

function isAdmin(email: string, adminEmailsJson: string): boolean {
  try {
    const admins = JSON.parse(adminEmailsJson) as string[];
    return admins.some((a) => a.toLowerCase() === email.toLowerCase());
  } catch {
    return false;
  }
}

async function handleDiscovery(env: Env): Promise<Response> {
  return json({
    issuer: env.ISSUER,
    authorization_endpoint: `${env.ISSUER}/authorize`,
    token_endpoint: `${env.ISSUER}/token`,
    jwks_uri: `${env.ISSUER}/jwks`,
    response_types_supported: ["code"],
    subject_types_supported: ["public"],
    id_token_signing_alg_values_supported: ["RS256"],
    scopes_supported: ["openid", "email", "profile"],
    claims_supported: ["sub", "email", "email_verified", "name", "preferred_username", ROLES_CLAIM],
    token_endpoint_auth_methods_supported: ["client_secret_post"],
    grant_types_supported: ["authorization_code"],
  });
}

async function handleJwks(env: Env): Promise<Response> {
  const publicJwk = JSON.parse(env.SIGNING_KEY_PUBLIC_JWK);
  return json({ keys: [publicJwk] });
}

async function handleAuthorize(request: Request, env: Env): Promise<Response> {
  const url = new URL(request.url);
  const responseType = url.searchParams.get("response_type");
  const clientId = url.searchParams.get("client_id");
  const redirectUri = url.searchParams.get("redirect_uri");
  const state = url.searchParams.get("state") ?? "";
  const nonce = url.searchParams.get("nonce") ?? "";

  if (responseType !== "code" || clientId !== env.CLIENT_ID || redirectUri !== env.ALLOWED_REDIRECT_URI) {
    return json({ error: "invalid_request" }, 400);
  }

  // Defense in depth: this route is meant to sit entirely behind a
  // Cloudflare Access Application, but never trust that alone - verify
  // the actual signed assertion the same way access.ts does for Reader.
  const jwt = request.headers.get("Cf-Access-Jwt-Assertion");
  const email = jwt ? await verifyAccessJwt(jwt, env.ACCESS_TEAM_DOMAIN, env.ACCESS_AUD) : null;
  if (!email) {
    return json({ error: "access_denied", error_description: "No verified Access session" }, 403);
  }

  // Access authenticating someone only proves they control that email
  // address - it says nothing about whether they're a real, currently
  // enrolled BTECH person. Mirrors Reader's own access.ts: reject before
  // ever minting a code, same as Reader rejects before creating a session.
  const entitled = await checkRosterEntitlement(env, email);
  if (!entitled) {
    const redirect = new URL(redirectUri);
    redirect.searchParams.set("error", "access_denied");
    redirect.searchParams.set("error_description", "not_entitled");
    redirect.searchParams.set("state", state);
    return Response.redirect(redirect.toString(), 302);
  }

  const roles = isAdmin(email, env.ADMIN_EMAILS) ? ["Admin", "Login"] : ["Login"];

  const code = await signJwt(
    {
      email,
      roles,
      nonce,
      aud: clientId,
      exp: Math.floor(Date.now() / 1000) + CODE_TTL_SECONDS,
    },
    JSON.parse(env.SIGNING_KEY_PRIVATE_JWK),
    env.SIGNING_KID,
  );

  const redirect = new URL(redirectUri);
  redirect.searchParams.set("code", code);
  redirect.searchParams.set("state", state);
  return Response.redirect(redirect.toString(), 302);
}

async function handleToken(request: Request, env: Env): Promise<Response> {
  const form = await request.formData();
  const grantType = form.get("grant_type");
  const code = form.get("code");
  const redirectUri = form.get("redirect_uri");
  const clientId = form.get("client_id");
  const clientSecret = form.get("client_secret");

  if (
    grantType !== "authorization_code" ||
    typeof code !== "string" ||
    redirectUri !== env.ALLOWED_REDIRECT_URI ||
    typeof clientId !== "string" ||
    typeof clientSecret !== "string" ||
    !safeEqual(clientId, env.CLIENT_ID) ||
    !safeEqual(clientSecret, env.CLIENT_SECRET)
  ) {
    return json({ error: "invalid_grant" }, 400);
  }

  const codePayload = await verifyOwnJwt<{ email: string; roles: string[]; aud: string; nonce?: string }>(
    code,
    JSON.parse(env.SIGNING_KEY_PUBLIC_JWK),
  );
  if (!codePayload || codePayload.aud !== clientId) {
    return json({ error: "invalid_grant", error_description: "Code invalid or expired" }, 400);
  }

  const now = Math.floor(Date.now() / 1000);
  const idToken = await signJwt(
    {
      iss: env.ISSUER,
      aud: clientId,
      sub: codePayload.email,
      email: codePayload.email,
      email_verified: true,
      preferred_username: codePayload.email,
      name: codePayload.email,
      nonce: codePayload.nonce || undefined,
      [ROLES_CLAIM]: codePayload.roles,
      iat: now,
      exp: now + ID_TOKEN_TTL_SECONDS,
    },
    JSON.parse(env.SIGNING_KEY_PRIVATE_JWK),
    env.SIGNING_KID,
  );

  return json({
    access_token: idToken,
    token_type: "Bearer",
    expires_in: ID_TOKEN_TTL_SECONDS,
    id_token: idToken,
  });
}

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    const url = new URL(request.url);

    if (request.method === "GET" && url.pathname === "/.well-known/openid-configuration") {
      return handleDiscovery(env);
    }
    if (request.method === "GET" && url.pathname === "/jwks") {
      return handleJwks(env);
    }
    if (request.method === "GET" && url.pathname === "/authorize") {
      return handleAuthorize(request, env);
    }
    if (request.method === "POST" && url.pathname === "/token") {
      return handleToken(request, env);
    }

    return new Response("Not found", { status: 404 });
  },
};
