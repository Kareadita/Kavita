import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, Input, OnInit} from '@angular/core';
import {Series} from "../../_models/series";
import {SeriesService} from "../../_services/series.service";
import {FormControl, FormGroup} from "@angular/forms";
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";
import {TranslocoDirective} from "@jsverse/transloco";
import {ExternalSeries} from "../../_models/series-detail/external-series";
import {ImageComponent} from "../../shared/image/image.component";
import {SeriesFormatComponent} from "../../shared/series-format/series-format.component";
import {MatchSeriesResultItemComponent} from "../match-series-result-item/match-series-result-item.component";
import {ExternalSeriesDetail} from "../../_models/series-detail/external-series-detail";
import {LoadingComponent} from "../../shared/loading/loading.component";
import {ExternalSeriesMatch} from "../../_models/series-detail/external-series-match";

@Component({
  selector: 'app-match-series-modal',
  standalone: true,
  imports: [
    TranslocoDirective,
    ImageComponent,
    MatchSeriesResultItemComponent,
    LoadingComponent
  ],
  templateUrl: './match-series-modal.component.html',
  styleUrl: './match-series-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MatchSeriesModalComponent implements OnInit {
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly seriesService = inject(SeriesService);
  private readonly modalService = inject(NgbActiveModal);

  @Input({required: true}) series!: Series;

  formGroup = new FormGroup({});
  matches: Array<ExternalSeriesMatch> = [];
  isLoading = true;

  ngOnInit() {
    this.formGroup.addControl('query', new FormControl('', []));
    this.formGroup.addControl('dontMatch', new FormControl(false, []));

    const model: any = this.formGroup.value;
    model.seriesId = this.series.id;


    this.seriesService.matchSeries(model).subscribe(results => {
      this.isLoading = false;
      this.matches = results;
      this.cdRef.detectChanges();
    });
  }

  close() {
    this.modalService.close(false);
  }

  save() {

    this.modalService.close(true);
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
