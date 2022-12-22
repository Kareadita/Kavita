import { StatCount } from "./stat-count";

export interface UserReadStatistics {
    totalPagesRead: number;
    timeSpentReading: number;
    chaptersRead: number;
    lastActive: string;
    avgHoursPerWeekSpentReading: number;
    percentReadPerLibrary: Array<StatCount<number>>;
}