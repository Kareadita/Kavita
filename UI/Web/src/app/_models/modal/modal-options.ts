import {NgbModalOptions} from "@ng-bootstrap/ng-bootstrap";

export const DefaultModalOptions: Partial<NgbModalOptions> = {
  scrollable: true,
  size: 'xl',
  fullscreen: 'xl',
};

/** Any Edit Entity modal should use this */
export function editModal(): Partial<NgbModalOptions> {
  return { ...DefaultModalOptions, size: 'xl', fullscreen: 'xl' };
}

export function mediumModal(): Partial<NgbModalOptions> {
  return { ...DefaultModalOptions, size: 'md' };
}

export function confirmModal(): Partial<NgbModalOptions> {
  return {size: 'lg', fullscreen: 'md'};
}

/** Any Edit Entity modal should use this */
export function addToModal(): Partial<NgbModalOptions> {
  return { ...DefaultModalOptions, size: 'md', fullscreen: 'sm' };
}
