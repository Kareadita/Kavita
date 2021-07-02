import { Injectable } from '@angular/core';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { ConfirmDialogComponent } from './confirm-dialog/confirm-dialog.component';
import { ConfirmConfig } from './confirm-dialog/_models/confirm-config';

@Injectable({
  providedIn: 'root'
})
export class ConfirmService {

  defaultConfirm = new ConfirmConfig();
  defaultAlert = new ConfirmConfig();

  constructor(private modalService: NgbModal) {
    this.defaultConfirm.buttons.push({text: 'Cancel', type: 'secondary'});
    this.defaultConfirm.buttons.push({text: 'Confirm', type: 'primary'});

    this.defaultAlert._type = 'alert';
    this.defaultAlert.header = 'Alert';
    this.defaultAlert.buttons.push({text: 'Ok', type: 'primary'});

  }

  public async confirm(content?: string, config?: ConfirmConfig): Promise<boolean> {

    return new Promise((resolve, reject) => {
      if (content === undefined && config === undefined) {
        console.error('Confirm must have either text or a config object passed');
        return reject(false);
      }

      if (content !== undefined && config === undefined) {
        config = this.defaultConfirm;
        config.content = content;
      }

      const modalRef = this.modalService.open(ConfirmDialogComponent);
      modalRef.componentInstance.config = config;
      modalRef.closed.subscribe(result => {
        return resolve(result);
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
        config = this.defaultConfirm;
        config.content = content;
      }

      const modalRef = this.modalService.open(ConfirmDialogComponent);
      modalRef.componentInstance.config = config;
      modalRef.closed.subscribe(result => {
        return resolve(result);
      });
    })
  }
}
