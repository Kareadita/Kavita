import { PageSplitOption } from "src/app/_models/preferences/page-split-option";
import { ReaderMode } from "src/app/_models/preferences/reader-mode";
import { LayoutMode } from "./layout-mode";
import { FITTING_OPTION, PAGING_DIRECTION } from "./reader-enums";

export interface ReaderSetting {
    pageSplit: PageSplitOption;
    fitting: FITTING_OPTION;
    widthSlider: string;
    layoutMode: LayoutMode;
    darkness: number;
    pagingDirection: PAGING_DIRECTION;
    readerMode: ReaderMode;
    emulateBook: boolean;
}
