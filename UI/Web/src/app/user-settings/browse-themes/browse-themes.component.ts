import {ChangeDetectionStrategy, ChangeDetectorRef, Component, DestroyRef, inject} from '@angular/core';
import {NgbActiveModal} from "@ng-bootstrap/ng-bootstrap";
import {ThemeService} from "../../_services/theme.service";
import {DownloadableSiteTheme} from "../../_models/theme/downloadable-site-theme";
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import {ImageComponent} from "../../shared/image/image.component";
import {TranslocoDirective} from "@ngneat/transloco";
import {LoadingComponent} from "../../shared/loading/loading.component";

@Component({
  selector: 'app-browse-themes',
  standalone: true,
  imports: [
    ImageComponent,
    TranslocoDirective,
    LoadingComponent
  ],
  templateUrl: './browse-themes.component.html',
  styleUrl: './browse-themes.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BrowseThemesComponent {
  protected readonly ngbModal = inject(NgbActiveModal);
  private readonly themeService = inject(ThemeService);
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly destroyRef = inject(DestroyRef);

  isLoading = true;
  downloadableThemes: Array<DownloadableSiteTheme> = [];

  constructor() {
    this.themeService.getDownloadableThemes().pipe(takeUntilDestroyed(this.destroyRef)).subscribe(res => {
      this.downloadableThemes = res;
      this.isLoading = false;
      this.cdRef.markForCheck();
    });
  }

  download(theme: DownloadableSiteTheme) {
    this.themeService.downloadTheme(theme).subscribe(res => {

    });
  }


}
