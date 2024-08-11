import {ChangeDetectionStrategy, Component, Input} from '@angular/core';
import {CarouselReelComponent} from "../../carousel/_components/carousel-reel/carousel-reel.component";
import {PersonBadgeComponent} from "../../shared/person-badge/person-badge.component";
import {TranslocoDirective} from "@jsverse/transloco";
import {IHasCast} from "../../_models/metadata/series-metadata";

@Component({
  selector: 'app-cast-tab',
  standalone: true,
  imports: [
    CarouselReelComponent,
    PersonBadgeComponent,
    TranslocoDirective
  ],
  templateUrl: './cast-tab.component.html',
  styleUrl: './cast-tab.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class CastTabComponent {

  @Input({required: true}) metadata!: IHasCast;

}
