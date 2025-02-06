import { Component, OnDestroy, OnInit } from '@angular/core';
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import { BehaviorSubject, take } from 'rxjs';
import { AccountService } from 'src/app/_services/account.service';
import { environment } from 'src/environments/environment';
import { VirtualScrollerModule } from '@iharbeck/ngx-virtual-scroller';
import { AsyncPipe, DatePipe } from '@angular/common';

interface LogMessage {
  timestamp: string;
  level: 'Information' | 'Debug' | 'Warning' | 'Error';
  message: string;
  exception: string;
}

@Component({
    selector: 'app-manage-logs',
    templateUrl: './manage-logs.component.html',
    styleUrls: ['./manage-logs.component.scss'],
    standalone: true,
    imports: [VirtualScrollerModule, AsyncPipe, DatePipe]
})
export class ManageLogsComponent implements OnInit, OnDestroy {

  hubUrl = environment.hubUrl;
  private hubConnection!: HubConnection;

  logsSource = new BehaviorSubject<LogMessage[]>([]);
  public logs$ = this.logsSource.asObservable();

  constructor(private accountService: AccountService) { }

  ngOnInit(): void {
    // TODO: Come back and implement this one day
    this.accountService.currentUser$.pipe(take(1)).subscribe(user => {
      if (user) {
        this.hubConnection = new HubConnectionBuilder()
        .withUrl(this.hubUrl + 'logs', {
          accessTokenFactory: () => user.token
        })
        .withAutomaticReconnect()
        .build();

        console.log('Starting log connection');

      this.hubConnection
      .start()
      .catch(err => console.error(err));

      this.hubConnection.on('SendLogAsObject', resp => {
        const payload = resp.arguments[0] as LogMessage;
        const logMessage = {timestamp: payload.timestamp, level: payload.level, message: payload.message, exception: payload.exception};
        // NOTE: It might be better to just have a queue to show this
        const values = this.logsSource.getValue();
        values.push(logMessage);
        this.logsSource.next(values);
      });
      }
    });

  }

  ngOnDestroy(): void {
    // unsubscribe from signalr connection
    if (this.hubConnection) {
      this.hubConnection.stop().catch(err => console.error(err));
      console.log('Stoping log connection');
    }
  }

}
