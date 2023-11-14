import { Library } from "src/app/_models/library/library";
import { Series } from "src/app/_models/series";
import { User } from "src/app/_models/user";
import { StatCount } from "./stat-count";

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
