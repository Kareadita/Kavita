import {StatCount} from "./stat-count";
import {User} from "../../_models/user/user";
import {Series} from "../../_models/series";
import {Library} from "../../_models/library/library";

export interface ServerStatistics {
    chapterCount: number;
    volumeCount: number;
    seriesCount: number;
    totalFiles: number;
    totalSize: number;
    totalGenres: number;
    totalTags: number;
    totalPeople: number;
    totalReadingTime: number;
    mostActiveUsers: Array<StatCount<User>>;
    mostActiveLibraries: Array<StatCount<Library>>;
    mostReadSeries: Array<StatCount<Series>>;
    recentlyRead: Array<Series>;
}
