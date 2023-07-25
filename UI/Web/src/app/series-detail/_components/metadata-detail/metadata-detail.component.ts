import {ChangeDetectionStrategy, Component, ContentChild, inject, Input, TemplateRef} from '@angular/core';
import {CommonModule} from '@angular/common';
import {A11yClickDirective} from "../../../shared/a11y-click.directive";
import {BadgeExpanderComponent} from "../../../shared/badge-expander/badge-expander.component";
import {TagBadgeComponent, TagBadgeCursor} from "../../../shared/tag-badge/tag-badge.component";
import {FilterQueryParam} from "../../../shared/_services/filter-utilities.service";
import {Router} from "@angular/router";

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
  @Input() queryParam: FilterQueryParam = FilterQueryParam.None;
  @ContentChild('titleTemplate') titleTemplate!: TemplateRef<any>;
  @ContentChild('itemTemplate') itemTemplate?: TemplateRef<any>;

  private readonly router = inject(Router);
  protected readonly TagBadgeCursor = TagBadgeCursor;


  goTo(queryParamName: FilterQueryParam, filter: any) {
    if (queryParamName === FilterQueryParam.None) return;
    let params: any = {};
    params[queryParamName] = filter;
    params[FilterQueryParam.Page] = 1;
    this.router.navigate(['library', this.libraryId], {queryParams: params});
  }
}
