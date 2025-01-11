import { Injectable } from '@angular/core';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { take } from 'rxjs/operators';
import { ConfirmDialogComponent } from './confirm-dialog/confirm-dialog.component';
import { ConfirmConfig } from './confirm-dialog/_models/confirm-config';
import {translate} from "@jsverse/transloco";
import {ConfirmButton} from "./confirm-dialog/_models/confirm-button";


@Injectable({
  providedIn: 'root'
})
export class ConfirmService {

  defaultConfirm = new ConfirmConfig();
  defaultAlert = new ConfirmConfig();
  defaultInfo = new ConfirmConfig();

  constructor(private modalService: NgbModal) {
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
}
