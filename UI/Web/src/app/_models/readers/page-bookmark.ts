import {Series} from "../series";

export interface PageBookmark {
    id: number;
    page: number;
    seriesId: number;
    volumeId: number;
    chapterId: number;
    series: Series;
}
