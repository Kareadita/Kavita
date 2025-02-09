import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, OnInit} from '@angular/core';
import {LicenseService} from "../../_services/license.service";
import {Router} from "@angular/router";
import {TranslocoDirective} from "@jsverse/transloco";
import {ImageComponent} from "../../shared/image/image.component";
import {ImageService} from "../../_services/image.service";
import {Series} from "../../_models/series";
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
import {ColumnMode, NgxDatatableModule} from "@siemens/ngx-datatable";
import {LibraryNamePipe} from "../../_pipes/library-name.pipe";
import {AsyncPipe} from "@angular/common";
import {EVENTS, MessageHubService} from "../../_services/message-hub.service";
import {ScanSeriesEvent} from "../../_models/events/scan-series-event";

@Component({
  selector: 'app-manage-matched-metadata',
  standalone: true,
  imports: [
    TranslocoDirective,
    ImageComponent,
    VirtualScrollerModule,
    ReactiveFormsModule,
    Select2Module,
    MatchStateOptionPipe,
    UtcToLocalTimePipe,
    DefaultValuePipe,
    NgxDatatableModule,
    LibraryNamePipe,
    AsyncPipe,
  ],
  templateUrl: './manage-matched-metadata.component.html',
  styleUrl: './manage-matched-metadata.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManageMatchedMetadataComponent implements OnInit {
  protected readonly ColumnMode = ColumnMode;
  protected readonly MatchStateOption = MatchStateOption;
  protected readonly allMatchStates = allMatchStates.filter(m => m !== MatchStateOption.Matched); // Matched will have too many

  private readonly licenseService = inject(LicenseService);
  private readonly actionService = inject(ActionService);
  private readonly router = inject(Router);
  private readonly manageService = inject(ManageService);
  private readonly messageHub = inject(MessageHubService);
  private readonly cdRef = inject(ChangeDetectorRef);
  protected readonly imageService = inject(ImageService);


  isLoading: boolean = true;
  data: Array<ManageMatchSeries> = [];
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

      this.messageHub.messages$.subscribe(message => {
        if (message.event !== EVENTS.ScanSeries) return;

        const evt = message.payload as ScanSeriesEvent;
        if (this.data.filter(d => d.series.id === evt.seriesId).length > 0) {
          this.loadData();
        }
      });

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


  fixMatch(series: Series) {
    this.actionService.matchSeries(series, result => {
      if (!result) return;
      this.data = [...this.data.filter(s => s.series.id !== series.id)];
      this.cdRef.markForCheck();
    });
  }
}
