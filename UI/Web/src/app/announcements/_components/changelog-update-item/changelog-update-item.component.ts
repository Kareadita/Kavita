import {ChangeDetectionStrategy, Component, inject, Input} from '@angular/core';
import {SafeHtmlPipe} from "../../../_pipes/safe-html.pipe";
import {UpdateSectionComponent} from "../update-section/update-section.component";
import {AsyncPipe, DatePipe} from "@angular/common";
import {UpdateVersionEvent} from "../../../_models/events/update-version-event";
import {TranslocoDirective} from "@jsverse/transloco";
import {AccountService} from "../../../_services/account.service";

@Component({
  selector: 'app-changelog-update-item',
  standalone: true,
  imports: [
    SafeHtmlPipe,
    UpdateSectionComponent,
    AsyncPipe,
    DatePipe,
    TranslocoDirective
  ],
  templateUrl: './changelog-update-item.component.html',
  styleUrl: './changelog-update-item.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ChangelogUpdateItemComponent {

  protected readonly accountService = inject(AccountService);

  @Input({required:true}) update: UpdateVersionEvent | null = null;
  @Input() index: number = 0;
  @Input() showExtras: boolean = true;

}
