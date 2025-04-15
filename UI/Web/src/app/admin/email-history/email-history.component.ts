import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, OnInit} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {VirtualScrollerModule} from "@iharbeck/ngx-virtual-scroller";
import {UtcToLocalTimePipe} from "../../_pipes/utc-to-local-time.pipe";
import {EmailHistory} from "../../_models/email-history";
import {EmailService} from "../../_services/email.service";
import {ColumnMode, NgxDatatableModule} from "@siemens/ngx-datatable";

@Component({
  selector: 'app-email-history',
  imports: [
    TranslocoDirective,
    VirtualScrollerModule,
    UtcToLocalTimePipe,
    NgxDatatableModule
  ],
  templateUrl: './email-history.component.html',
  styleUrl: './email-history.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class EmailHistoryComponent implements OnInit {
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly emailService = inject(EmailService)

  isLoading = true;
  data: Array<EmailHistory> = [];

  ngOnInit() {
    this.emailService.getEmailHistory().subscribe(data => {
      this.data = data;
      this.isLoading = false;
      this.cdRef.markForCheck();
    });
  }

  protected readonly ColumnMode = ColumnMode;
}
