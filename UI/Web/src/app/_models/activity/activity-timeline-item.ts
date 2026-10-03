import {ActivityJob} from './activity-job';
import {ActivityEntry} from './activity-entry';

export interface ActivityProblemGroup {
  kind: 'group';
  id: string;
  entries: ActivityEntry[];
  libraryCount: number;
  updatedUtc: string;
}

export type ActivityTimelineItem = ActivityJob | ActivityEntry | ActivityProblemGroup;
