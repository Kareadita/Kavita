import { ChangeDetectionStrategy, Component, Input, OnInit } from '@angular/core';
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { CompactNumberPipe } from 'src/app/pipe/compact-number.pipe';
import { StatisticsService } from 'src/app/_services/statistics.service';
import { GenericListModalComponent } from '../_modals/generic-list-modal/generic-list-modal.component';

@Component({
  selector: 'app-user-stats-info-cards',
  templateUrl: './user-stats-info-cards.component.html',
  styleUrls: ['./user-stats-info-cards.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class UserStatsInfoCardsComponent {

  @Input() totalPagesRead: number = 0;
  @Input() totalWordsRead: number = 0;
  @Input() timeSpentReading: number = 0;
  @Input() chaptersRead: number = 0;
  @Input() lastActive: string = '';
  @Input() avgHoursPerWeekSpentReading: number = 0;

  constructor(private statsService: StatisticsService, private modalService: NgbModal) { }

  openPageByYearList() {
    const numberPipe = new CompactNumberPipe();
    this.statsService.getPagesPerYear().subscribe(yearCounts => {
      const ref = this.modalService.open(GenericListModalComponent, { scrollable: true });
      ref.componentInstance.items = yearCounts.map(t => `${t.name}: ${numberPipe.transform(t.value)} pages`);
      ref.componentInstance.title = 'Pages Read By Year';
    });
  }

  openWordByYearList() {
    const numberPipe = new CompactNumberPipe();
    this.statsService.getWordsPerYear().subscribe(yearCounts => {
      const ref = this.modalService.open(GenericListModalComponent, { scrollable: true });
      ref.componentInstance.items = yearCounts.map(t => `${t.name}: ${numberPipe.transform(t.value)} words`);
      ref.componentInstance.title = 'Words Read By Year';
    });
  }

}
