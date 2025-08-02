import { Injectable } from '@angular/core';

export interface RGBColor {
    r: number;
    g: number;
    b: number;
}

@Injectable({
    providedIn: 'root'
})
export class CssVariableService {
    private variableCache = new Map<string, string>();
    private cacheTimeout = 5000; // Cache for 5 seconds
    private cacheTimestamps = new Map<string, number>();
    
    setVariable(name: string, value: string, element?: HTMLElement): void {
        try {
            const targetElement = element || document.documentElement;
            targetElement.style.setProperty(name, value);
            
            // Update cache
            this.cacheVariable(name, value);
        } catch (error) {
            console.warn(`Failed to set CSS variable '${name}' to '${value}':`, error);
        }
    }
    
    setVariablesBatch(variables: Record<string, string>, element?: HTMLElement): void {
        try {
            const targetElement = element || document.documentElement;
            // Use CSSStyleDeclaration.setProperty for better performance
            Object.entries(variables).forEach(([name, value]) => {
                targetElement.style.setProperty(name, value);
            });
        } catch (error) {
            console.warn('Failed to set CSS variables batch:', error);
        }
    }
    
    getVariable(variableName: string, element?: HTMLElement): string | null {
        // Check cache first
        const cachedValue = this.getCachedVariable(variableName);
        if (cachedValue !== null) {
            return cachedValue;
        }
        
        try {
            const targetElement = element || document.documentElement;
            const value = getComputedStyle(targetElement).getPropertyValue(variableName).trim();
            
            // Cache the result
            this.cacheVariable(variableName, value);
            
            return value || null;
        } catch (error) {
            console.warn(`Failed to get CSS variable '${variableName}':`, error);
            return null;
        }
    }
    
    private getCachedVariable(variableName: string): string | null {
        const timestamp = this.cacheTimestamps.get(variableName);
        if (timestamp && Date.now() - timestamp < this.cacheTimeout) {
            return this.variableCache.get(variableName) || null;
        }
        return null;
    }
    
    private cacheVariable(variableName: string, value: string): void {
        this.variableCache.set(variableName, value);
        this.cacheTimestamps.set(variableName, Date.now());
    }
    
    clearCache(): void {
        this.variableCache.clear();
        this.cacheTimestamps.clear();
    }
    
    getVariableAsRgb(variableName: string, element?: HTMLElement): RGBColor | null {
        const value = this.getVariable(variableName, element);
        if (!value) {
            return null;
        }
        
        // Try to parse as hex first
        if (value.startsWith('#')) {
            return this.hexToRgb(value);
        }
        
        // Try to parse as rgb/rgba
        if (value.startsWith('rgb')) {
            return this.rgbToRgb(value);
        }
        
        // Try to parse as hsl/hsla
        if (value.startsWith('hsl')) {
            return this.hslToRgb(value);
        }
        
        // If none of the above, try hex parsing as fallback
        return this.hexToRgb(value);
    }
    
    private hexToRgb(hex: string): RGBColor | null {
        // Remove the hash if present
        hex = hex.replace('#', '');
        
        // Handle 3-digit hex codes (e.g., #fff -> #ffffff)
        if (hex.length === 3) {
            hex = hex.split('').map(char => char + char).join('');
        }
        
        // Validate hex format - return null for invalid formats
        if (hex.length !== 6 || !/^[0-9A-Fa-f]{6}$/.test(hex)) {
            return null;
        }
        
        // Convert to RGB
        const r = parseInt(hex.substring(0, 2), 16);
        const g = parseInt(hex.substring(2, 4), 16);
        const b = parseInt(hex.substring(4, 6), 16);
        
        return { r, g, b };
    }
    
    private rgbToRgb(rgb: string): RGBColor | null {
        // Parse rgb(r, g, b) or rgba(r, g, b, a) format
        const match = rgb.match(/rgba?\((\d+),\s*(\d+),\s*(\d+)(?:,\s*[\d.]+)?\)/);
        if (!match) return null;
        
        const r = parseInt(match[1], 10);
        const g = parseInt(match[2], 10);
        const b = parseInt(match[3], 10);
        
        return { r, g, b };
    }
    
    private hslToRgb(hsl: string): RGBColor | null {
        // Parse hsl(h, s%, l%) or hsla(h, s%, l%, a) format
        const match = hsl.match(/hsla?\((\d+),\s*(\d+)%,\s*(\d+)%(?:,\s*[\d.]+)?\)/);
        if (!match) return null;
        
        const h = parseInt(match[1], 10);
        const s = parseInt(match[2], 10) / 100;
        const l = parseInt(match[3], 10) / 100;
        
        // Convert HSL to RGB
        const c = (1 - Math.abs(2 * l - 1)) * s;
        const x = c * (1 - Math.abs((h / 60) % 2 - 1));
        const m = l - c / 2;
        
        let r = 0, g = 0, b = 0;
        
        if (h >= 0 && h < 60) {
            r = c; g = x; b = 0;
        } else if (h >= 60 && h < 120) {
            r = x; g = c; b = 0;
        } else if (h >= 120 && h < 180) {
            r = 0; g = c; b = x;
        } else if (h >= 180 && h < 240) {
            r = 0; g = x; b = c;
        } else if (h >= 240 && h < 300) {
            r = x; g = 0; b = c;
        } else if (h >= 300 && h < 360) {
            r = c; g = 0; b = x;
        }
        
        return {
            r: Math.round((r + m) * 255),
            g: Math.round((g + m) * 255),
            b: Math.round((b + m) * 255)
        };
    }
} 