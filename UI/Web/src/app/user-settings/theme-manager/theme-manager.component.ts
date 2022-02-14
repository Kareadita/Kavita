import { Component, OnDestroy, OnInit } from '@angular/core';
import { distinctUntilChanged, Subject, takeUntil } from 'rxjs';
import { ThemeService } from 'src/app/theme.service';
import { SiteTheme, ThemeProvider } from 'src/app/_models/preferences/site-theme';

@Component({
  selector: 'app-theme-manager',
  templateUrl: './theme-manager.component.html',
  styleUrls: ['./theme-manager.component.scss']
})
export class ThemeManagerComponent implements OnInit, OnDestroy {

  themes: Array<SiteTheme> = [];
  currentTheme!: SiteTheme;

  private readonly onDestroy = new Subject<void>();

  get ThemeProvider() {
    return ThemeProvider;
  }

  constructor(public themeService: ThemeService) {
    themeService.currentTheme$.pipe(takeUntil(this.onDestroy), distinctUntilChanged()).subscribe(theme => {
      if (theme) {
        this.currentTheme = theme;
      }
    });
  }

  ngOnInit(): void {
    this.themeService.getThemes().subscribe(themes => {
      this.themes = themes;
    });
  }

  ngOnDestroy(): void {
    this.onDestroy.next();
    this.onDestroy.complete();
  }

}
