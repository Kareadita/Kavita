export interface DirectoryDto {
    name: string;
    fullPath: string;
    /**
     * This is only on the UI to disable paths
     */
    disabled: boolean;
}