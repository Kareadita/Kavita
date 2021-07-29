import { Injectable } from '@angular/core';
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { User } from '@sentry/angular';
import { ToastrService } from 'ngx-toastr';
import { env } from 'process';
import { environment } from 'src/environments/environment';
import { UpdateNotificationModalComponent } from '../shared/update-notification/update-notification-modal.component';

export enum EVENTS {
  UpdateAvailable = 'UpdateAvailable'
}

export enum METHODS {
  CheckForUpdate = 'CheckForUpdate'
}

@Injectable({
  providedIn: 'root'
})
export class MessageHubService {
  hubUrl = environment.hubUrl;
  private hubConnection!: HubConnection;

  constructor(private toatsr: ToastrService, private modalService: NgbModal) { }

  createHubConnection(user: User) {
    this.hubConnection = new HubConnectionBuilder()
      .withUrl(this.hubUrl + 'messages', {
        accessTokenFactory: () => user.token
      })
      .withAutomaticReconnect()
      .build();

    this.hubConnection
    .start()
    .catch(err => console.error(err));

    this.hubConnection.on('receiveMessage', body => {
      console.log('[Hub] Body: ', body);
      this.toatsr.info(body.version);
    });

    this.hubConnection.on(EVENTS.UpdateAvailable, resp => {
      console.log('[Hub] Body: ', resp);
      this.toatsr.info('Update available: ' + resp.body.currentVersion + ' -> ' + resp.body.updateVersion);
      const modalRef = this.modalService.open(UpdateNotificationModalComponent, { scrollable: true, size: 'lg' });
      modalRef.componentInstance.updateData = resp.body;
    });
  }

  stopHubConnection() {
    this.hubConnection.stop().catch(err => console.error(err));
  }

  sendMessage(methodName: string, body?: any) {
    return this.hubConnection.invoke(methodName, body);
  }
  
}
