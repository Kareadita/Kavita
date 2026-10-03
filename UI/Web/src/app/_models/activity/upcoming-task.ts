export interface UpcomingTask {
  taskId: string;
  cron: string;
  nextRunUtc: string;
}
