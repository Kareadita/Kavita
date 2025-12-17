import {ChangeDetectionStrategy, Component, computed, inject, input} from '@angular/core';
import {TranslocoDirective} from "@jsverse/transloco";
import {TopUserRead} from "../../_models/top-reads";
import {DecimalPipe, NgOptimizedImage} from "@angular/common";
import {ImageService} from "../../../_services/image.service";
import {ProfileIconComponent} from "../../../_single-module/profile-icon/profile-icon.component";

export interface UserReadStats {
  hoursRead: number;
  hoursInPeriod: number;
  comics: number;
  manga: number;
  books: number;
}

export interface TopSeries {
  id: number;
  name: string;
  coverUrl: string | null;
}

export interface ActiveUser {
  id: number;
  username: string;
  avatarUrl: string | null;
  stats: UserReadStats;
  topSeries: TopSeries[];
}

@Component({
  selector: 'app-active-user-card',
  imports: [
    TranslocoDirective,
    DecimalPipe,
    NgOptimizedImage,
    ProfileIconComponent
  ],
  templateUrl: './active-user-card.component.html',
  styleUrl: './active-user-card.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ActiveUserCardComponent {
  private readonly imageService = inject(ImageService);

  user = input.required<TopUserRead>();
  timeFrameLabel = input<string>('this week');

  totalRead = computed(() => {
    const user = this.user();

    return user.booksTime + user.comicsTime + user.mangaTime;
  });

  topSeries = computed(() => {
    const user = this.user();
    return [
      {id: 1, name: 'Test 1', coverUrl: null},
      {id: 2, name: 'Test 2', coverUrl: null},
      {id: 3, name: 'Test 3', coverUrl: null},
      {id: 4, name: 'Test 4', coverUrl: null},
      {id: 5, name: 'Test 5', coverUrl: null},
    ];
  })

  formatNumber(num: number): string {
    if (num >= 1000000) return `${(num / 1000000).toFixed(1)}M`;
    if (num >= 1000) return `${(num / 1000).toFixed(1)}K`;
    return num.toString();
  }
}
