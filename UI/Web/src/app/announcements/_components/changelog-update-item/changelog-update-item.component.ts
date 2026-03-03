import {ChangeDetectionStrategy, Component, inject, input} from '@angular/core';
import {SafeHtmlPipe} from "../../../_pipes/safe-html.pipe";
import {UpdateSectionComponent} from "../update-section/update-section.component";
import {DatePipe} from "@angular/common";
import {UpdateVersionEvent} from "../../../_models/events/update-version-event";
import {TranslocoDirective} from "@jsverse/transloco";
import {AccountService} from "../../../_services/account.service";

@Component({
  selector: 'app-changelog-update-item',
  imports: [
      SafeHtmlPipe,
      UpdateSectionComponent,
      DatePipe,
      TranslocoDirective
  ],
  templateUrl: './changelog-update-item.component.html',
  styleUrl: './changelog-update-item.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ChangelogUpdateItemComponent {

  protected readonly accountService = inject(AccountService);

  update = input.required<UpdateVersionEvent | null>();
  index = input<number>(0);
  showExtras = input<boolean>(true);

}
