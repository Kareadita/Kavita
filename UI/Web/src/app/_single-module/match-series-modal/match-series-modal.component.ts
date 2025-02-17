import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, Input, OnInit} from '@angular/core';
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
import { ThemeService } from 'src/app/_services/theme.service';
import { AsyncPipe } from '@angular/common';

@Component({
  selector: 'app-match-series-modal',
  standalone: true,
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
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly seriesService = inject(SeriesService);
  private readonly modalService = inject(NgbActiveModal);
  private readonly toastr = inject(ToastrService);
  protected readonly themeService = inject(ThemeService);

  @Input({required: true}) series!: Series;

  formGroup = new FormGroup({});
  matches: Array<ExternalSeriesMatch> = [];
  isLoading = true;

  ngOnInit() {
    this.formGroup.addControl('query', new FormControl('', []));
    this.formGroup.addControl('dontMatch', new FormControl(this.series?.dontMatch || false, []));

    this.search();
  }

  search() {
    this.isLoading = true;
    this.cdRef.markForCheck();

    const model: any = this.formGroup.value;
    model.seriesId = this.series.id;

    if (model.dontMatch) return;

    this.seriesService.matchSeries(model).subscribe(results => {
      this.isLoading = false;
      this.matches = results;
      this.cdRef.markForCheck();
    });
  }

  close() {
    this.modalService.close(false);
  }

  save() {

    const model: any = this.formGroup.value;
    model.seriesId = this.series.id;

    const dontMatchChanged = this.series.dontMatch !== model.dontMatch;

    // We need to update the dontMatch status
    if (dontMatchChanged) {
      this.seriesService.updateDontMatch(this.series.id, model.dontMatch).subscribe(_ => {
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

    this.seriesService.updateMatch(this.series.id, data).subscribe(_ => {
      this.save();
    });
  }

}
