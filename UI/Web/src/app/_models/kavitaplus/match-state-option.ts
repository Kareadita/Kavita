export enum MatchStateOption {
  All = 0,
  Matched = 1,
  NotMatched = 2,
  Error = 3,
  DontMatch = 4
}

export const allMatchStates = [
  MatchStateOption.Matched, MatchStateOption.NotMatched, MatchStateOption.Error, MatchStateOption.DontMatch
];
