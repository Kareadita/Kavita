import { Volume } from './volume';

export interface Series {
    id: number;
    name: string;
    originalName: string;
    sortName: string;
    summary: string;
    volumes: Volume[];
}
