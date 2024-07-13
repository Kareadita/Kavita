import { Injectable, Inject } from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { BehaviorSubject } from 'rxjs';

interface ColorSpace {
  primary: string;
  lighter: string;
  darker: string;
  complementary: string;
}

const colorScapeSelector = 'colorscape';

@Injectable({
  providedIn: 'root'
})
export class ColorTransitionService {
  private colorSubject = new BehaviorSubject<any>(null);
  public colors$ = this.colorSubject.asObservable();

  private minDuration = 1000; // 1 second minimum duration
  private maxDuration = 4000; // 3 seconds maximum duration

  constructor(@Inject(DOCUMENT) private document: Document) {}

  setColorScape(primaryColor: string, complementaryColor: string | null = null) {
    if (this.getCssVariable('--colorscape-enabled') === 'false') {
      return;
    }

    const elem = this.document.querySelector('#backgroundCanvas');

    if (!elem) {
      return;
    }

    let newColors;
    if (primaryColor === '' || primaryColor === null || primaryColor === undefined) {
      newColors = this.defaultColors();
    } else {
      newColors = this.generateBackgroundColors(primaryColor, complementaryColor, this.isDarkTheme());
      // Convert hex colors to RGB format
      newColors = this.convertColorsToRGB(newColors);
    }

    const oldColors = this.colorSubject.getValue() || this.defaultColors();
    const duration = this.calculateTransitionDuration(oldColors, newColors);

    //console.log('Transitioning colors from ', oldColors, ' to ', newColors);
    this.animateColorTransition(oldColors, newColors, duration);

    this.colorSubject.next(newColors);
  }

  private convertColorsToRGB(colors: ColorSpace): any {
    const convertedColors: any = {};
    for (const [key, value] of Object.entries(colors)) {
      convertedColors[key] = this.hexToRGB(value as string);
    }
    return convertedColors;
  }

  private calculateTransitionDuration(oldColors: ColorSpace, newColors: ColorSpace): number {
    const colorKeys = ['primary', 'lighter', 'darker', 'complementary'];
    let totalDistance = 0;

    for (const key of colorKeys) {
      const oldRGB = this.getRGBValues((oldColors as any)[key]);
      const newRGB = this.getRGBValues((newColors as any)[key]);
      totalDistance += this.calculateColorDistance(oldRGB, newRGB);
    }

    // Normalize the total distance and map it to our duration range
    const normalizedDistance = Math.min(totalDistance / (255 * 3 * 4), 1); // Max possible distance is 255*3*4
    const duration = this.minDuration + normalizedDistance * (this.maxDuration - this.minDuration);

    return Math.round(duration);
  }

  private calculateColorDistance(rgb1: number[], rgb2: number[]): number {
    return Math.sqrt(
      Math.pow(rgb2[0] - rgb1[0], 2) +
      Math.pow(rgb2[1] - rgb1[1], 2) +
      Math.pow(rgb2[2] - rgb1[2], 2)
    );
  }

  private hexToRGB(hex: string): string {
    if (hex.startsWith('#')) {
      const r = parseInt(hex.slice(1, 3), 16);
      const g = parseInt(hex.slice(3, 5), 16);
      const b = parseInt(hex.slice(5, 7), 16);
      return `rgb(${r}, ${g}, ${b})`;
    }
    return hex; // Return as is if it's not a hex color
  }

  private defaultColors() {
    return {
      primary: this.getCssVariable('--colorscape-primary-default-color'),
      lighter: this.getCssVariable('--colorscape-lighter-default-color'),
      darker: this.getCssVariable('--colorscape-darker-default-color'),
      complementary: this.getCssVariable('--colorscape-complementary-default-color'),
    }
  }


  private animateColorTransition(oldColors: ColorSpace, newColors: ColorSpace, duration: number) {
    const startTime = performance.now();

    const animate = (currentTime: number) => {
      const elapsedTime = currentTime - startTime;
      const progress = Math.min(elapsedTime / duration, 1);

      const interpolatedColors = {
        primary: this.interpolateColor(oldColors.primary, newColors.primary, progress),
        darker: this.interpolateColor(oldColors.darker, newColors.darker, progress),
        lighter: this.interpolateColor(oldColors.lighter, newColors.lighter, progress),
        complementary: this.interpolateColor(oldColors.complementary, newColors.complementary, progress)
      };

      this.setColorsImmediately(interpolatedColors);

      if (progress < 1) {
        requestAnimationFrame(animate);
      }
    };

    requestAnimationFrame(animate);
  }

