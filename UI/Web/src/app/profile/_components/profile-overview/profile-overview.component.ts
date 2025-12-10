import {ChangeDetectionStrategy, Component, computed, inject, input} from '@angular/core';
import {MemberInfo} from "../../../_models/user/member-info";
import {AsyncPipe} from "@angular/common";
import {
  CarouselReelComponent,
  NextPageLoader
} from "../../../carousel/_components/carousel-reel/carousel-reel.component";
import {SeriesCardComponent} from "../../../cards/series-card/series-card.component";
import {map, Observable} from "rxjs";
import {AccountService} from "../../../_services/account.service";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {SeriesService} from "../../../_services/series.service";

type OverviewStream = {
  title: string;
  api: Observable<any[]>;
  nextPageLoader: NextPageLoader;
}

@Component({
  selector: 'app-profile-overview',
  imports: [
    AsyncPipe,
    CarouselReelComponent,
    SeriesCardComponent,
    TranslocoDirective
  ],
  templateUrl: './profile-overview.component.html',
  styleUrl: './profile-overview.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProfileOverviewComponent {

  private readonly accountService = inject(AccountService);
  private readonly seriesService = inject(SeriesService);

  memberInfo = input.required<MemberInfo>();

  streams = computed<OverviewStream[]>(() => {
    const userId = this.accountService.currentUserSignal()!.id;
    const memberId = this.memberInfo().id;

    return [
      {
        title: translate('profile-overview.currently-reading'),
        api: this.seriesService
          .getCurrentlyReading(memberId, 0, 20)
          .pipe(map(pr => pr.result)),
        nextPageLoader: (pageNum, pageSize) => this.seriesService
          .getCurrentlyReading(memberId, pageNum, pageSize),
      },
      {
        title: translate('profile-overview.want-to-read'),
        api: this.seriesService
          .getWantToRead(0, 20, undefined, memberId)
          .pipe(map(pr => pr.result)),
        nextPageLoader: (pageNum, pageSize) => this.seriesService
          .getWantToRead(pageNum, pageSize, undefined, memberId),
      }
    ];
  });

}
