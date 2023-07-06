import { Component, OnInit } from '@angular/core';
import { UpdateVersionEvent } from 'src/app/_models/events/update-version-event';
import { ServerService } from 'src/app/_services/server.service';
import { LoadingComponent } from '../../../shared/loading/loading.component';
import { ReadMoreComponent } from '../../../shared/read-more/read-more.component';
import { NgFor, NgIf, DatePipe } from '@angular/common';

@Component({
    selector: 'app-changelog',
    templateUrl: './changelog.component.html',
    styleUrls: ['./changelog.component.scss'],
    standalone: true,
    imports: [NgFor, NgIf, ReadMoreComponent, LoadingComponent, DatePipe]
})
export class ChangelogComponent implements OnInit {

  updates: Array<UpdateVersionEvent> = [];
  isLoading: boolean = true;

  constructor(private serverService: ServerService) { }

  ngOnInit(): void {

    this.serverService.getChangelog().subscribe(updates => {
      this.updates = updates;
      this.isLoading = false;
    });
    

    
  }
}
