import { Component, Input, OnInit } from '@angular/core';
import { NgbModal, NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import { Chapter } from 'src/app/_models/chapter';
import { MangaFormat } from 'src/app/_models/manga-format';
import { Series } from 'src/app/_models/series';
import { Volume } from 'src/app/_models/volume';
import { SeriesService } from 'src/app/_services/series.service';
import { UtilityService } from '../../_services/utility.service';

@Component({
  selector: 'app-series-card-details',
  templateUrl: './series-card-details.component.html',
  styleUrls: ['./series-card-details.component.scss']
})
export class SeriesCardDetailsComponent implements OnInit {

  @Input() parentName = '';
  @Input() data!: Series;
  seriesVolumes: any[] = [];
  imageStyles = {width: '74px'};
  isLoadingVolumes = false;

  isCollapsed = true;
  volumeCollapsed: any = {};


  constructor(private modalService: NgbModal, public modal: NgbActiveModal, private seriesService: SeriesService, public utilityService: UtilityService) { }

  ngOnInit(): void {
    this.isLoadingVolumes = true;
    this.seriesService.getVolumes(this.data.id).subscribe(volumes => {
      this.seriesVolumes = volumes;
      this.isLoadingVolumes = false;

      volumes.forEach(v => {
        this.volumeCollapsed[v.name] = true;
      });
      this.seriesVolumes.forEach(vol => {
        vol.volumeFiles = vol.chapters?.sort(this.utilityService.sortChapters).map((c: Chapter) => c.files.map((f: any) => {
          f.chapter = c.number;
          return f;
        })).flat();
      });
      console.log('volumes:', this.seriesVolumes);
    });
    console.log('data: ', this.data);
  }

  close() {
    this.modal.close();
  }

  formatChapterNumber(chapter: Chapter) {
    if (chapter.number === '0') {
      return '1';
    }
    return chapter.number;
  }
}