  private setColorsImmediately(colors: ColorSpace) {
    this.injectStyleElement(colorScapeSelector, `
      :root, :root .default {
        --colorscape-primary-color: ${colors.primary};
        --colorscape-lighter-color: ${colors.lighter};
        --colorscape-darker-color: ${colors.darker};
        --colorscape-complementary-color: ${colors.complementary};
      }
    `);
  }

  private interpolateColor(color1: string, color2: string, progress: number): string {
    const [r1, g1, b1] = this.getRGBValues(color1);
    const [r2, g2, b2] = this.getRGBValues(color2);

    const r = Math.round(r1 + (r2 - r1) * progress);
    const g = Math.round(g1 + (g2 - g1) * progress);
    const b = Math.round(b1 + (b2 - b1) * progress);

    return `rgb(${r}, ${g}, ${b})`;
  }

  private getRGBValues(color: string): number[] {
    const matches = color.match(/\d+/g);
    if (matches) {
      return matches.map(Number);
    }
    console.error('Invalid color format:', color);
    return [0, 0, 0]; // Fallback to black if color format is not recognized
  }

  // Include your existing methods here:
  private generateBackgroundColors(primaryColor: string, secondaryColor: string | null = null, leanDark: boolean = true) {
    const lightenOffsetPrimary = parseInt(this.getCssVariable('--colorscape-primary-lighten-offset'), 10);
    const darkenOffsetPrimary = parseInt(this.getCssVariable('--colorscape-primary-darken-offset'), 10);

    const lightenOffsetSecondary = parseInt(this.getCssVariable('--colorscape-primary-lighten-offset'), 10);
    const darkenOffsetSecondary = parseInt(this.getCssVariable('--colorscape-primary-darken-offset'), 10);

    const compColor = secondaryColor ? secondaryColor : this.calculateComplementaryColor(primaryColor);

    const lighterColor = this.lightenDarkenColor(compColor, lightenOffsetPrimary);
    const darkerColor = this.lightenDarkenColor(primaryColor, darkenOffsetPrimary);

    // let compColor = secondaryColor ? secondaryColor : this.calculateComplementaryColor(primaryColor);
    // if (leanDark) {
    //   compColor = this.lightenDarkenColor(compColor, lightenOffsetSecondary); // Make it darker
    // } else {
    //   compColor = this.lightenDarkenColor(compColor, darkenOffsetSecondary);  // Make it lighter
    // }

    return {primary: primaryColor, darker: darkerColor, lighter: lighterColor, complementary: compColor};
  }

  private getCssVariable(variableName: string): string {
    return getComputedStyle(this.document.body).getPropertyValue(variableName).trim();
  }

  private isDarkTheme(): boolean {
    return getComputedStyle(this.document.body).getPropertyValue('--color-scheme').trim().toLowerCase() === 'dark';
  }

  private injectStyleElement(id: string, styles: string) {
    let styleElement = this.document.getElementById(id);
    if (!styleElement) {
      styleElement = this.document.createElement('style');
      styleElement.id = id;
      this.document.head.appendChild(styleElement);
    }
    styleElement.textContent = styles;
  }

  private lightenDarkenColor(hex: string, amt: number) {
    let num = parseInt(hex.slice(1), 16);
    let r = (num >> 16) + amt;
    let g = ((num >> 8) & 0x00FF) + amt;
    let b = (num & 0x0000FF) + amt;

    r = Math.max(Math.min(255, r), 0);
    g = Math.max(Math.min(255, g), 0);
    b = Math.max(Math.min(255, b), 0);

    let newColor = (r << 16) | (g << 8) | b;
    return `#${(0x1000000 + newColor).toString(16).slice(1).toUpperCase()}`;
  }

  private calculateComplementaryColor(hex: string): string {
    const num = parseInt(hex.slice(1), 16);
    let compNum = 0xFFFFFF ^ num;
    return `#${compNum.toString(16).padStart(6, '0').toUpperCase()}`;
  }

  private unsetPageColorOverrides() {
    Array.from(this.document.head.children).filter(el => el.tagName === 'STYLE' && el.id.toLowerCase() === colorScapeSelector).forEach(c => this.document.head.removeChild(c));
  }
}
