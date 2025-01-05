import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, OnInit} from '@angular/core';
import {LicenseService} from "../../_services/license.service";
import {take} from "rxjs/operators";
import {Router} from "@angular/router";
import {TranslocoDirective} from "@jsverse/transloco";
import {LoadingComponent} from "../../shared/loading/loading.component";
import {ImageComponent} from "../../shared/image/image.component";
import {ImageService} from "../../_services/image.service";
import {CardActionablesComponent} from "../../_single-module/card-actionables/card-actionables.component";
import {Series} from "../../_models/series";
import {Action, ActionFactoryService, ActionItem} from "../../_services/action-factory.service";
import {ActionService} from "../../_services/action.service";
import {ManageService} from "../../_services/manage.service";
import {ManageMatchSeries} from "../../_models/kavitaplus/manage-match-series";
import {VirtualScrollerModule} from "@iharbeck/ngx-virtual-scroller";

@Component({
  selector: 'app-manage-matched-metadata',
  standalone: true,
  imports: [
    TranslocoDirective,
    ImageComponent,
    CardActionablesComponent,
    LoadingComponent,
    VirtualScrollerModule
  ],
  templateUrl: './manage-matched-metadata.component.html',
  styleUrl: './manage-matched-metadata.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManageMatchedMetadataComponent implements OnInit {
  private readonly licenseService = inject(LicenseService);
  private readonly actionFactory = inject(ActionFactoryService);
  private readonly actionService = inject(ActionService);
  private readonly router = inject(Router);
  private readonly manageService = inject(ManageService);
  private readonly cdRef = inject(ChangeDetectorRef);
  protected readonly imageService = inject(ImageService);


  isLoading: boolean = true;
  data: Array<ManageMatchSeries> = [];
  actions: Array<ActionItem<Series>> = this.actionFactory.getSeriesActions(this.fixMatch.bind(this))
    .filter(item => item.action === Action.Match);


  constructor() {
    this.licenseService.hasValidLicense$.pipe(take(1)).subscribe(license => {
      if (!license) {
        // Navigate home
        this.router.navigate(['/']);
      }
    });
  }

  ngOnInit() {
    this.isLoading = true;
    this.cdRef.markForCheck();
    this.manageService.getAllKavitaPlusSeries().subscribe(data => {
      this.data = data;
      this.isLoading = false;
      this.cdRef.markForCheck();
    });
  }

  performAction(action: ActionItem<Series>, series: Series) {
    if (action.callback) {
      action.callback(action, series);
    }
  }

  fixMatch(actionItem: ActionItem<Series>, series: Series) {
    this.actionService.matchSeries(series, result => {

    });
  }

}
