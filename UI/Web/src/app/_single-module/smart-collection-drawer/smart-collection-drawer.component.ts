import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, Input, OnInit} from '@angular/core';
import {NgbActiveOffcanvas, NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {UserCollection} from "../../_models/collection-tag";
import {ImageComponent} from "../../shared/image/image.component";
import {LoadingComponent} from "../../shared/loading/loading.component";
import {MetadataDetailComponent} from "../../series-detail/_components/metadata-detail/metadata-detail.component";
import {DatePipe, DecimalPipe, NgOptimizedImage} from "@angular/common";
import {ProviderImagePipe} from "../../_pipes/provider-image.pipe";
import {PublicationStatusPipe} from "../../_pipes/publication-status.pipe";
import {ReadMoreComponent} from "../../shared/read-more/read-more.component";
import {TranslocoDirective} from "@jsverse/transloco";
import {Series} from "../../_models/series";
import {SafeHtmlPipe} from "../../_pipes/safe-html.pipe";
import {RouterLink} from "@angular/router";
import {DefaultDatePipe} from "../../_pipes/default-date.pipe";
import {UtcToLocalTimePipe} from "../../_pipes/utc-to-local-time.pipe";
import {SettingItemComponent} from "../../settings/_components/setting-item/setting-item.component";

@Component({
  selector: 'app-smart-collection-drawer',
  standalone: true,
  imports: [
    ImageComponent,
    LoadingComponent,
    MetadataDetailComponent,
    NgOptimizedImage,
    NgbTooltip,
    ProviderImagePipe,
    PublicationStatusPipe,
    ReadMoreComponent,
    TranslocoDirective,
    SafeHtmlPipe,
    RouterLink,
    DatePipe,
    DefaultDatePipe,
    UtcToLocalTimePipe,
    SettingItemComponent,
    DecimalPipe
  ],
  templateUrl: './smart-collection-drawer.component.html',
  styleUrl: './smart-collection-drawer.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SmartCollectionDrawerComponent implements OnInit {
  private readonly activeOffcanvas = inject(NgbActiveOffcanvas);
  private readonly cdRef = inject(ChangeDetectorRef);

  @Input({required: true}) collection!: UserCollection;
  @Input({required: true}) series: Series[] = [];

  ngOnInit() {

  }

  close() {
    this.activeOffcanvas.close();
  }
}
