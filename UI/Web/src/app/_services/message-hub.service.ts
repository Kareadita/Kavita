import { Injectable } from '@angular/core';
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import { NgbModal, NgbModalRef } from '@ng-bootstrap/ng-bootstrap';
import { User } from '@sentry/angular';
import { BehaviorSubject, ReplaySubject } from 'rxjs';
import { environment } from 'src/environments/environment';
import { UpdateNotificationModalComponent } from '../shared/update-notification/update-notification-modal.component';

export enum EVENTS {
  UpdateAvailable = 'UpdateAvailable'
}

export interface Message<T> {
  event: EVENTS;
  payload: T;
}

@Injectable({
  providedIn: 'root'
})
export class MessageHubService {
  hubUrl = environment.hubUrl;
  private hubConnection!: HubConnection;
  private updateNotificationModalRef: NgbModalRef | null = null;

  private messagesSource = new ReplaySubject<Message<any>>(1);
  public messages$ = this.messagesSource.asObservable();

  constructor(private modalService: NgbModal) { }

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
      //console.log('[Hub] Body: ', body);
    });

    this.hubConnection.on(EVENTS.UpdateAvailable, resp => {
      this.messagesSource.next({
        event: EVENTS.UpdateAvailable,
        payload: resp.body
      });
      // Ensure only 1 instance of UpdateNotificationModal can be open at once
      if (this.updateNotificationModalRef != null) { return; }
      this.updateNotificationModalRef = this.modalService.open(UpdateNotificationModalComponent, { scrollable: true, size: 'lg' });
      this.updateNotificationModalRef.componentInstance.updateData = resp.body;
      this.updateNotificationModalRef.closed.subscribe(() => {
        this.updateNotificationModalRef = null;
      });
      this.updateNotificationModalRef.dismissed.subscribe(() => {
        this.updateNotificationModalRef = null;
      });
    });
  }

  stopHubConnection() {
    this.hubConnection.stop().catch(err => console.error(err));
  }

  sendMessage(methodName: string, body?: any) {
    return this.hubConnection.invoke(methodName, body);
  }
  
}
