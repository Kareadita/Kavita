import { Component, Input, OnInit } from '@angular/core';
import { NgbModal, NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { Series } from 'src/app/_models/series';
import { SeriesService } from 'src/app/_services/series.service';

@Component({
  selector: 'app-edit-series-modal',
  templateUrl: './edit-series-modal.component.html',
  styleUrls: ['./edit-series-modal.component.scss']
})
export class EditSeriesModalComponent implements OnInit {

  @Input() series!: Series;

  constructor(private modalService: NgbModal, public modal: NgbActiveModal, private seriesService: SeriesService) { }

  ngOnInit(): void {
  }

  close() {
    this.modal.close();
  }

}
