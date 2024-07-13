import { Injectable, Inject } from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { BehaviorSubject } from 'rxjs';

interface ColorSpace {
  primary: string;
  lighter: string;
  darker: string;
  complementary: string;
}

interface RGBAColor {
  r: number;
  g: number;
  b: number;
  a: number;
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

    let newColors: ColorSpace;
    if (primaryColor === '' || primaryColor === null || primaryColor === undefined) {
      newColors = this.defaultColors();
    } else {
      newColors = this.generateBackgroundColors(primaryColor, complementaryColor, this.isDarkTheme());
    }

    // Convert hex colors to RGB format
    const newColorsRGBA = this.convertColorsToRGBA(newColors);

    const oldColors = this.colorSubject.getValue() || this.convertColorsToRGBA(this.defaultColors());
    const duration = this.calculateTransitionDuration(oldColors, newColorsRGBA);

    //console.log('Transitioning colors from ', oldColors, ' to ', newColorsRGBA);
    this.animateColorTransition(oldColors, newColorsRGBA, duration);

    this.colorSubject.next(newColorsRGBA);
  }

  private convertColorsToRGBA(colors: ColorSpace): { [key: string]: RGBAColor } {
    const convertedColors: { [key: string]: RGBAColor } = {};
    for (const [key, value] of Object.entries(colors)) {
      convertedColors[key] = this.hexToRGBA(value);
    }
    return convertedColors;
  }

  private calculateTransitionDuration(oldColors: { [key: string]: RGBAColor }, newColors: { [key: string]: RGBAColor }): number {
    const colorKeys = ['primary', 'lighter', 'darker', 'complementary'];
    let totalDistance = 0;

    for (const key of colorKeys) {
      const oldRGB = [oldColors[key].r, oldColors[key].g, oldColors[key].b];
      const newRGB = [newColors[key].r, newColors[key].g, newColors[key].b];
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

  private hexToRGBA(hex: string, opacity: number = 1): RGBAColor {
    if (hex.startsWith('#')) {
      const r = parseInt(hex.slice(1, 3), 16);
      const g = parseInt(hex.slice(3, 5), 16);
      const b = parseInt(hex.slice(5, 7), 16);
      return { r, g, b, a: opacity };
    }
    return { r: 0, g: 0, b: 0, a: opacity }; // Fallback to opaque black if not a hex color
  }

  private defaultColors() {
    return {
      primary: this.getCssVariable('--colorscape-primary-default-color'),
      lighter: this.getCssVariable('--colorscape-lighter-default-color'),
      darker: this.getCssVariable('--colorscape-darker-default-color'),
      complementary: this.getCssVariable('--colorscape-complementary-default-color'),
    }
  }


  private animateColorTransition(oldColors: { [key: string]: RGBAColor }, newColors: { [key: string]: RGBAColor }, duration: number) {
    const startTime = performance.now();

    const animate = (currentTime: number) => {
      const elapsedTime = currentTime - startTime;
      const progress = Math.min(elapsedTime / duration, 1);

      const interpolatedColors: { [key: string]: RGBAColor } = {};
      for (const key in oldColors) {
        interpolatedColors[key] = this.interpolateRGBAColor(oldColors[key], newColors[key], progress);
      }

      this.setColorsImmediately(interpolatedColors);

      if (progress < 1) {
        requestAnimationFrame(animate);
      }
    };

    requestAnimationFrame(animate);
  }

  private interpolateRGBAColor(color1: RGBAColor, color2: RGBAColor, progress: number): RGBAColor {
    return {
      r: Math.round(color1.r + (color2.r - color1.r) * progress),
      g: Math.round(color1.g + (color2.g - color1.g) * progress),
      b: Math.round(color1.b + (color2.b - color1.b) * progress),
      a: color1.a + (color2.a - color1.a) * progress
    };
  }

  private setColorsImmediately(colors: { [key: string]: RGBAColor }) {
    const defaultBackgroundColors = this.defaultColors();
    this.injectStyleElement(colorScapeSelector, `
      :root, :root .default {
        --colorscape-primary-color: ${this.rgbaToString(colors.primary)};
        --colorscape-lighter-color: ${this.rgbaToString(colors.lighter)};
        --colorscape-darker-color: ${this.rgbaToString(colors.darker)};
        --colorscape-complementary-color: ${this.rgbaToString(colors.complementary)};
        --colorscape-primary-alpha-color: ${this.rgbaToString({ ...colors.primary, a: 0 })};
        --colorscape-lighter-alpha-color: ${this.rgbaToString({ ...colors.lighter, a: 0 })};
        --colorscape-darker-alpha-color: ${this.rgbaToString({ ...colors.darker, a: 0 })};
        --colorscape-complementary-alpha-color: ${this.rgbaToString({ ...colors.complementary, a: 0 })};
      }
    `);
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

  private rgbaToString(color: RGBAColor): string {
    return `rgba(${color.r}, ${color.g}, ${color.b}, ${color.a})`;
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
