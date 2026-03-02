import {ChangeDetectionStrategy, Component, inject, input, OnInit, signal} from '@angular/core';
import {Series} from "../../_models/series";
import {SeriesService} from "../../_services/series.service";
import {FormControl, FormGroup, ReactiveFormsModule} from "@angular/forms";
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";
import {translate, TranslocoDirective} from "@jsverse/transloco";
import {MatchSeriesResultItemComponent} from "../match-series-result-item/match-series-result-item.component";
import {LoadingComponent} from "../../shared/loading/loading.component";
import {ExternalSeriesMatch} from "../../_models/series-detail/external-series-match";
import {ToastrService} from "ngx-toastr";
import {SettingItemComponent} from "../../settings/_components/setting-item/setting-item.component";
import {SettingSwitchComponent} from "../../settings/_components/setting-switch/setting-switch.component";
import {ThemeService} from 'src/app/_services/theme.service';
import {AsyncPipe} from '@angular/common';
import {catchError, of, tap} from "rxjs";

@Component({
    selector: 'app-match-series-modal',
    imports: [
        AsyncPipe,
        TranslocoDirective,
        MatchSeriesResultItemComponent,
        LoadingComponent,
        ReactiveFormsModule,
        SettingItemComponent,
        SettingSwitchComponent
    ],
    templateUrl: './match-series-modal.component.html',
    styleUrl: './match-series-modal.component.scss',
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class MatchSeriesModalComponent implements OnInit {
  private readonly seriesService = inject(SeriesService);
  private readonly modalService = inject(NgbActiveModal);
  private readonly toastr = inject(ToastrService);
  protected readonly themeService = inject(ThemeService);

  series = input.required<Series>();

  formGroup = new FormGroup({});
  matches = signal<ExternalSeriesMatch[]>([]);
  isLoading = signal<boolean>(true);

  ngOnInit() {
    this.formGroup.addControl('query', new FormControl('', []));
    this.formGroup.addControl('dontMatch', new FormControl(this.series().dontMatch || false, []));

    this.search();
  }

  search() {
    this.isLoading.set(true);

    const model: any = this.formGroup.value;
    model.seriesId = this.series().id;

    if (model.dontMatch) {
      this.isLoading.set(false);
      return;
    }

    this.seriesService.matchSeries(model).pipe(
      tap(results => {
        this.isLoading.set(false);
        this.matches.set(results);
      }),
      catchError(() => {
        this.isLoading.set(false);
        return of([]);
      })
    ).subscribe();
  }

  close() {
    this.modalService.dismiss();
  }

  save() {

    const model: any = this.formGroup.value;
    model.seriesId = this.series().id;

    const dontMatchChanged = this.series().dontMatch !== model.dontMatch;

    // We need to update the dontMatch status
    if (dontMatchChanged) {
      this.seriesService.updateDontMatch(this.series().id, model.dontMatch).subscribe(_ => {
        this.modalService.close(true);
      });
    } else {
      this.toastr.success(translate('toasts.match-success'));
      this.modalService.close(true);
    }
  }

  selectMatch(item: ExternalSeriesMatch) {
    const data = item.series;
    data.tags = data.tags || [];
    data.genres = data.genres || [];

    this.seriesService.updateMatch(this.series().id, item.series).subscribe(_ => {
      this.save();
    });
  }

}
