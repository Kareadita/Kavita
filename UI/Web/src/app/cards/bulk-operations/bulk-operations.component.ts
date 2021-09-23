import { Component, OnInit } from '@angular/core';
import { BulkSelectionService } from '../bulk-selection.service';

@Component({
  selector: 'app-bulk-operations',
  templateUrl: './bulk-operations.component.html',
  styleUrls: ['./bulk-operations.component.scss']
})
export class BulkOperationsComponent implements OnInit {

  constructor(public bulkSelectionService: BulkSelectionService) { }

  ngOnInit(): void {
  }

}
