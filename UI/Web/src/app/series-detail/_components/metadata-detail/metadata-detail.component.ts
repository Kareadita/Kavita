import {ChangeDetectionStrategy, Component, ContentChild, inject, Input, TemplateRef} from '@angular/core';
import {CommonModule} from '@angular/common';
import {A11yClickDirective} from "../../../shared/a11y-click.directive";
import {BadgeExpanderComponent} from "../../../shared/badge-expander/badge-expander.component";
import {TagBadgeComponent, TagBadgeCursor} from "../../../shared/tag-badge/tag-badge.component";
import {FilterUtilitiesService} from "../../../shared/_services/filter-utilities.service";
import {FilterComparison} from "../../../_models/metadata/v2/filter-comparison";
import {FilterField} from "../../../_models/metadata/v2/filter-field";

@Component({
  selector: 'app-metadata-detail',
  standalone: true,
  imports: [CommonModule, A11yClickDirective, BadgeExpanderComponent, TagBadgeComponent],
  templateUrl: './metadata-detail.component.html',
  styleUrls: ['./metadata-detail.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MetadataDetailComponent {

  @Input({required: true}) tags: Array<any> = [];
  @Input({required: true}) libraryId!: number;
  @Input({required: true}) heading!: string;
  @Input() queryParam: FilterField = FilterField.None;
  @ContentChild('titleTemplate') titleTemplate!: TemplateRef<any>;
  @ContentChild('itemTemplate') itemTemplate?: TemplateRef<any>;

  private readonly filterUtilitiesService = inject(FilterUtilitiesService);
  protected readonly TagBadgeCursor = TagBadgeCursor;


  goTo(queryParamName: FilterField, filter: any) {
    if (queryParamName === FilterField.None) return;
    this.filterUtilitiesService.applyFilter(['library', this.libraryId], queryParamName, FilterComparison.Equal, filter);
  }
}
