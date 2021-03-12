import { Component, OnInit } from '@angular/core';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { ConfirmButton } from './_models/confirm-button';
import { ConfirmConfig } from './_models/confirm-config';

@Component({
  selector: 'app-confirm-dialog',
  templateUrl: './confirm-dialog.component.html',
  styleUrls: ['./confirm-dialog.component.scss']
})
export class ConfirmDialogComponent implements OnInit {

  config!: ConfirmConfig;

  constructor(public modal: NgbActiveModal) {}

  ngOnInit(): void {
    if (this.config) {
      this.config.buttons.sort(this._button_sort);
    }
  }

  private _button_sort(x: ConfirmButton, y: ConfirmButton) {
    const xIsSecondary = x.type === 'secondary';
    const yIsSecondary = y.type === 'secondary';
    if (xIsSecondary && !yIsSecondary) {
      return -1;
    } else if (!xIsSecondary && yIsSecondary) {
      return 1;
    }
    return 0;
  }

  clickButton(button: ConfirmButton) {
    this.modal.close(button.type === 'primary');
  }

  close() {
    this.modal.close(false);
  }

}
