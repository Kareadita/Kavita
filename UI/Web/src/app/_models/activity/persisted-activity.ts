import {ActivityRow} from './activity-row';
import {DismissedActivity} from './dismissed-activity';

export interface PersistedActivity {
  rows: ActivityRow[];
  dismissed: DismissedActivity[];
}
