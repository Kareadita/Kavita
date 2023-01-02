import { StatCount } from "./stat-count";

export interface UserReadStatistics {
    totalPagesRead: number;
    totalWordsRead: number;
    timeSpentReading: number;
    chaptersRead: number;
    lastActive: string;
    avgHoursPerWeekSpentReading: number;
    percentReadPerLibrary: Array<StatCount<number>>;
}