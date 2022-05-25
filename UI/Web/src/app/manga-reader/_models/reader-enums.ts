export enum FITTING_OPTION {
  HEIGHT = 'full-height',
  WIDTH = 'full-width',
  ORIGINAL = 'original',
}

/**
 * How to split a page into virtual pages. Only works with LayoutMode.Single
 */
export enum SPLIT_PAGE_PART {
  NO_SPLIT = 'none',
  LEFT_PART = 'left',
  RIGHT_PART = 'right',
}

export enum PAGING_DIRECTION {
  FORWARD = 1,
  BACKWARDS = -1,
}
