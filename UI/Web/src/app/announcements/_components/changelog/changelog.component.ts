import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, OnInit} from '@angular/core';
import {UpdateVersionEvent} from 'src/app/_models/events/update-version-event';
import {ServerService} from 'src/app/_services/server.service';
import {LoadingComponent} from '../../../shared/loading/loading.component';
import {ReadMoreComponent} from '../../../shared/read-more/read-more.component';
import {DatePipe, NgFor, NgIf} from '@angular/common';
import {TranslocoDirective} from "@ngneat/transloco";

@Component({
  selector: 'app-changelog',
  templateUrl: './changelog.component.html',
  styleUrls: ['./changelog.component.scss'],
  standalone: true,
  imports: [NgFor, NgIf, ReadMoreComponent, LoadingComponent, DatePipe, TranslocoDirective],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ChangelogComponent implements OnInit {

  private readonly serverService = inject(ServerService);
  private readonly cdRef = inject(ChangeDetectorRef);
  updates: Array<UpdateVersionEvent> = [];
  isLoading: boolean = true;

  ngOnInit(): void {
    this.serverService.getChangelog().subscribe(updates => {
      this.updates = updates;
      this.isLoading = false;
      this.cdRef.markForCheck();
    });
  }

  isNightly(update: UpdateVersionEvent) {
    // Split the version numbers into arrays
    const updateVersionArr = update.updateVersion.split('.');
    const currentVersionArr = update.currentVersion.split('.');

    // Compare the first three parts of the version numbers
    for (let i = 0; i < 3; i++) {
      const updatePart = parseInt(updateVersionArr[i]);
      const currentPart = parseInt(currentVersionArr[i]);

      // If any part of the update version is less than the corresponding part of the current version, return true
      if (updatePart < currentPart) {
        return true;
      }
      // If any part of the update version is greater than the corresponding part of the current version, return false
      else if (updatePart > currentPart) {
        return false;
      }
    }

    // If all parts are equal, compare the length of the version numbers
    return updateVersionArr.length < currentVersionArr.length;
  }
}
