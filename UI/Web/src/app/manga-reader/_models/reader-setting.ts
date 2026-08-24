import {LayoutMode} from "./layout-mode";
import {FITTING_OPTION, PAGING_DIRECTION} from "./reader-enums";
import {ReaderMode} from "../../_models/preferences/reader-mode";
import {PageSplitOption} from "../../_models/preferences/page-split-option";

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
