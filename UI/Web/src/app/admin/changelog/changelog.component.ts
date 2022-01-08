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
  installedVersion: string = '';

  constructor(private serverService: ServerService) { }

  ngOnInit(): void {

    this.serverService.getServerInfo().subscribe(info => {
      this.installedVersion = info.kavitaVersion;
      this.serverService.getChangelog().subscribe(updates => {
        this.updates = updates;
        this.isLoading = false;

        if (this.updates.filter(u => u.updateVersion === this.installedVersion).length === 0) {
          // User is on a nightly version. Tell them the last stable is installed
          this.installedVersion = this.updates[0].updateVersion;
        }
      });
    });
    

    
  }
}
