import {Component, inject, OnInit} from '@angular/core';
import {LicenseService} from "../../_services/license.service";
import {take} from "rxjs/operators";
import {Router} from "@angular/router";
import {
  KavitaplusMetadataBreakdownStatsComponent
} from "../../statistics/_components/kavitaplus-metadata-breakdown-stats/kavitaplus-metadata-breakdown-stats.component";
import {TranslocoDirective} from "@jsverse/transloco";

@Component({
  selector: 'app-manage-matched-metadata',
  standalone: true,
  imports: [
    KavitaplusMetadataBreakdownStatsComponent,
    TranslocoDirective
  ],
  templateUrl: './manage-matched-metadata.component.html',
  styleUrl: './manage-matched-metadata.component.scss'
})
export class ManageMatchedMetadataComponent implements OnInit {
  private readonly licenseService = inject(LicenseService);
  private readonly router = inject(Router);


  constructor() {
    this.licenseService.hasValidLicense$.pipe(take(1)).subscribe(license => {
      if (!license) {
        // Navigate home
        this.router.navigate(['/']);
      }
    });
  }

  ngOnInit() {

  }

}
