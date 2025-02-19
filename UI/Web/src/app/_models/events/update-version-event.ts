export interface UpdateVersionEvent {
  currentVersion: string;
  updateVersion: string;
  updateBody: string;
  updateTitle: string;
  updateUrl: string;
  isDocker: boolean;
  publishDate: string;
  isOnNightlyInRelease: boolean;
  isReleaseNewer: boolean;
  isReleaseEqual: boolean;

  added: Array<string>;
  removed: Array<string>;
  changed: Array<string>;
  fixed: Array<string>;
  theme: Array<string>;
  developer: Array<string>;
  api: Array<string>;
  featureRequests: Array<string>;
  /**
   * The part above the changelog part
   */
  blogPart: string;
}
