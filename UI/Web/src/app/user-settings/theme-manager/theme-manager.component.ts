import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  DestroyRef,
  inject,
} from '@angular/core';
import { ToastrService } from 'ngx-toastr';
import { distinctUntilChanged, take } from 'rxjs';
import { ThemeService } from 'src/app/_services/theme.service';
import { SiteTheme } from 'src/app/_models/preferences/site-theme';
import { User } from 'src/app/_models/user';
import { AccountService } from 'src/app/_services/account.service';
import {takeUntilDestroyed} from "@angular/core/rxjs-interop";
import { SiteThemeProviderPipe } from '../_pipes/site-theme-provider.pipe';
import { SentenceCasePipe } from '../../pipe/sentence-case.pipe';
import { NgIf, NgFor, AsyncPipe } from '@angular/common';
import {TranslocoDirective, TranslocoService} from "@ngneat/transloco";

@Component({
    selector: 'app-theme-manager',
    templateUrl: './theme-manager.component.html',
    styleUrls: ['./theme-manager.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    standalone: true,
    imports: [NgIf, NgFor, AsyncPipe, SentenceCasePipe, SiteThemeProviderPipe, TranslocoDirective]
})
export class ThemeManagerComponent {

  currentTheme: SiteTheme | undefined;
  isAdmin: boolean = false;
  user: User | undefined;
  private readonly destroyRef = inject(DestroyRef);
  private readonly translocService = inject(TranslocoService);


  constructor(public themeService: ThemeService, private accountService: AccountService,
    private toastr: ToastrService, private readonly cdRef: ChangeDetectorRef) {

    themeService.currentTheme$.pipe(takeUntilDestroyed(this.destroyRef), distinctUntilChanged()).subscribe(theme => {
      this.currentTheme = theme;
      this.cdRef.markForCheck();
    });

    accountService.currentUser$.pipe(take(1)).subscribe(user => {
      if (user) {
        this.user = user;
        this.isAdmin = accountService.hasAdminRole(user);
        this.cdRef.markForCheck();
      }
    });
  }

  applyTheme(theme: SiteTheme) {

    if (this.user) {
      const pref = Object.assign({}, this.user.preferences);
      pref.theme = theme;
      this.accountService.updatePreferences(pref).subscribe(updatedPref => {
        if (this.user) {
          this.user.preferences = updatedPref;
        }
        this.themeService.setTheme(theme.name);
        this.cdRef.markForCheck();
      });
    }

  }

  updateDefault(theme: SiteTheme) {
    this.themeService.setDefault(theme.id).subscribe(() => {
      this.toastr.success(this.translocService.translate('theme-manager.updated-toastr', {name: theme.name}));
    });
  }

  scan() {
    this.themeService.scan().subscribe(() => {
      this.toastr.info(this.translocService.translate('theme-manager.scan-queued'));
    });
  }
}
