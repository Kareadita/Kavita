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
export class ColorscapeService {
  private colorSubject = new BehaviorSubject<ColorSpaceRGBA | null>(null);
  public colors$ = this.colorSubject.asObservable();

  private minDuration = 1000; // minimum duration
  private maxDuration = 4000; // maximum duration

  constructor(@Inject(DOCUMENT) private document: Document) {

  }

  /**
   * Sets a color scape for the active theme
   * @param primaryColor
   * @param complementaryColor
   */
  setColorScape(primaryColor: string, complementaryColor: string | null = null) {
    if (this.getCssVariable('--colorscape-enabled') === 'false') {
      return;
    }

    const elem = this.document.querySelector('#backgroundCanvas');

    if (!elem) {
      return;
    }

    const newColors: ColorSpace = primaryColor ?
      this.generateBackgroundColors(primaryColor, complementaryColor, this.isDarkTheme()) :
      this.defaultColors();

    const newColorsRGBA = this.convertColorsToRGBA(newColors);
    const oldColors = this.colorSubject.getValue() || this.convertColorsToRGBA(this.defaultColors());
    const duration = this.calculateTransitionDuration(oldColors, newColorsRGBA);


    // Check if the colors we are transitioning to are visually equal
    if (this.areColorSpacesVisuallyEqual(oldColors, newColorsRGBA)) {
      return;
    }

    this.animateColorTransition(oldColors, newColorsRGBA, duration);

    this.colorSubject.next(newColorsRGBA);
  }

  private areColorSpacesVisuallyEqual(color1: ColorSpaceRGBA, color2: ColorSpaceRGBA, threshold: number = 0): boolean {
    return this.areRGBAColorsVisuallyEqual(color1.primary, color2.primary, threshold) &&
      this.areRGBAColorsVisuallyEqual(color1.lighter, color2.lighter, threshold) &&
      this.areRGBAColorsVisuallyEqual(color1.darker, color2.darker, threshold) &&
      this.areRGBAColorsVisuallyEqual(color1.complementary, color2.complementary, threshold);
  }

  private areRGBAColorsVisuallyEqual(color1: RGBAColor, color2: RGBAColor, threshold: number = 0): boolean {
    return Math.abs(color1.r - color2.r) <= threshold &&
      Math.abs(color1.g - color2.g) <= threshold &&
      Math.abs(color1.b - color2.b) <= threshold &&
      Math.abs(color1.a - color2.a) <= threshold / 255;
  }

  private convertColorsToRGBA(colors: ColorSpace): ColorSpaceRGBA {
    return {
      primary: this.parseColorToRGBA(colors.primary),
      lighter: this.parseColorToRGBA(colors.lighter),
      darker: this.parseColorToRGBA(colors.darker),
      complementary: this.parseColorToRGBA(colors.complementary)
    };
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

  private calculateTransitionDuration(oldColors: ColorSpaceRGBA, newColors: ColorSpaceRGBA): number {
    const colorKeys: (keyof ColorSpaceRGBA)[] = ['primary', 'lighter', 'darker', 'complementary'];
    let totalDistance = 0;

    for (const key of colorKeys) {
      const oldRGB = this.rgbaToRgb(oldColors[key]);
      const newRGB = this.rgbaToRgb(newColors[key]);
      totalDistance += this.calculateColorDistance(oldRGB, newRGB);
    }

    // Normalize the total distance and map it to our duration range
    const normalizedDistance = Math.min(totalDistance / (255 * 3 * 4), 1); // Max possible distance is 255*3*4
    const duration = this.minDuration + normalizedDistance * (this.maxDuration - this.minDuration);

    return Math.round(duration);
  }

  private rgbaToRgb(rgba: RGBAColor): RGB {
    return { r: rgba.r, g: rgba.g, b: rgba.b };
  }

  private calculateColorDistance(rgb1: RGB, rgb2: RGB): number {
    return Math.sqrt(
      Math.pow(rgb2.r - rgb1.r, 2) +
      Math.pow(rgb2.g - rgb1.g, 2) +
      Math.pow(rgb2.b - rgb1.b, 2)
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

  private animateColorTransition(oldColors: ColorSpaceRGBA, newColors: ColorSpaceRGBA, duration: number) {
    const startTime = performance.now();

    const animate = (currentTime: number) => {
      const elapsedTime = currentTime - startTime;
      const progress = Math.min(elapsedTime / duration, 1);

      const interpolatedColors: ColorSpaceRGBA = {
        primary: this.interpolateRGBAColor(oldColors.primary, newColors.primary, progress),
        lighter: this.interpolateRGBAColor(oldColors.lighter, newColors.lighter, progress),
        darker: this.interpolateRGBAColor(oldColors.darker, newColors.darker, progress),
        complementary: this.interpolateRGBAColor(oldColors.complementary, newColors.complementary, progress)
      };

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

  private setColorsImmediately(colors: ColorSpaceRGBA) {
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

  private generateBackgroundColors(primaryColor: string, secondaryColor: string | null = null, isDarkTheme: boolean = true): ColorSpace {
    const primary = this.hexToRgb(primaryColor);
    const secondary = secondaryColor ? this.hexToRgb(secondaryColor) : this.calculateComplementaryRgb(primary);

    const primaryHSL = this.rgbToHsl(primary);
    const secondaryHSL = this.rgbToHsl(secondary);

    if (isDarkTheme) {
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
    } else {
      // NOTE: Light themes look bad in general with this system.
      const lighterHSL = { ...primaryHSL };
      lighterHSL.s = Math.max(lighterHSL.s - 0.3, 0);
      lighterHSL.l = Math.min(lighterHSL.l + 0.5, 0.95);

      const darkerHSL = { ...primaryHSL };
      darkerHSL.s = Math.max(darkerHSL.s - 0.1, 0);
      darkerHSL.l = Math.min(darkerHSL.l + 0.3, 0.9);

      const complementaryHSL = this.adjustHue(primaryHSL, 180);
      complementaryHSL.s = Math.max(complementaryHSL.s - 0.2, 0);
      complementaryHSL.l = Math.min(complementaryHSL.l + 0.4, 0.9);

      return {
        primary: this.rgbToHex(primary),
        lighter: this.rgbToHex(this.hslToRgb(lighterHSL)),
        darker: this.rgbToHex(this.hslToRgb(darkerHSL)),
        complementary: this.rgbToHex(this.hslToRgb(complementaryHSL))
      };
    }
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

  private unsetPageColorOverrides() {
    Array.from(this.document.head.children).filter(el => el.tagName === 'STYLE' && el.id.toLowerCase() === colorScapeSelector).forEach(c => this.document.head.removeChild(c));
  }
}
