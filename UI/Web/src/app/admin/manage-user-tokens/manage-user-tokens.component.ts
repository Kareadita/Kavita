import {ChangeDetectionStrategy, Component, inject, OnInit, signal} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {MemberService} from "../../_services/member.service";
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";
import {UtcToLocalTimePipe} from "../../_pipes/utc-to-local-time.pipe";
import {VirtualScrollerModule} from "@iharbeck/ngx-virtual-scroller";
import {UserTokenInfo} from "../../_models/kavitaplus/user-token-info";
import {NgxDatatableModule} from "@siemens/ngx-datatable";
import {ResponsiveTableComponent} from "../../shared/_components/responsive-table/responsive-table.component";
import {ScrobbleProvider, ScrobblingService} from "../../_services/scrobbling.service";
import {ScrobbleProviderNamePipe} from "../../_pipes/scrobble-provider-name.pipe";
import {
  ScrobbleProviderImageComponent
} from "../../shared/_components/scrobble-provider-image/scrobble-provider-image.component";
import {NULL_DATE} from "../../_pipes/date-year-range.pipe";

@Component({
  selector: 'app-manage-user-tokens',
  imports: [
    TranslocoDirective,
    DefaultValuePipe,
    UtcToLocalTimePipe,
    VirtualScrollerModule,
    NgxDatatableModule,
    ResponsiveTableComponent,
    ScrobbleProviderNamePipe,
    ScrobbleProviderImageComponent
  ],
  templateUrl: './manage-user-tokens.component.html',
  styleUrl: './manage-user-tokens.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManageUserTokensComponent implements OnInit {

  private readonly memberService = inject(MemberService);
  protected readonly scrobblingService = inject(ScrobblingService);

  users = signal<UserTokenInfo[]>([]);
  trackBy = (idx: number, item: UserTokenInfo) => item;

  ngOnInit() {
    this.loadData();
  }

  loadData() {
    this.memberService.getUserTokenInfo().subscribe(users => {
      this.users.set([...users]);
    });
  }

  getTokenValidityInfo(user: UserTokenInfo, provider: ScrobbleProvider) {
    return user.tokens.find(token => token.provider == provider);
  }

  protected readonly NULL_DATE = NULL_DATE;
}
