import { PageSplitOption } from "src/app/_models/preferences/page-split-option";
import { LayoutMode } from "./layout-mode";
import { FITTING_OPTION } from "./reader-enums";

export interface ReaderSetting {
    pageSplit: PageSplitOption;
    fitting: FITTING_OPTION;
    layoutMode: LayoutMode;
    darkness: number;
    isSplitLeftToRight: boolean;
    isWideImage: boolean;
    isNoSplit: boolean;
}