// Copied verbatim from koodo-bridge's functions/lib/roster.ts, per that
// file's own precedent (see access.ts in this directory) - shared
// entitlement-check logic, meant to be copied into every Iterverse
// service's own codebase rather than centralized. Keep in sync with the
// original if it ever changes there.
//
// One real difference from Reader's usage: `product` here is "library",
// not "reader" - this is the first non-Reader caller of this check.
// Named for the product ("Iterverse Library"), not the underlying engine
// ("Kavita"), matching how Reader's own key is "reader" and not "koodo" -
// the entitlement API shouldn't bake in an implementation detail that
// could change under the product name later.
// Confirmed required: iterverse_hub's entitlement.ts validates `product`
// against a closed allowlist/branch set, so "library" must be registered
// there (see its own "reader"/"chat" any-active-enrollment branches) or
// every login here is unconditionally denied.
export async function checkRosterEntitlement(env: Env, email: string): Promise<boolean> {
  const response = await fetch(`${env.ROSTER_API_URL}/api/entitlement/check`, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${env.ROSTER_SERVICE_KEY}`,
    },
    body: JSON.stringify({ email, product: "library" }),
  });
  if (!response.ok) {
    // Distinguishes a roster-service/auth failure (bad ROSTER_SERVICE_KEY,
    // outage, etc.) from a genuine "not entitled" - both used to collapse
    // into the same silent `false`, making a token problem indistinguishable
    // from a real lockout when reading `wrangler tail` output.
    console.error(`checkRosterEntitlement: roster API returned ${response.status} for entitlement check`);
    return false;
  }
  const data = await response.json<{ entitled?: boolean }>().catch(() => ({ entitled: false }));
  return data.entitled === true;
}
