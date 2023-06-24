import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  Input,
  ViewChild
} from '@angular/core';
import {CommonModule} from '@angular/common';
import {ExternalSeries} from "../../_models/series-detail/external-series";
import {RouterLinkActive} from "@angular/router";
import {CardsModule} from "../cards.module";
import {ImageComponent} from "../../shared/image/image.component";
import {NgbProgressbar, NgbTooltip} from "@ng-bootstrap/ng-bootstrap";
import {ReactiveFormsModule} from "@angular/forms";

@Component({
  selector: 'app-external-series-card',
  standalone: true,
  imports: [CommonModule, CardsModule, ImageComponent, NgbProgressbar, NgbTooltip, ReactiveFormsModule, RouterLinkActive],
  templateUrl: './external-series-card.component.html',
  styleUrls: ['./external-series-card.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ExternalSeriesCardComponent {
  @Input({required: true}) data!: ExternalSeries;
  @ViewChild('link', {static: false}) link!: ElementRef<HTMLAnchorElement>;

  handleClick() {
    if (this.link) {
      this.link.nativeElement.click();
    }
  }
}
