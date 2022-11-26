import { ChangeDetectionStrategy, Component, Input, OnInit } from '@angular/core';

@Component({
  selector: 'app-user-stats-info-cards',
  templateUrl: './user-stats-info-cards.component.html',
  styleUrls: ['./user-stats-info-cards.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class UserStatsInfoCardsComponent implements OnInit {

  @Input() totalPagesRead: number = 0;
  @Input() timeSpentReading: number = 0;

  constructor() { }

  ngOnInit(): void {
  }

}
