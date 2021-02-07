import { PageSplitOption } from './page-split-option';
import { ReadingDirection } from './reading-direction';
import { ScalingOption } from './scaling-option';

export interface Preferences {
    readingDirection: ReadingDirection;
    scalingOption: ScalingOption;
    pageSplitOption: PageSplitOption;
    hideReadOnDetails: boolean;
}
