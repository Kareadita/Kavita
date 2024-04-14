import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import {CommonModule} from "@angular/common";
import {BadgeExpanderComponent} from "../../shared/badge-expander/badge-expander.component";
import {PersonBadgeComponent} from "../../shared/person-badge/person-badge.component";
import {TranslocoDirective} from "@ngneat/transloco";
import {Chapter} from "../../_models/chapter";

@Component({
  selector: 'app-chapter-metadata-detail',
  standalone: true,
  imports: [CommonModule, BadgeExpanderComponent, PersonBadgeComponent, TranslocoDirective],
  templateUrl: './chapter-metadata-detail.component.html',
  styleUrls: ['./chapter-metadata-detail.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ChapterMetadataDetailComponent {
  @Input() chapter: Chapter | undefined;
}
