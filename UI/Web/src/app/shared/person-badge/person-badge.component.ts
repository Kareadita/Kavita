import { ChangeDetectionStrategy, Component, Input } from '@angular/core';
import { Person } from '../../_models/metadata/person';
import {CommonModule} from "@angular/common";

@Component({
  selector: 'app-person-badge',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './person-badge.component.html',
  styleUrls: ['./person-badge.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PersonBadgeComponent {

  @Input({required: true}) person!: Person;

  constructor() { }
}
