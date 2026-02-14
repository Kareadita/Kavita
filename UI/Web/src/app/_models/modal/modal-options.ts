import {NgbModalOptions} from "@ng-bootstrap/ng-bootstrap";

export const DefaultModalOptions: Partial<NgbModalOptions> = {
  scrollable: true,
  size: 'xl',
  fullscreen: 'xl',
};

/** Any Edit Entity modal should use this */
export function editModal(): Partial<NgbModalOptions> {
  return {...DefaultModalOptions, size: 'xl', fullscreen: 'xl'};
}

export function mediumModal(): Partial<NgbModalOptions> {
  return {...DefaultModalOptions, size: 'md', fullscreen: 'sm'};
}

export function confirmModal(): Partial<NgbModalOptions> {
  return {...DefaultModalOptions, size: 'lg', fullscreen: 'md'};
}

/** Any Add-To flow (Add to Reading List/Collection/etc) modal should use this. A thinned out modal.  */
export function addToModal(): Partial<NgbModalOptions> {
  return {...DefaultModalOptions, size: 'md', fullscreen: 'sm'};
}

/** Non-dismissible — for refresh-required modals only */
export function versionRefreshModal(): Partial<NgbModalOptions> {
  return {
    ...DefaultModalOptions,
    size: 'lg',
    keyboard: false,
    scrollable: true,
    backdrop: 'static'
  };
}

/** Dismissible — for update-available and out-of-date modals */
export function versionNotifyModal(): Partial<NgbModalOptions> {
  return {
    ...DefaultModalOptions,
    size: 'lg',
    scrollable: true,
  };
}
