import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, OnInit} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {MemberService} from "../../_services/member.service";
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";
import {LoadingComponent} from "../../shared/loading/loading.component";
import {UtcToLocalTimePipe} from "../../_pipes/utc-to-local-time.pipe";
import {VirtualScrollerModule} from "@iharbeck/ngx-virtual-scroller";
import {UserTokenInfo} from "../../_models/kavitaplus/user-token-info";
import {ServerService} from "../../_services/server.service";
import {SettingsService} from "../settings.service";
import {MessageHubService} from "../../_services/message-hub.service";
import {ConfirmService} from "../../shared/confirm.service";
import {ColumnMode, NgxDatatableModule} from "@siemens/ngx-datatable";
import {CardActionablesComponent} from "../../_single-module/card-actionables/card-actionables.component";
import {ImageComponent} from "../../shared/image/image.component";

@Component({
  selector: 'app-manage-user-tokens',
  standalone: true,
    imports: [
        TranslocoDirective,
        DefaultValuePipe,
        LoadingComponent,
        UtcToLocalTimePipe,
        VirtualScrollerModule,
        CardActionablesComponent,
        ImageComponent,
        NgxDatatableModule
    ],
  templateUrl: './manage-user-tokens.component.html',
  styleUrl: './manage-user-tokens.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManageUserTokensComponent implements OnInit {

  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly memberService = inject(MemberService);

  isLoading = true;
  users: UserTokenInfo[] = [];

  ngOnInit() {
    this.loadData();
  }

  loadData() {
    this.isLoading = true;
    this.cdRef.markForCheck();

    this.memberService.getUserTokenInfo().subscribe(users => {
      this.users = users;
      this.isLoading = false;
      this.cdRef.markForCheck();
    });
  }

    protected readonly ColumnMode = ColumnMode;
}
