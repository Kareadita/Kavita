import {Component, inject, OnInit} from '@angular/core';
import {NgbActiveModal} from '@ng-bootstrap/ng-bootstrap';
import {ConfirmButton} from './_models/confirm-button';
import {ConfirmConfig} from './_models/confirm-config';
import {SafeHtmlPipe} from "../../_pipes/safe-html.pipe";
import {TranslocoDirective} from "@jsverse/transloco";
import {ConfirmTranslatePipe} from "../../_pipes/confirm-translate.pipe";
import {FormControl, FormGroup, ReactiveFormsModule} from "@angular/forms";

@Component({
    selector: 'app-confirm-dialog',
  imports: [SafeHtmlPipe, TranslocoDirective, ConfirmTranslatePipe, ReactiveFormsModule],
    templateUrl: './confirm-dialog.component.html',
    styleUrls: ['./confirm-dialog.component.scss'],
    host: {
      '[class]': 'hostClass',
    }
})
export class ConfirmDialogComponent implements OnInit {

  protected readonly modal = inject(NgbActiveModal);

  config!: ConfirmConfig;
  formGroup = new FormGroup({
    'prompt': new FormControl('', []),
  })

  get hostClass(): string {
    return this.config ? `confirm-${this.config._type}` : '';
  }

  get typeIcon(): string {
    switch (this.config?._type) {
      case 'alert': return 'fa-triangle-exclamation';
      case 'info': return 'fa-circle-info';
      case 'prompt': return 'fa-pen-to-square';
      default: return 'fa-circle-question';
    }
  }

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
    const isConfirming = button.type !== 'secondary';
    if (this.config._type === 'prompt') {
      this.modal.close(isConfirming ? this.formGroup.get('prompt')?.value : '');
      return;
    }
    this.modal.close(isConfirming);
  }

  close() {
    this.modal.close(false);
  }

}
