import { Injectable } from '@angular/core';
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import { User } from '@sentry/angular';
import { ToastrService } from 'ngx-toastr';
import { environment } from 'src/environments/environment';

@Injectable({
  providedIn: 'root'
})
export class MessageHubService {
  hubUrl = environment.hubUrl;
  private hubConnection!: HubConnection;

  constructor(private toatsr: ToastrService) { }

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
  }

  stopHubConnection() {
    this.hubConnection.stop().catch(err => console.error(err));
  }
  
}
