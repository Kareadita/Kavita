import { Injectable, Inject } from '@angular/core';
import { DOCUMENT } from '@angular/common';
import { BehaviorSubject } from 'rxjs';

interface ColorSpace {
  primary: string;
  lighter: string;
  darker: string;
  complementary: string;
}

interface ColorSpaceRGBA {
  primary: RGBAColor;
  lighter: RGBAColor;
  darker: RGBAColor;
  complementary: RGBAColor;
}

interface RGBAColor {
  r: number;
  g: number;
  b: number;
  a: number;
}

interface RGB {
  r: number;
  g: number;
  b: number;
}

const colorScapeSelector = 'colorscape';

/**
 * ColorScape handles setting the scape and managing the transitions
 */
@Injectable({
  providedIn: 'root'
})
export class ColorScapeService {
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

    // Check if the colors we are transitioning to are the same
    // if (this.areRGBAColorsEqual(oldColors, newColorsRGBA)) {
    //   return;
    // }

    console.log('Transitioning colors from ', oldColors, ' to ', newColorsRGBA);
    this.animateColorTransition(oldColors, newColorsRGBA, duration);

    this.colorSubject.next(newColorsRGBA);
  }

  /**
   * Are two colors equal that allow for small visual differences not noticeable to the eye
   * @param color1
   * @param color2
   * @param threshold
   * @private
   */
  private areRGBAColorsVisuallyEqual(color1: RGBAColor, color2: RGBAColor, threshold: number = 1): boolean {
    return Math.abs(color1.r - color2.r) <= threshold &&
      Math.abs(color1.g - color2.g) <= threshold &&
      Math.abs(color1.b - color2.b) <= threshold &&
      Math.abs(color1.a - color2.a) <= threshold / 255;
  }

  private areRGBAColorsEqual(color1: RGBAColor, color2: RGBAColor): boolean {
    return color1.r === color2.r &&
      color1.g === color2.g &&
      color1.b === color2.b &&
      color1.a === color2.a;
  }

  private convertColorsToRGBA(colors: ColorSpace): { [key: string]: RGBAColor } {
    const convertedColors: { [key: string]: RGBAColor } = {};
    for (const [key, value] of Object.entries(colors)) {
      convertedColors[key] = this.parseColorToRGBA(value);
    }
    return convertedColors;
  }

  private parseColorToRGBA(color: string): RGBAColor {
    if (color.startsWith('#')) {
      return this.hexToRGBA(color);
    } else if (color.startsWith('rgb')) {
      return this.rgbStringToRGBA(color);
    } else {
      console.warn(`Unsupported color format: ${color}. Defaulting to black.`);
      return { r: 0, g: 0, b: 0, a: 1 };
    }
  }

  private hexToRGBA(hex: string, opacity: number = 1): RGBAColor {
    const result = /^#?([a-f\d]{2})([a-f\d]{2})([a-f\d]{2})$/i.exec(hex);
    return result
      ? {
        r: parseInt(result[1], 16),
        g: parseInt(result[2], 16),
        b: parseInt(result[3], 16),
        a: opacity
      }
      : { r: 0, g: 0, b: 0, a: opacity };
  }

  private rgbStringToRGBA(rgb: string): RGBAColor {
    const matches = rgb.match(/(\d+(\.\d+)?)/g);
    if (matches) {
      return {
        r: parseInt(matches[0], 10),
        g: parseInt(matches[1], 10),
        b: parseInt(matches[2], 10),
        a: matches.length === 4 ? parseFloat(matches[3]) : 1
      };
    }
    return { r: 0, g: 0, b: 0, a: 1 };
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

  // private generateBackgroundColors(primaryColor: string, secondaryColor: string | null = null, leanDark: boolean = true) {
  //   const lightenOffsetPrimary = parseInt(this.getCssVariable('--colorscape-primary-lighten-offset'), 10);
  //   const darkenOffsetPrimary = parseInt(this.getCssVariable('--colorscape-primary-darken-offset'), 10);
  //
  //   const lightenOffsetSecondary = parseInt(this.getCssVariable('--colorscape-primary-lighten-offset'), 10);
  //   const darkenOffsetSecondary = parseInt(this.getCssVariable('--colorscape-primary-darken-offset'), 10);
  //
  //   const compColor = secondaryColor ? secondaryColor : this.calculateComplementaryColor(primaryColor);
  //
  //   const lighterColor = this.lightenDarkenColor(compColor, lightenOffsetPrimary);
  //   const darkerColor = this.lightenDarkenColor(primaryColor, darkenOffsetPrimary);
  //
  //   // let compColor = secondaryColor ? secondaryColor : this.calculateComplementaryColor(primaryColor);
  //   // if (leanDark) {
  //   //   compColor = this.lightenDarkenColor(compColor, lightenOffsetSecondary); // Make it darker
  //   // } else {
  //   //   compColor = this.lightenDarkenColor(compColor, darkenOffsetSecondary);  // Make it lighter
  //   // }
  //
  //   return {primary: primaryColor, darker: darkerColor, lighter: lighterColor, complementary: compColor};
  // }

  private generateBackgroundColors(primaryColor: string, secondaryColor: string | null = null, leanDark: boolean = true): ColorSpace {
    const primary = this.hexToRgb(primaryColor);
    const secondary = secondaryColor ? this.hexToRgb(secondaryColor) : this.calculateComplementaryRgb(primary);

    const primaryHSL = this.rgbToHsl(primary);
    const secondaryHSL = this.rgbToHsl(secondary);

    const lighterHSL = this.adjustHue(secondaryHSL, 30);
    lighterHSL.s = Math.min(lighterHSL.s + 0.2, 1);
    lighterHSL.l = Math.min(lighterHSL.l + 0.1, 0.6);

    const darkerHSL = { ...primaryHSL };
    darkerHSL.l = Math.max(darkerHSL.l - 0.3, 0.1);

    const complementaryHSL = this.adjustHue(primaryHSL, 180);
    complementaryHSL.s = Math.min(complementaryHSL.s + 0.1, 1);
    complementaryHSL.l = Math.max(complementaryHSL.l - 0.2, 0.2);

    return {
      primary: this.rgbToHex(primary),
      lighter: this.rgbToHex(this.hslToRgb(lighterHSL)),
      darker: this.rgbToHex(this.hslToRgb(darkerHSL)),
      complementary: this.rgbToHex(this.hslToRgb(complementaryHSL))
    };
  }

  private hexToRgb(hex: string): RGB {
    const result = /^#?([a-f\d]{2})([a-f\d]{2})([a-f\d]{2})$/i.exec(hex);
    return result ? {
      r: parseInt(result[1], 16),
      g: parseInt(result[2], 16),
      b: parseInt(result[3], 16)
    } : { r: 0, g: 0, b: 0 };
  }

  private rgbToHex(rgb: RGB): string {
    return `#${((1 << 24) + (rgb.r << 16) + (rgb.g << 8) + rgb.b).toString(16).slice(1)}`;
  }

  private rgbToHsl(rgb: RGB): { h: number; s: number; l: number } {
    const r = rgb.r / 255;
    const g = rgb.g / 255;
    const b = rgb.b / 255;
    const max = Math.max(r, g, b);
    const min = Math.min(r, g, b);
    let h = 0;
    let s = 0;
    const l = (max + min) / 2;

    if (max !== min) {
      const d = max - min;
      s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
      switch (max) {
        case r: h = (g - b) / d + (g < b ? 6 : 0); break;
        case g: h = (b - r) / d + 2; break;
        case b: h = (r - g) / d + 4; break;
      }
      h /= 6;
    }

    return { h, s, l };
  }

  private hslToRgb(hsl: { h: number; s: number; l: number }): RGB {
    const { h, s, l } = hsl;
    let r, g, b;

    if (s === 0) {
      r = g = b = l;
    } else {
      const hue2rgb = (p: number, q: number, t: number) => {
        if (t < 0) t += 1;
        if (t > 1) t -= 1;
        if (t < 1/6) return p + (q - p) * 6 * t;
        if (t < 1/2) return q;
        if (t < 2/3) return p + (q - p) * (2/3 - t) * 6;
        return p;
      };

      const q = l < 0.5 ? l * (1 + s) : l + s - l * s;
      const p = 2 * l - q;
      r = hue2rgb(p, q, h + 1/3);
      g = hue2rgb(p, q, h);
      b = hue2rgb(p, q, h - 1/3);
    }

    return {
      r: Math.round(r * 255),
      g: Math.round(g * 255),
      b: Math.round(b * 255)
    };
  }

  private adjustHue(hsl: { h: number; s: number; l: number }, amount: number): { h: number; s: number; l: number } {
    return {
      h: (hsl.h + amount / 360) % 1,
      s: hsl.s,
      l: hsl.l
    };
  }

  private calculateComplementaryRgb(rgb: RGB): RGB {
    const hsl = this.rgbToHsl(rgb);
    const complementaryHsl = this.adjustHue(hsl, 180);
    return this.hslToRgb(complementaryHsl);
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
