export interface ServerInfoSlim {
  kavitaVersion: string;
  installId: string;
  isDocker: boolean;
  firstInstallVersion?: string;
  firstInstallDate?: string;
}
