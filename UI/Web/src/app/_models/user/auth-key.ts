export interface AuthKey {
  id: number;
  key: string;
  name: string;
  createdAtUtc: string;
  expiresAtUtc: string;
  lastAccessedAtUtc: string;
  provider: AuthKeyProvider;
}

export enum AuthKeyProvider {
  User = 0,
  System = 1
}

export const ImageOnlyName = 'image-only';
export const OpdsName = 'opds';

