import { Injectable, Inject } from '@angular/core';
import { DOCUMENT } from '@angular/common';
import {BehaviorSubject, filter, take, tap, timer} from 'rxjs';
import {NavigationEnd, Router} from "@angular/router";

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

interface RGBAColor {r: number;g: number;b: number;a: number;}
interface RGB { r: number;g: number; b: number; }
interface HSL { h: number; s: number; l: number; }

const colorScapeSelector = 'colorscape';

/**
 * ColorScape handles setting the scape and managing the transitions
 */
@Injectable({
  providedIn: 'root'
})
export class ColorscapeService {
  private colorSubject = new BehaviorSubject<ColorSpaceRGBA | null>(null);
  private colorSeedSubject = new BehaviorSubject<{primary: string, complementary: string | null} | null>(null);
  public readonly colors$ = this.colorSubject.asObservable();

  private minDuration = 1000; // minimum duration
  private maxDuration = 4000; // maximum duration
  private defaultColorspaceDuration = 300; // duration to wait before defaulting back to default colorspace

  constructor(@Inject(DOCUMENT) private document: Document, private readonly router: Router) {
    this.router.events.pipe(
      filter(event => event instanceof NavigationEnd),
      tap(() => this.checkAndResetColorscapeAfterDelay())
    ).subscribe();

  }

