import {MessageEventCode} from '../events/core/message-event-code';

export const DelayedScanCodes: (MessageEventCode | null)[] = [
  MessageEventCode.ScanLibrariesDelayed,
  MessageEventCode.ScanLibraryDelayed,
  MessageEventCode.ScanSeriesDelayed,
];
