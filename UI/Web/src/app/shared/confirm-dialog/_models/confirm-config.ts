import { ConfirmButton } from './confirm-button';

export class ConfirmConfig {
    _type: 'confirm' | 'alert' | 'info' = 'confirm';
    header: string = 'Confirm';
    content: string = '';
    buttons: Array<ConfirmButton> = [];
    /**
     * If the close button shouldn't be rendered
     */
    disableEscape: boolean = false;
}
