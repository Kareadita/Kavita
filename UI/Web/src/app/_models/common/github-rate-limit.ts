export interface GithubRateLimit {
  remaining: number | null;
  limit: number | null;
  resetsAtUtc: string | null;
  isLow: boolean;
  isExhausted: boolean;
}
