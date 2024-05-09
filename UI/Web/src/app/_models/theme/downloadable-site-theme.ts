export interface DownloadableSiteTheme {
  name: string;
  cssUrl: string;
  previewUrls: Array<string>;
  author: string;
  isCompatible: boolean;
  lastCompatibleVersion: string;
  alreadyDownloaded: boolean;
  description: string;
}
