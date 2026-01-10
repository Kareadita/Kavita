import {ChangeDetectionStrategy, Component, computed, effect, inject, OnInit, signal} from '@angular/core';
import {LicenseService} from "../../_services/license.service";
import {Router} from "@angular/router";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {ImageComponent} from "../../shared/image/image.component";
import {ImageService} from "../../_services/image.service";
import {Series} from "../../_models/series";
import {ActionService} from "../../_services/action.service";
import {ManageService} from "../../_services/manage.service";
import {ManageMatchSeries} from "../../_models/kavitaplus/manage-match-series";
import {VirtualScrollerModule} from "@iharbeck/ngx-virtual-scroller";
import {FormControl, FormGroup, ReactiveFormsModule} from "@angular/forms";
import {ManageMatchFilter} from "../../_models/kavitaplus/manage-match-filter";
import {allMatchStates, MatchStateOption} from "../../_models/kavitaplus/match-state-option";
import {MatchStateOptionPipe} from "../../_pipes/match-state.pipe";
import {UtcToLocalTimePipe} from "../../_pipes/utc-to-local-time.pipe";
import {debounceTime, distinctUntilChanged, switchMap, tap} from "rxjs";
import {DefaultValuePipe} from "../../_pipes/default-value.pipe";
import {ColumnMode, NgxDatatableModule} from "@siemens/ngx-datatable";
import {LibraryNamePipe} from "../../_pipes/library-name.pipe";
import {APP_BASE_HREF, AsyncPipe} from "@angular/common";
import {EVENTS, MessageHubService} from "../../_services/message-hub.service";
import {ScanSeriesEvent} from "../../_models/events/scan-series-event";
import {LibraryTypePipe} from "../../_pipes/library-type.pipe";
import {allKavitaPlusMetadataApplicableTypes} from "../../_models/library/library";
import {ExternalMatchRateLimitErrorEvent} from "../../_models/events/external-match-rate-limit-error-event";
import {ToastrService} from "ngx-toastr";
import {ResponsiveTableComponent} from "../../shared/_components/responsive-table/responsive-table.component";
import {Pagination} from "../../_models/pagination";

@Component({
  selector: 'app-manage-matched-metadata',
  imports: [
    TranslocoDirective,
    ImageComponent,
    VirtualScrollerModule,
    ReactiveFormsModule,
    MatchStateOptionPipe,
    UtcToLocalTimePipe,
    DefaultValuePipe,
    NgxDatatableModule,
    LibraryNamePipe,
    AsyncPipe,
    LibraryTypePipe,
    ResponsiveTableComponent,
  ],
  templateUrl: './manage-matched-metadata.component.html',
  styleUrl: './manage-matched-metadata.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ManageMatchedMetadataComponent implements OnInit {
  protected readonly ColumnMode = ColumnMode;
  protected readonly MatchStateOption = MatchStateOption;
  protected readonly allMatchStates = allMatchStates.filter(m => m !== MatchStateOption.Matched); // Matched will have too many
  protected readonly allLibraryTypes = allKavitaPlusMetadataApplicableTypes;

  private readonly licenseService = inject(LicenseService);
  private readonly actionService = inject(ActionService);
  private readonly router = inject(Router);
  private readonly manageService = inject(ManageService);
  private readonly messageHub = inject(MessageHubService);
  private readonly toastr = inject(ToastrService);
  protected readonly imageService = inject(ImageService);
  protected readonly baseUrl = inject(APP_BASE_HREF);

  isLoading = signal(true);
  data = signal<ManageMatchSeries[]>([]);
  pagination = signal<Pagination>({
    currentPage: 1,
    totalItems: 0,
    totalPages: 0,
    itemsPerPage: 15,
  });

  filterGroup = new FormGroup({
    'matchState': new FormControl(MatchStateOption.Error, []),
    'libraryType': new FormControl(-1, []), // Denotes all
  });
  trackBy = (idx: number, item: ManageMatchSeries) => `${item.isMatched}_${item.series.name}_${idx}`;

  ngOnInit() {
    this.licenseService.hasValidLicense$.subscribe(license => {
      if (!license) {
        // Navigate home
        this.router.navigate(['/']);
        return;
      }

      this.messageHub.messages$.subscribe(message => {
        if (message.event == EVENTS.ScanSeries) {
          const evt = message.payload as ScanSeriesEvent;
          if (this.data().filter(d => d.series.id === evt.seriesId).length > 0) {
            this.loadData();
          }
        }

        if (message.event == EVENTS.ExternalMatchRateLimitError) {
          const evt = message.payload as ExternalMatchRateLimitErrorEvent;
          this.toastr.error(translate('toasts.external-match-rate-error', {seriesName: evt.seriesName}))
        }
      });

      this.filterGroup.valueChanges.pipe(
        debounceTime(300),
        distinctUntilChanged(),
        tap(_ => {
          this.isLoading.set(true);
        }),
        switchMap(_ => this.loadData()),
        tap(_ => {
          this.isLoading.set(false);
        }),
      ).subscribe();

      this.loadData().subscribe();
    });
  }

  onPageChange(page: number) {
    this.loadData(page + 1).subscribe();
  }

  loadData(pageNumber: number = 1) {
    const filter: ManageMatchFilter = {
      matchStateOption: parseInt(this.filterGroup.get('matchState')!.value + '', 10),
      libraryType: parseInt(this.filterGroup.get('libraryType')!.value + '', 10),
      searchTerm: ''
    };

    this.isLoading.set(true);

    return this.manageService.getAllKavitaPlusSeries(filter, pageNumber, this.pagination().itemsPerPage).pipe(tap(data => {
      this.data.set(data.result);
      this.pagination.set({
        itemsPerPage: data.pagination.itemsPerPage,
        totalItems: data.pagination.totalItems,
        totalPages: data.pagination.totalPages,
        currentPage: data.pagination.currentPage - 1, // ngx-datatable is 0 based, Kavita is 1 based
      });
      this.isLoading.set(false);
    }));
  }

  fixMatch(series: Series) {
    this.actionService.matchSeries(series, result => {
      if (!result) return;

      this.data.update(x => x.filter(s => s.series.id !== series.id));
    });
  }
}
