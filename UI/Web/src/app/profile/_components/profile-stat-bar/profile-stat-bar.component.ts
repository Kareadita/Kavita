import {ChangeDetectionStrategy, Component, inject, input} from '@angular/core';
import {StatisticsService} from "../../../_services/statistics.service";
import {StatsFilter} from "../../../statistics/_models/stats-filter";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {DecimalPipe} from "@angular/common";
import {IconAndTitleComponent} from "../../../shared/icon-and-title/icon-and-title.component";
import {CompactNumberPipe} from "../../../_pipes/compact-number.pipe";
import {
  GenericListModalComponent
} from "../../../statistics/_components/_modals/generic-list-modal/generic-list-modal.component";
import {NgbModal} from "@ng-bootstrap/ng-bootstrap";

export interface ProfileStatBar {
  booksRead: number;
  comicsRead: number;
  pagesRead: number;
  wordsRead: number;
  authorsRead: number;
  reviews: number;
  ratings: number;
}

@Component({
  selector: 'app-profile-stat-bar',
  imports: [
    TranslocoDirective,
    DecimalPipe,
    IconAndTitleComponent
  ],
  templateUrl: './profile-stat-bar.component.html',
  styleUrl: './profile-stat-bar.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProfileStatBarComponent {
  private readonly statsService = inject(StatisticsService);
  private readonly modalService = inject(NgbModal);

  userId = input.required<number>();
  year = input.required<number>();
  filter = input.required<StatsFilter>();

  dataResource = this.statsService.getUserOverallStats(() => this.filter(), () => this.userId());

  openPageByYearList() {
    const numberPipe = new CompactNumberPipe();
    this.statsService.getPagesPerYear(this.userId()).subscribe(yearCounts => {
      const ref = this.modalService.open(GenericListModalComponent, { scrollable: true });
      ref.componentInstance.items = yearCounts.map(t => {
        const countStr = translate('user-stats-info-cards.pages-count', {num: numberPipe.transform(t.value)});
        return `${t.name}: ${countStr}`;
      });
      ref.componentInstance.title = translate('user-stats-info-cards.pages-read-by-year-title');
    });
  }

  openWordByYearList() {
    const numberPipe = new CompactNumberPipe();
    this.statsService.getWordsPerYear(this.userId()).subscribe(yearCounts => {
      const ref = this.modalService.open(GenericListModalComponent, { scrollable: true });
      ref.componentInstance.items = yearCounts.map(t => {
        const countStr = translate('user-stats-info-cards.words-count', {num: numberPipe.transform(t.value)});
        return `${t.name}: ${countStr}`;
      });
      ref.componentInstance.title = translate('user-stats-info-cards.words-read-by-year-title');
    });
  }
}
