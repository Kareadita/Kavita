export enum ActivityEndReason {
  /**
   * The server no longer lists the job, it ended while this client was not listening
   */
  Away = 'away',
  Restart = 'restart',
  /**
   * The server lists the job as no longer running, but not every step sent ended
   */
  Failed = 'failed',
}
