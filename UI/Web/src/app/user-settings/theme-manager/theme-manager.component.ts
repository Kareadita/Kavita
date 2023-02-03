import { ChangeDetectionStrategy, ChangeDetectorRef, Component, OnDestroy, OnInit } from '@angular/core';
import { ToastrService } from 'ngx-toastr';
import { distinctUntilChanged, Subject, take, takeUntil } from 'rxjs';
import { ThemeService } from 'src/app/_services/theme.service';
import { SiteTheme, ThemeProvider } from 'src/app/_models/preferences/site-theme';
import { User } from 'src/app/_models/user';
import { AccountService } from 'src/app/_services/account.service';

@Component({
  selector: 'app-theme-manager',
  templateUrl: './theme-manager.component.html',
  styleUrls: ['./theme-manager.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ThemeManagerComponent implements OnDestroy {

  currentTheme: SiteTheme | undefined;
  isAdmin: boolean = false;
  user: User | undefined;

  private readonly onDestroy = new Subject<void>();

  get ThemeProvider() {
    return ThemeProvider;
  }

  constructor(public themeService: ThemeService, private accountService: AccountService, 
    private toastr: ToastrService, private readonly cdRef: ChangeDetectorRef) {

    themeService.currentTheme$.pipe(takeUntil(this.onDestroy), distinctUntilChanged()).subscribe(theme => {
      this.currentTheme = theme;
    });

    accountService.currentUser$.pipe(take(1)).subscribe(user => {
      if (user) {
        this.user = user;
        this.isAdmin = accountService.hasAdminRole(user);
        this.cdRef.markForCheck();
      }
    });
  }

  ngOnDestroy(): void {
    this.onDestroy.next();
    this.onDestroy.complete();
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
      this.toastr.success('Site default has been updated to ' + theme.name);
    });
  }

  scan() {
    this.themeService.scan().subscribe(() => {
      this.toastr.info('A site theme scan has been queued');
    });
  }
}
