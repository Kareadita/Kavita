import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, Input, OnInit} from '@angular/core';
import {NgbActiveOffcanvas} from "@ng-bootstrap/ng-bootstrap";
import {UserCollection} from "../../_models/collection-tag";
import {DecimalPipe} from "@angular/common";
import {TranslocoDirective} from "@jsverse/transloco";
import {Series} from "../../_models/series";
import {SafeHtmlPipe} from "../../_pipes/safe-html.pipe";
import {RouterLink} from "@angular/router";
import {DefaultDatePipe} from "../../_pipes/default-date.pipe";
import {UtcToLocalTimePipe} from "../../_pipes/utc-to-local-time.pipe";
import {SettingItemComponent} from "../../settings/_components/setting-item/setting-item.component";

@Component({
    selector: 'app-smart-collection-drawer',
    imports: [
        TranslocoDirective,
        SafeHtmlPipe,
        RouterLink,
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
