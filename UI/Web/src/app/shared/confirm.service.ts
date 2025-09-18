import { Injectable, inject } from '@angular/core';
import {NgbModal} from '@ng-bootstrap/ng-bootstrap';
import {take} from 'rxjs/operators';
import {ConfirmDialogComponent} from './confirm-dialog/confirm-dialog.component';
import {ConfirmConfig} from './confirm-dialog/_models/confirm-config';


@Injectable({
  providedIn: 'root'
})
export class ConfirmService {
  private modalService = inject(NgbModal);


  defaultConfirm = new ConfirmConfig();
  defaultAlert = new ConfirmConfig();
  defaultInfo = new ConfirmConfig();
  defaultPrompt = new ConfirmConfig();

  constructor() {
    this.defaultConfirm.buttons = [
      {text: 'confirm.cancel', type: 'secondary'},
      {text: 'confirm.confirm', type: 'primary'},
    ];

    this.defaultAlert._type = 'alert';
    this.defaultAlert.header = 'confirm.alert';
    this.defaultAlert.buttons = [
      {text: 'confirm.ok', type: 'primary'}
    ];

    this.defaultInfo.buttons = [
      {text: 'confirm.ok', type: 'primary'}
    ];
    this.defaultInfo.header = 'confirm.info';
    this.defaultInfo._type = 'info';

    this.defaultPrompt.buttons = [
      {text: 'confirm.cancel', type: 'secondary'},
      {text: 'confirm.ok', type: 'primary'}
    ];
    this.defaultPrompt.header = 'confirm.prompt';
    this.defaultPrompt._type = 'prompt';
  }

  public async confirm(content?: string, config?: ConfirmConfig): Promise<boolean> {

    return new Promise((resolve, reject) => {
      if (content === undefined && config === undefined) {
        console.error('Confirm must have either text or a config object passed');
        return reject(false);
      }

      if (content !== undefined && config === undefined) {
        config = this.defaultConfirm;
        config.header = 'confirm.confirm';
        config.content = content;
      }
      if (content !== undefined && content !== '' && config!.content === '') {
        config!.content = content;
      }

      const modalRef = this.modalService.open(ConfirmDialogComponent);
      modalRef.componentInstance.config = config;
      modalRef.closed.pipe(take(1)).subscribe(result => {
        return resolve(result);
      });
      modalRef.dismissed.pipe(take(1)).subscribe(() => {
        return resolve(false);
      });
    });

  }

  public async info(content: string, header?: string, config?: ConfirmConfig): Promise<boolean> {
    return new Promise((resolve, reject) => {
      if (content === undefined && config === undefined) {
        console.error('Alert must have either text or a config object passed');
        return reject(false);
      }

      if (content !== undefined && config === undefined) {
        config = this.defaultInfo;
        config.content = content;

        if (header != undefined) {
          config.header = header;
        }
      }

      const modalRef = this.modalService.open(ConfirmDialogComponent, {size: "lg", fullscreen: "md"});
      modalRef.componentInstance.config = config;
      modalRef.closed.pipe(take(1)).subscribe(result => {
        return resolve(result);
      });
      modalRef.dismissed.pipe(take(1)).subscribe(() => {
        return resolve(false);
      });
    });
  }

  public async alert(content?: string, config?: ConfirmConfig): Promise<boolean> {
    return new Promise((resolve, reject) => {
      if (content === undefined && config === undefined) {
        console.error('Alert must have either text or a config object passed');
        return reject(false);
      }

      if (content !== undefined && config === undefined) {
        config = this.defaultAlert;
        config.header = 'confirm.alert';
        config.content = content;
      }

      const modalRef = this.modalService.open(ConfirmDialogComponent, {size: "lg", fullscreen: "md"});
      modalRef.componentInstance.config = config;
      modalRef.closed.pipe(take(1)).subscribe(result => {
        return resolve(result);
      });
      modalRef.dismissed.pipe(take(1)).subscribe(() => {
        return resolve(false);
      });
    });
  }

  public async prompt(title: string | undefined = undefined, config: ConfirmConfig | undefined = undefined): Promise<string> {

    return new Promise((resolve, reject) => {
      if (title === undefined && config === undefined) {
        console.error('Confirm must have either text or a config object passed');
        return reject(false);
      }

      if (title !== undefined && config === undefined) {
        config = this.defaultPrompt;
        config.header = title;
      }
      if (title !== undefined && title !== '' && config!.header === '') {
        config!.header = title;
      }

      const modalRef = this.modalService.open(ConfirmDialogComponent);
      modalRef.componentInstance.config = config;
      modalRef.closed.pipe(take(1)).subscribe(result => {
        return resolve(result);
      });
      modalRef.dismissed.pipe(take(1)).subscribe(() => {
        return resolve('');
      });
    });

  }
}
