import {ChangeDetectionStrategy, Component, inject, Input} from '@angular/core';
import {CarouselReelComponent} from "../../carousel/_components/carousel-reel/carousel-reel.component";
import {PersonBadgeComponent} from "../../shared/person-badge/person-badge.component";
import {TranslocoDirective} from "@jsverse/transloco";
import {IHasCast} from "../../_models/common/i-has-cast";
import {Person, PersonRole} from "../../_models/metadata/person";
import {Router} from "@angular/router";
import {FilterField} from "../../_models/metadata/v2/filter-field";
import {FilterComparison} from "../../_models/metadata/v2/filter-comparison";
import {FilterUtilitiesService} from "../../shared/_services/filter-utilities.service";

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

  private readonly router = inject(Router);
  private readonly filterUtilityService = inject(FilterUtilitiesService);
  protected readonly PersonRole = PersonRole;
  protected readonly FilterField = FilterField;

  @Input({required: true}) metadata!: IHasCast;


  openPerson(queryParamName: FilterField, filter: Person) {
    if (queryParamName === FilterField.None) return;
    this.filterUtilityService.applyFilter(['all-series'], queryParamName, FilterComparison.Equal, `${filter.id}`).subscribe();
  }


}
