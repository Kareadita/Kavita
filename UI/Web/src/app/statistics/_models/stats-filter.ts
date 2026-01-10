
export type StatsFilter = {
  timeFilter: {
    startDate: Date | null,
    endDate: Date | null,
  },
  libraries: number[],
  timezone: string | null,
}

