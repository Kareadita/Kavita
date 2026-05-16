import {
  ChangeDetectionStrategy,
  Component,
  contentChild,
  effect,
  inject,
  input,
  signal,
  TemplateRef
} from '@angular/core';
import {NgTemplateOutlet} from "@angular/common";
import {NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {TranslocoDirective} from "@jsverse/transloco";
import {ScrobbleProvider, ScrobblingService} from "../../_services/scrobbling.service";
import {ScrobbleProviderNamePipe} from "../../_pipes/scrobble-provider-name.pipe";
import {
  ScrobbleProviderImageComponent
} from "../../shared/_components/scrobble-provider-image/scrobble-provider-image.component";

@Component({
  selector: 'app-scrobble-provider-item',
  imports: [
    NgbTooltip,
    TranslocoDirective,
    ScrobbleProviderNamePipe,
    NgTemplateOutlet,
    ScrobbleProviderImageComponent
  ],
  templateUrl: './scrobble-provider-item.component.html',
  styleUrl: './scrobble-provider-item.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ScrobbleProviderItemComponent {

  private readonly scrobblingService = inject(ScrobblingService);

  provider = input.required<ScrobbleProvider>();
  token = input.required<string>();
  isEditMode = input<boolean>(false);
  editRef = contentChild<TemplateRef<any>>('edit');

  hasExpired = signal<boolean>(false);

  constructor() {

    effect(() => {
      this.scrobblingService.hasTokenExpired(this.provider()).subscribe(hasExpired => {
        this.hasExpired.set(hasExpired);
      });
    });
  }

}
