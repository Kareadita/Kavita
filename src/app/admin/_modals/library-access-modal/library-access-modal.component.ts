import { Component, Input, OnInit } from '@angular/core';
import { Member } from 'src/app/_models/member';

@Component({
  selector: 'app-library-access-modal',
  templateUrl: './library-access-modal.component.html',
  styleUrls: ['./library-access-modal.component.scss']
})
export class LibraryAccessModalComponent implements OnInit {

  @Input() member: Member | undefined;

  constructor() { }

  ngOnInit(): void {
  }

}
