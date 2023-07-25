import {ChangeDetectionStrategy, Component, ContentChild, inject, Input, OnInit, TemplateRef} from '@angular/core';
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
export class MetadataDetailComponent implements OnInit {

  @Input({required: true}) tags: Array<any> = [];
  @Input({required: true}) libraryId!: number;
  @Input({required: true}) heading!: string;
  @Input({required: true}) queryParam!: FilterQueryParam;
  @ContentChild('titleTemplate') titleTemplate!: TemplateRef<any>;
  @ContentChild('itemTemplate') itemTemplate?: TemplateRef<any>;

  private readonly router = inject(Router);
  protected readonly TagBadgeCursor = TagBadgeCursor;

  ngOnInit() {
    console.log(this.itemTemplate)
  }

  goTo(queryParamName: FilterQueryParam, filter: any) {
    let params: any = {};
    params[queryParamName] = filter;
    params[FilterQueryParam.Page] = 1;
    this.router.navigate(['library', this.libraryId], {queryParams: params});
  }
}
