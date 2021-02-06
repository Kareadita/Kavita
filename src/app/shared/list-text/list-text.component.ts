import { Component, Input, OnInit } from '@angular/core';

@Component({
  selector: 'app-list-text',
  templateUrl: './list-text.component.html',
  styleUrls: ['./list-text.component.scss']
})
export class ListTextComponent implements OnInit {

  @Input() list: any[] = [];
  @Input() key: string | undefined;
  constructor() { }

  ngOnInit(): void {
  }

}
