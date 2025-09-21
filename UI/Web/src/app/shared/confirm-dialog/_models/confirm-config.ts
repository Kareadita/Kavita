import {ConfirmButton} from './confirm-button';

export class ConfirmConfig {
    _type: 'confirm' | 'alert' | 'info' | 'prompt' = 'confirm';
    header: string = 'Confirm';
    content: string = '';
    buttons: Array<ConfirmButton> = [];
    /**
     * If the close button shouldn't be rendered
     */
    disableEscape: boolean = false;
  /**
   * Enables book theme css classes to style the popup properly
   */
  bookReader?: boolean = false;
}
