export enum ScrobbleReadStatus {
  Ignore = 0,
  WantToRead = 1,
  Read = 2,
  UnRead = 3,
  Dropped = 4,
  OnHold = 5,
}

export const ScrobbleReadStatuses = [ScrobbleReadStatus.Ignore, ScrobbleReadStatus.WantToRead, ScrobbleReadStatus.Read,
  ScrobbleReadStatus.UnRead, ScrobbleReadStatus.Dropped, ScrobbleReadStatus.OnHold];
