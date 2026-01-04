import {ChangeDetectionStrategy, Component, inject, input} from '@angular/core';
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {MostActiveUser} from "../../_models/top-reads";
import {ImageService} from "../../../_services/image.service";
import {ProfileIconComponent} from "../../../_single-module/profile-icon/profile-icon.component";
import {RouterLink} from "@angular/router";
import {TimeDurationPipe} from "../../../_pipes/time-duration.pipe";
import {ImageComponent} from "../../../shared/image/image.component";
import {StatsFilter} from "../../_models/stats-filter";


@Component({
  selector: 'app-active-user-card',
  imports: [
    TranslocoDirective,
    ProfileIconComponent,
    RouterLink,
    TimeDurationPipe,
    ImageComponent
  ],
  templateUrl: './active-user-card.component.html',
  styleUrl: './active-user-card.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ActiveUserCardComponent {
  protected readonly imageService = inject(ImageService);

  user = input.required<MostActiveUser>();
  filter = input.required<StatsFilter>();
  timeFrameLabel = input<string>(translate('time-frame-label.overall'));


  formatNumber(num: number): string {
    if (num >= 1000000) return `${(num / 1000000).toFixed(1)}M`;
    if (num >= 1000) return `${(num / 1000).toFixed(1)}K`;
    return num.toString();
  }
}
