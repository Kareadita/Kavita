import { ChangeDetectionStrategy, Component, Input, OnInit } from '@angular/core';
import { Person } from '../../_models/metadata/person';

@Component({
  selector: 'app-person-badge',
  templateUrl: './person-badge.component.html',
  styleUrls: ['./person-badge.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class PersonBadgeComponent implements OnInit {

  @Input() person!: Person;

  constructor() { }

  ngOnInit(): void {
  }

}
