import {ChangeDetectionStrategy, Component, ContentChild, inject, Input, TemplateRef} from '@angular/core';
import {NgTemplateOutlet} from '@angular/common';
import {A11yClickDirective} from "../../../shared/a11y-click.directive";
import {BadgeExpanderComponent} from "../../../shared/badge-expander/badge-expander.component";
import {TagBadgeComponent, TagBadgeCursor} from "../../../shared/tag-badge/tag-badge.component";
import {FilterUtilitiesService} from "../../../shared/_services/filter-utilities.service";
import {FilterComparison} from "../../../_models/metadata/v2/filter-comparison";
import {FilterField} from "../../../_models/metadata/v2/filter-field";
import {Breakpoint, UtilityService} from "../../../shared/_services/utility.service";

@Component({
  selector: 'app-metadata-detail',
  standalone: true,
  imports: [A11yClickDirective, BadgeExpanderComponent, TagBadgeComponent, NgTemplateOutlet],
  templateUrl: './metadata-detail.component.html',
  styleUrls: ['./metadata-detail.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MetadataDetailComponent {

  private readonly filterUtilityService = inject(FilterUtilitiesService);
  public readonly utilityService = inject(UtilityService);

  protected readonly TagBadgeCursor = TagBadgeCursor;
  protected readonly Breakpoint = Breakpoint;

  @Input({required: true}) tags: Array<any> = [];
  @Input({required: true}) libraryId!: number;
  @Input({required: true}) heading!: string;
  @Input() queryParam: FilterField = FilterField.None;
  @Input() includeComma: boolean = true;
  @ContentChild('titleTemplate') titleTemplate!: TemplateRef<any>;
  @ContentChild('itemTemplate') itemTemplate?: TemplateRef<any>;


  goTo(queryParamName: FilterField, filter: any) {
    if (queryParamName === FilterField.None) return;
    this.filterUtilityService.applyFilter(['library', this.libraryId], queryParamName, FilterComparison.Equal, filter).subscribe();
  }


}
