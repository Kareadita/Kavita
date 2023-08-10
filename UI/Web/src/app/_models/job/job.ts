export interface Job {
    id: string;
    title: string;
    cron: string;
    lastExecutionUtc: string;
}
