import { Component, Input, OnInit } from '@angular/core';
import { Person } from '../../_models/person';

@Component({
  selector: 'app-person-badge',
  templateUrl: './person-badge.component.html',
  styleUrls: ['./person-badge.component.scss']
})
export class PersonBadgeComponent implements OnInit {

  @Input() person!: Person;

  constructor() { }

  ngOnInit(): void {
  }

}
