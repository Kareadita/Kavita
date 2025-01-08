export interface UserTokenInfo {
  userId: number;
  username: string;
  isAniListTokenSet: boolean;
  aniListValidUntilUtc: string;
  isAniListTokenValid: boolean;
  isMalTokenSet: boolean;
}
