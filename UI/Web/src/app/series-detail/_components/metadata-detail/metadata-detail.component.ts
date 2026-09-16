import {ChangeDetectionStrategy, Component, contentChild, inject, input, Input, TemplateRef} from '@angular/core';
import {NgTemplateOutlet} from '@angular/common';
import {A11yClickDirective} from "../../../shared/a11y-click.directive";
import {BadgeExpanderComponent} from "../../../shared/badge-expander/badge-expander.component";
import {TagBadgeComponent, TagBadgeCursor} from "../../../shared/tag-badge/tag-badge.component";
import {FilterUtilitiesService} from "../../../shared/_services/filter-utilities.service";
import {FilterComparison} from "../../../_models/metadata/v2/filter-comparison";
import {SeriesFilterField} from "../../../_models/metadata/v2/series-filter-field";
import {UtilityService} from "../../../shared/_services/utility.service";
import {BreakpointService} from "../../../_services/breakpoint.service";

@Component({
    selector: 'app-metadata-detail',
  imports: [BadgeExpanderComponent, TagBadgeComponent, NgTemplateOutlet, A11yClickDirective],
    templateUrl: './metadata-detail.component.html',
    styleUrls: ['./metadata-detail.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class MetadataDetailComponent<T> {

  private readonly filterUtilityService = inject(FilterUtilitiesService);
  public readonly utilityService = inject(UtilityService);
  protected readonly breakpointService = inject(BreakpointService);

  protected readonly TagBadgeCursor = TagBadgeCursor;

  items = input.required<T[]>();
  libraryId = input.required<number>();
  heading = input.required<string>();
  queryParam = input(SeriesFilterField.None);
  includeComma = input(true);

  readonly titleTemplate = contentChild.required<TemplateRef<any>>('titleTemplate');
  readonly itemTemplate = contentChild<TemplateRef<any>>('itemTemplate');


  goTo(queryParamName: SeriesFilterField, filter: any) {
    if (queryParamName === SeriesFilterField.None) return;
    this.filterUtilityService.applyFilter(['library', this.libraryId()], queryParamName, FilterComparison.Equal, filter).subscribe();
  }


}
