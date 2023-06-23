import {ChangeDetectionStrategy, ChangeDetectorRef, Component, inject, Input} from '@angular/core';
import {CommonModule} from '@angular/common';
import {ExternalSeries} from "../../_models/series-detail/external-series";
import {Router, RouterLinkActive} from "@angular/router";
import {CardsModule} from "../cards.module";
import {ImageComponent} from "../../shared/image/image.component";
import {NgbProgressbar, NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {PipeModule} from "../../pipe/pipe.module";
import {ReactiveFormsModule} from "@angular/forms";

@Component({
  selector: 'app-external-series-card',
  standalone: true,
  imports: [CommonModule, CardsModule, ImageComponent, NgbProgressbar, NgbTooltip, PipeModule, ReactiveFormsModule, RouterLinkActive],
  templateUrl: './external-series-card.component.html',
  styleUrls: ['./external-series-card.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ExternalSeriesCardComponent {
  @Input({required: true}) data!: ExternalSeries;
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly router = inject(Router);

  handleClick() {
    this.router.navigateByUrl(this.data.url);
  }

  protected readonly undefined = undefined;
}
