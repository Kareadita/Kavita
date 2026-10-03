export enum ActivityEndReason {
  /**
   * The server no longer lists the job, it ended while this client was not listening
   */
  Away = 'away',
  Restart = 'restart',
}
