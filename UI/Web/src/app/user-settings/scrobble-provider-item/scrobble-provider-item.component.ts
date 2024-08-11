import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  ContentChild,
  inject,
  Input,
  TemplateRef
} from '@angular/core';
import {NgOptimizedImage, NgTemplateOutlet} from "@angular/common";
import {NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {TranslocoDirective} from "@jsverse/transloco";
import {ScrobbleProvider, ScrobblingService} from "../../_services/scrobbling.service";
import {ScrobbleProviderNamePipe} from "../../_pipes/scrobble-provider-name.pipe";

@Component({
  selector: 'app-scrobble-provider-item',
  standalone: true,
  imports: [
    NgOptimizedImage,
    NgbTooltip,
    TranslocoDirective,
    ScrobbleProviderNamePipe,
    NgTemplateOutlet
  ],
  templateUrl: './scrobble-provider-item.component.html',
  styleUrl: './scrobble-provider-item.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ScrobbleProviderItemComponent {

  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly scrobblingService = inject(ScrobblingService);

  @Input({required: true}) provider!: ScrobbleProvider;
  @Input({required: true}) token!: string;
  @Input({required: true}) isEditMode = false;
  @ContentChild('edit') editRef!: TemplateRef<any>;

  hasExpired: boolean = false;

  constructor() {
    this.scrobblingService.hasTokenExpired(ScrobbleProvider.AniList).subscribe(hasExpired => {
      this.hasExpired = hasExpired;
      this.cdRef.markForCheck();
    });
  }

}
