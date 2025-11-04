import {ChangeDetectionStrategy, Component, inject} from '@angular/core';
import {ReadingPaceComponent, ReadingStats} from "../../../statistics/_components/reading-pace/reading-pace.component";
import {AccountService} from "../../../_services/account.service";
import {ActivityGraphComponent} from "../../../statistics/_components/activity-graph/activity-graph.component";

@Component({
  selector: 'app-profile',
  imports: [
    ReadingPaceComponent,
    ActivityGraphComponent
  ],
  templateUrl: './profile.component.html',
  styleUrl: './profile.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProfileComponent {

  protected readonly accountService = inject(AccountService);

  testReadingPace: ReadingStats = {
    hoursRead: 25,
    pagesRead: 92201,
    wordsRead: 15000000,
    booksRead: 308,
    comicsRead: 45,
    daysInRange: 365
  };

}
