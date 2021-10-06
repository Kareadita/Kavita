export interface CollectionTag {
    id: number;
    title: string;
    promoted: boolean;
    /**
     * This is used as a placeholder to store the coverImage url. The backend does not use this or send it.
     */
    coverImage: string;
    coverImageLocked: boolean;
    summary: string;
}