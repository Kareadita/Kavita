import { Component, OnInit } from '@angular/core';
import { UpdateVersionEvent } from 'src/app/_models/events/update-version-event';
import { ServerService } from 'src/app/_services/server.service';

@Component({
  selector: 'app-changelog',
  templateUrl: './changelog.component.html',
  styleUrls: ['./changelog.component.scss']
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
