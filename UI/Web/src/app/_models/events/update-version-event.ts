export interface UpdateVersionEvent {
    currentVersion: string;
    updateVersion: string;
    updateBody: string;
    updateTitle: string;
    updateUrl: string;
    isDocker: boolean;
    publishDate: string;
}