  /**
   * Due to changing ColorScape on route end, we might go from one space to another, but the router events resets to default
   * This delays it to see if the colors changed or not in 500ms and if not, then we will reset to default.
   * @private
   */
  private checkAndResetColorscapeAfterDelay() {
    // Capture the current colors at the start of NavigationEnd
    const initialColors = this.colorSubject.getValue();

    // Wait for X ms, then check if colors have changed
    timer(this.defaultColorspaceDuration).pipe(
      take(1), // Complete after the timer emits
      tap(() => {
        const currentColors = this.colorSubject.getValue();
        if (initialColors != null && currentColors != null && this.areColorSpacesVisuallyEqual(initialColors, currentColors)) {
          this.setColorScape(''); // Reset to default if colors haven't changed
        }
      })
    ).subscribe();
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

    // Check the old seed colors and check if they are similar, then avoid a change. In case you scan a series and this re-generates
    const previousColors = this.colorSeedSubject.getValue();
    if (previousColors != null && primaryColor == previousColors.primary) {
      this.colorSeedSubject.next({primary: primaryColor, complementary: complementaryColor});
      return;
    }
    this.colorSeedSubject.next({primary: primaryColor, complementary: complementaryColor});

    // TODO: Check if there is a secondary color and if the color is a strong contrast (opposite on color wheel) to primary

    // If we have a STRONG primary and secondary, generate LEFT/RIGHT orientation
    // If we have only one color, randomize the position of the primary
    // If we have 2 colors, but their contrast isn't STRONG, then use diagonal for Primary and Secondary




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

  private parseColorToRGBA(color: string) {

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

    // Add random variance to the duration
    const durationVariance = this.getRandomInRange(-500, 500);

    return Math.round(duration + durationVariance);
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

  private easeInOutCubic(t: number): number {
    return t < 0.5 ? 4 * t * t * t : 1 - Math.pow(-2 * t + 2, 3) / 2;
  }

  private interpolateRGBAColor(color1: RGBAColor, color2: RGBAColor, progress: number): RGBAColor {

    const easedProgress = this.easeInOutCubic(progress);

    return {
      r: Math.round(color1.r + (color2.r - color1.r) * easedProgress),
      g: Math.round(color1.g + (color2.g - color1.g) * easedProgress),
      b: Math.round(color1.b + (color2.b - color1.b) * easedProgress),
      a: color1.a + (color2.a - color1.a) * easedProgress
    };
  }

  private setColorsImmediately(colors: ColorSpaceRGBA) {
    this.injectStyleElement(colorScapeSelector, `
      :root, :root .default {
        --colorscape-primary-color: ${this.rgbToString(colors.primary)};
        --colorscape-lighter-color: ${this.rgbToString(colors.lighter)};
        --colorscape-darker-color: ${this.rgbToString(colors.darker)};
        --colorscape-complementary-color: ${this.rgbToString(colors.complementary)};
        --colorscape-primary-no-alpha-color: ${this.rgbaToString({ ...colors.primary, a: 0 })};
        --colorscape-lighter-no-alpha-color: ${this.rgbaToString({ ...colors.lighter, a: 0 })};
        --colorscape-darker-no-alpha-color: ${this.rgbaToString({ ...colors.darker, a: 0 })};
        --colorscape-complementary-no-alpha-color: ${this.rgbaToString({ ...colors.complementary, a: 0 })};
        --colorscape-primary-full-alpha-color: ${this.rgbaToString({ ...colors.primary, a: 1 })};
        --colorscape-lighter-full-alpha-color: ${this.rgbaToString({ ...colors.lighter, a: 1 })};
        --colorscape-darker-full-alpha-color: ${this.rgbaToString({ ...colors.darker, a: 1 })};
        --colorscape-complementary-full-alpha-color: ${this.rgbaToString({ ...colors.complementary, a: 1 })};
        --colorscape-primary-half-alpha-color: ${this.rgbaToString({ ...colors.primary, a: 0.5 })};
        --colorscape-lighter-half-alpha-color: ${this.rgbaToString({ ...colors.lighter, a: 0.5 })};
        --colorscape-darker-half-alpha-color: ${this.rgbaToString({ ...colors.darker, a: 0.5 })};
        --colorscape-complementary-half-alpha-color: ${this.rgbaToString({ ...colors.complementary, a: 0.5 })};
      }
    `);
  }

  private generateBackgroundColors(primaryColor: string, secondaryColor: string | null = null, isDarkTheme: boolean = true): ColorSpace {
    const primary = this.hexToRgb(primaryColor);
    const secondary = secondaryColor ? this.hexToRgb(secondaryColor) : this.calculateComplementaryRgb(primary);

    const primaryHSL = this.rgbToHsl(primary);
    const secondaryHSL = this.rgbToHsl(secondary);

    return  isDarkTheme
      ? this.calculateDarkThemeColors(secondaryHSL, primaryHSL, primary)
      : this.calculateLightThemeDarkColors(primaryHSL, primary); // NOTE: Light themes look bad in general with this system.
  }

  private adjustColorWithVariance(color: string): string {
    const rgb = this.hexToRgb(color);
    const randomVariance = () => this.getRandomInRange(-10, 10); // Random variance for each color channel
    return this.rgbToHex({
      r: Math.min(255, Math.max(0, rgb.r + randomVariance())),
      g: Math.min(255, Math.max(0, rgb.g + randomVariance())),
      b: Math.min(255, Math.max(0, rgb.b + randomVariance()))
    });
  }

  private calculateLightThemeDarkColors(primaryHSL: HSL, primary: RGB) {
    const lighterHSL = {...primaryHSL};
    lighterHSL.s = Math.max(lighterHSL.s - 0.3, 0);
    lighterHSL.l = Math.min(lighterHSL.l + 0.5, 0.95);

    const darkerHSL = {...primaryHSL};
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

  private calculateDarkThemeColors(secondaryHSL: HSL, primaryHSL: {
    h: number;
    s: number;
    l: number
  }, primary: RGB) {
    const lighterHSL = this.adjustHue(secondaryHSL, 30);
    lighterHSL.s = Math.min(lighterHSL.s + 0.2, 1);
    lighterHSL.l = Math.min(lighterHSL.l + 0.1, 0.6);

    const darkerHSL = {...primaryHSL};
    darkerHSL.l = Math.max(darkerHSL.l - 0.3, 0.1);

    const complementaryHSL = this.adjustHue(primaryHSL, 180);
    complementaryHSL.s = Math.min(complementaryHSL.s + 0.1, 1);
    complementaryHSL.l = Math.max(complementaryHSL.l - 0.2, 0.2);

    // Array of colors to shuffle
    const colors = [
      this.rgbToHex(primary),
      this.rgbToHex(this.hslToRgb(lighterHSL)),
      this.rgbToHex(this.hslToRgb(darkerHSL)),
      this.rgbToHex(this.hslToRgb(complementaryHSL))
    ];

    // Shuffle colors array
    this.shuffleArray(colors);

    // Set a brightness threshold (you can adjust this value as needed)
    const brightnessThreshold = 100; // Adjust based on your needs (0-255)

    // Ensure the 'lighter' color is not too bright
    if (this.getBrightness(colors[1]) > brightnessThreshold) {
      // If it is too bright, find a suitable swap
      for (let i = 0; i < colors.length; i++) {
        if (this.getBrightness(colors[i]) <= brightnessThreshold) {
          // Swap colors[1] (lighter) with a less bright color
          [colors[1], colors[i]] = [colors[i], colors[1]];
          break;
        }
      }
    }

    // Ensure no color is repeating and variance is maintained
    const uniqueColors = new Set(colors);
    if (uniqueColors.size < colors.length) {
      // If there are duplicates, re-shuffle the array
      this.shuffleArray(colors);
    }

    return {
      primary: colors[0],
      lighter: colors[1],
      darker: colors[2],
      complementary: colors[3]
    };
  }

  // Calculate brightness of a color (RGB)
  private getBrightness(color: string) {
    const rgb = this.hexToRgb(color); // Convert hex to RGB
    // Using the luminance formula for brightness
    return (0.299 * rgb.r + 0.587 * rgb.g + 0.114 * rgb.b);
  }

  // Fisher-Yates shuffle algorithm
  private shuffleArray(array: string[]) {
    for (let i = array.length - 1; i > 0; i--) {
      const j = Math.floor(Math.random() * (i + 1));
      [array[i], array[j]] = [array[j], array[i]];
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

  private rgbToHsl(rgb: RGB): HSL {
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

  private hslToRgb(hsl: HSL): RGB {
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

  private adjustHue(hsl: HSL, amount: number): HSL {
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

  private rgbToString(color: RGBAColor): string {
    return `rgb(${color.r}, ${color.g}, ${color.b})`;
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

  private getRandomInRange(min: number, max: number): number {
    return Math.random() * (max - min) + min;
  }
}
