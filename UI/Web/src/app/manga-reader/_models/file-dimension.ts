export interface FileDimension {
    pageNumber: number;
    width: number;
    height: number;
}

export type DimensionMap = {[key: number]: 'W'|'S'};