import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, OnInit} from '@angular/core';
import {LicenseService} from "../../_services/license.service";
import {Router} from "@angular/router";
import {TranslocoDirective} from "@jsverse/transloco";
import {ImageComponent} from "../../shared/image/image.component";
import {ImageService} from "../../_services/image.service";
import {CardActionablesComponent} from "../../_single-module/card-actionables/card-actionables.component";
import {Series} from "../../_models/series";
import {Action, ActionFactoryService, ActionItem} from "../../_services/action-factory.service";
import {ActionService} from "../../_services/action.service";
import {ManageService} from "../../_services/manage.service";
import {ManageMatchSeries} from "../../_models/kavitaplus/manage-match-series";
import {VirtualScrollerModule} from "@iharbeck/ngx-virtual-scroller";
import {FormControl, FormGroup, ReactiveFormsModule} from "@angular/forms";
import {Select2Module} from "ng-select2-component";
import {ManageMatchFilter} from "../../_models/kavitaplus/manage-match-filter";
import {allMatchStates, MatchStateOption} from "../../_models/kavitaplus/match-state-option";
import {MatchStateOptionPipe} from "../../_pipes/match-state.pipe";
import {UtcToLocalTimePipe} from "../../_pipes/utc-to-local-time.pipe";
import {debounceTime, distinctUntilChanged, switchMap, tap} from "rxjs";
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";
import {LooseLeafOrDefaultNumber, SpecialVolumeNumber} from "../../_models/chapter";
import {ScrobbleEventType} from "../../_models/scrobbling/scrobble-event";
import {ColumnMode, NgxDatatableModule} from "@siemens/ngx-datatable";

@Component({
  selector: 'app-manage-matched-metadata',
  standalone: true,
    imports: [
        TranslocoDirective,
        ImageComponent,
        CardActionablesComponent,
        VirtualScrollerModule,
        ReactiveFormsModule,
        Select2Module,
        MatchStateOptionPipe,
        UtcToLocalTimePipe,
        DefaultValuePipe,
        NgxDatatableModule,
    ],
  templateUrl: './manage-matched-metadata.component.html',
  styleUrl: './manage-matched-metadata.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManageMatchedMetadataComponent implements OnInit {
  protected readonly MatchState = MatchStateOption;
  protected readonly allMatchStates = allMatchStates.filter(m => m !== MatchStateOption.Matched); // Matched will have too many

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
  filterGroup = new FormGroup({
    'matchState': new FormControl(MatchStateOption.Error, []),
  });

  ngOnInit() {
    this.licenseService.hasValidLicense$.subscribe(license => {
      if (!license) {
        // Navigate home
        this.router.navigate(['/']);
        return;
      }

      this.filterGroup.valueChanges.pipe(
        debounceTime(300),
        distinctUntilChanged(),
        tap(_ => {
          this.isLoading = true;
          this.cdRef.markForCheck();
        }),
        switchMap(_ => this.loadData()),
        tap(_ => {
          this.isLoading = false;
          this.cdRef.markForCheck();
        }),
      ).subscribe();

      this.loadData().subscribe();

    });
  }


  loadData() {
    const filter: ManageMatchFilter = {
      matchStateOption: parseInt(this.filterGroup.get('matchState')!.value + '', 10),
      searchTerm: ''
    };

    this.isLoading = true;
    this.data = [];
    this.cdRef.markForCheck();

    return this.manageService.getAllKavitaPlusSeries(filter).pipe(tap(data => {
      this.data = [...data];
      this.isLoading = false;
      this.cdRef.markForCheck();
    }));
  }

  performAction(action: ActionItem<Series>, series: Series) {
    if (action.callback) {
      action.callback(action, series);
    }
  }

  fixMatch(actionItem: ActionItem<Series>, series: Series) {
    this.actionService.matchSeries(series, result => {
      if (!result) return;
      this.loadData().subscribe();
    });
  }

    protected readonly LooseLeafOrDefaultNumber = LooseLeafOrDefaultNumber;
    protected readonly ScrobbleEventType = ScrobbleEventType;
    protected readonly SpecialVolumeNumber = SpecialVolumeNumber;
    protected readonly ColumnMode = ColumnMode;
}
