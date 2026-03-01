import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, OnInit, signal} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {VirtualScrollerModule} from "@iharbeck/ngx-virtual-scroller";
import {UtcToLocalTimePipe} from "../../_pipes/utc-to-local-time.pipe";
import {EmailHistory} from "../../_models/email-history";
import {EmailService} from "../../_services/email.service";
import {NgxDatatableModule} from "@siemens/ngx-datatable";
import {ResponsiveTableComponent} from "../../shared/_components/responsive-table/responsive-table.component";
import {LoadingComponent} from "../../shared/loading/loading.component";

@Component({
  selector: 'app-email-history',
  imports: [
    TranslocoDirective,
    VirtualScrollerModule,
    UtcToLocalTimePipe,
    NgxDatatableModule,
    ResponsiveTableComponent,
    LoadingComponent
  ],
  templateUrl: './email-history.component.html',
  styleUrl: './email-history.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class EmailHistoryComponent implements OnInit {
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly emailService = inject(EmailService)

  isLoading = signal<boolean>(true);
  data = signal<EmailHistory[]>([]);

  trackBy = (index: number, item: EmailHistory) => `${item.sent}_${item.emailTemplate}_${index}`;

  ngOnInit() {
    this.emailService.getEmailHistory().subscribe(data => {
      this.data.set(data);
      this.isLoading.set(false);
    });
  }
}
