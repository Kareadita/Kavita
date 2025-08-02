import { Injectable } from '@angular/core';
import { CssVariableService, RGBColor } from './css-variable.service';

export interface GradientPoint {
    x: number;
    y: number;
    vx: number;
    vy: number;
    color: RGBColor;
    size: number; // Add size property for more dynamic gradients
}

@Injectable({
    providedIn: 'root'
})
export class GradientAnimationService {
    private animationId?: number;
    private resizeHandler?: () => void;
    private blurHandler?: () => void;
    private focusHandler?: () => void;
    private isWindowFocused = true;
    private isVisible = true;
    private intersectionObserver?: IntersectionObserver;
    private lastFrameTime = 0;
    
    // Remove complex caching and performance monitoring for smoother animation
    private canvasWidth = 0;
    private canvasHeight = 0;
    
    constructor(private cssVariableService: CssVariableService) {}
    
    startAnimation(canvas: HTMLCanvasElement, isReducedMotion: boolean = false, isStaticMode: boolean = false): void {
        if (!canvas) return;
        
        // Stop any existing animation
        this.stopAnimation();
        
        const ctx = canvas.getContext('2d');
        if (!ctx) return;
        
        // Optimize canvas settings
        ctx.imageSmoothingEnabled = true;
        ctx.imageSmoothingQuality = 'high';
        
        // Set canvas size and cache dimensions
        const resizeCanvas = () => {
            this.canvasWidth = window.innerWidth;
            this.canvasHeight = window.innerHeight;
            canvas.width = this.canvasWidth;
            canvas.height = this.canvasHeight;
            
            // If static mode, render once and stop
            if (isStaticMode) {
                this.renderStaticGradients(ctx);
                return;
            }
        };
        
        this.resizeHandler = resizeCanvas;
        resizeCanvas();
        window.addEventListener('resize', this.resizeHandler, { passive: true });
        
        // If static mode requested, don't start animation loop
        if (isStaticMode) {
            return;
        }
        
        // Handle window focus/blur
        this.blurHandler = () => { this.isWindowFocused = false; };
        this.focusHandler = () => { this.isWindowFocused = true; };
        window.addEventListener('blur', this.blurHandler, { passive: true });
        window.addEventListener('focus', this.focusHandler, { passive: true });
        
        // Setup intersection observer
        this.setupIntersectionObserver(canvas);
        
        // Get colors from CSS variables with fallbacks
        const gradientColor1 = this.cssVariableService.getVariableAsRgb('--gradient-color-1') || { r: 73, g: 197, b: 147 };
        const gradientColor2 = this.cssVariableService.getVariableAsRgb('--gradient-color-2') || { r: 138, g: 43, b: 226 };
        const gradientColor3 = this.cssVariableService.getVariableAsRgb('--gradient-color-3') || { r: 255, g: 215, b: 0 };
        const gradientColor4 = this.cssVariableService.getVariableAsRgb('--gradient-color-4') || { r: 255, g: 20, b: 147 };
        
        // Respect accessibility preferences - much slower or static for reduced motion
        const baseSpeed = isReducedMotion ? 0.00001 : 0.0003; // Nearly static for reduced motion
        const speedVariation = isReducedMotion ? 0.000005 : 0.0001; // Minimal variation for reduced motion
        
        const gradientPoints: GradientPoint[] = [
            {
                x: 0.05, // Moved closer to edge
                y: 0.1,
                vx: baseSpeed + speedVariation * 0.5,
                vy: (baseSpeed + speedVariation) * 1.2,
                color: gradientColor1,
                size: 1.4 // Reduced but still large
            },
            {
                x: 0.95, // Moved closer to edge
                y: 0.15,
                vx: -(baseSpeed + speedVariation * 0.8),
                vy: baseSpeed + speedVariation * 0.6,
                color: gradientColor2,
                size: 1.5 // Reduced but still large
            },
            {
                x: 0.5, // Center position
                y: 0.95, // Moved closer to edge
                vx: (baseSpeed + speedVariation * 0.7),
                vy: -(baseSpeed + speedVariation * 1.1),
                color: gradientColor3,
                size: 1.3 // Reduced but still large
            },
            {
                x: 0.15,
                y: 0.75,
                vx: -(baseSpeed + speedVariation * 1.0),
                vy: -(baseSpeed + speedVariation * 0.4),
                color: gradientColor4,
                size: 1.4 // Reduced but still large
            },
            {
                x: 0.85, // Additional gradient for better coverage
                y: 0.6,
                vx: baseSpeed + speedVariation * 0.3,
                vy: -(baseSpeed + speedVariation * 0.8),
                color: gradientColor1, // Reuse color for harmony
                size: 1.2
            }
        ];
        
        // Pre-calculate background color
        const backgroundColor = this.cssVariableService.getVariable('--elevation-layer2-dark-solid') || '#1f2020';
        
        const animate = (timestamp: number) => {
            // Simple time-based animation without complex performance monitoring
            const deltaTime = this.lastFrameTime > 0 ? Math.min(timestamp - this.lastFrameTime, 32) : 16.67;
            this.lastFrameTime = timestamp;
            
            // Skip frame if window not focused or not visible
            if (!this.isWindowFocused || !this.isVisible) {
                this.animationId = requestAnimationFrame(animate);
                return;
            }
            
            try {
                // Clear canvas with background color
                ctx.fillStyle = backgroundColor;
                ctx.fillRect(0, 0, this.canvasWidth, this.canvasHeight);
                
                // Set blend mode for smoother gradients
                ctx.globalCompositeOperation = 'screen';
                
                // Update and render gradient points
                gradientPoints.forEach(point => {
                    // Update position with time-based movement
                    const timeMultiplier = deltaTime / 16.67;
                    point.x += point.vx * timeMultiplier;
                    point.y += point.vy * timeMultiplier;
                    
                    // Smoother boundary bouncing with larger boundaries for bigger gradients
                    if (point.x <= 0.05 || point.x >= 0.95) {
                        point.vx = -point.vx;
                        point.x = Math.max(0.05, Math.min(0.95, point.x));
                    }
                    if (point.y <= 0.05 || point.y >= 0.95) {
                        point.vy = -point.vy;
                        point.y = Math.max(0.05, Math.min(0.95, point.y));
                    }
                    
                    // Smaller radius but strategic positioning for full coverage
                    const pointX = point.x * this.canvasWidth;
                    const pointY = point.y * this.canvasHeight;
                    const radius = Math.min(this.canvasWidth, this.canvasHeight) * 0.9 * point.size; // Reduced radius
                    
                    const gradient = ctx.createRadialGradient(
                        pointX, pointY, 0,
                        pointX, pointY, radius
                    );
                    
                    const { r, g, b } = point.color;
                    // More vibrant gradients with higher opacity
                    gradient.addColorStop(0, `rgba(${r}, ${g}, ${b}, 0.25)`); // Much more vibrant center
                    gradient.addColorStop(0.15, `rgba(${r}, ${g}, ${b}, 0.18)`); // Stronger near center
                    gradient.addColorStop(0.35, `rgba(${r}, ${g}, ${b}, 0.12)`); // More visible mid-range
                    gradient.addColorStop(0.6, `rgba(${r}, ${g}, ${b}, 0.06)`); // Gradual fade
                    gradient.addColorStop(0.8, `rgba(${r}, ${g}, ${b}, 0.03)`); // Soft edge
                    gradient.addColorStop(1, `rgba(${r}, ${g}, ${b}, 0)`); // Smooth to transparent
                    
                    ctx.fillStyle = gradient;
                    ctx.fillRect(0, 0, this.canvasWidth, this.canvasHeight);
                });
                
                // Reset composite operation
                ctx.globalCompositeOperation = 'source-over';
                
                this.animationId = requestAnimationFrame(animate);
            } catch (error) {
                // Silently handle animation errors and stop animation
                this.stopAnimation();
            }
        };
        
        // Start animation
        this.animationId = requestAnimationFrame(animate);
    }
    
    private renderStaticGradients(ctx: CanvasRenderingContext2D): void {
        // Get colors from CSS variables with fallbacks
        const gradientColor1 = this.cssVariableService.getVariableAsRgb('--gradient-color-1') || { r: 73, g: 197, b: 147 };
        const gradientColor2 = this.cssVariableService.getVariableAsRgb('--gradient-color-2') || { r: 138, g: 43, b: 226 };
        const gradientColor3 = this.cssVariableService.getVariableAsRgb('--gradient-color-3') || { r: 255, g: 215, b: 0 };
        const gradientColor4 = this.cssVariableService.getVariableAsRgb('--gradient-color-4') || { r: 255, g: 20, b: 147 };
        
        // Static gradient positions (no animation)
        const staticGradientPoints = [
            { x: 0.2, y: 0.2, color: gradientColor1, size: 1.4 },
            { x: 0.8, y: 0.3, color: gradientColor2, size: 1.5 },
            { x: 0.5, y: 0.8, color: gradientColor3, size: 1.3 },
            { x: 0.3, y: 0.6, color: gradientColor4, size: 1.4 },
            { x: 0.7, y: 0.7, color: gradientColor1, size: 1.2 }
        ];
        
        // Pre-calculate background color
        const backgroundColor = this.cssVariableService.getVariable('--elevation-layer2-dark-solid') || '#1f2020';
        
        // Clear canvas with background color
        ctx.fillStyle = backgroundColor;
        ctx.fillRect(0, 0, this.canvasWidth, this.canvasHeight);
        
        // Set blend mode for smoother gradients
        ctx.globalCompositeOperation = 'screen';
        
        // Render static gradients
        staticGradientPoints.forEach(point => {
            const pointX = point.x * this.canvasWidth;
            const pointY = point.y * this.canvasHeight;
            const radius = Math.min(this.canvasWidth, this.canvasHeight) * 0.9 * point.size;
            
            const gradient = ctx.createRadialGradient(
                pointX, pointY, 0,
                pointX, pointY, radius
            );
            
            const { r, g, b } = point.color;
            gradient.addColorStop(0, `rgba(${r}, ${g}, ${b}, 0.25)`);
            gradient.addColorStop(0.15, `rgba(${r}, ${g}, ${b}, 0.18)`);
            gradient.addColorStop(0.35, `rgba(${r}, ${g}, ${b}, 0.12)`);
            gradient.addColorStop(0.6, `rgba(${r}, ${g}, ${b}, 0.06)`);
            gradient.addColorStop(0.8, `rgba(${r}, ${g}, ${b}, 0.03)`);
            gradient.addColorStop(1, `rgba(${r}, ${g}, ${b}, 0)`);
            
            ctx.fillStyle = gradient;
            ctx.fillRect(0, 0, this.canvasWidth, this.canvasHeight);
        });
        
        // Reset composite operation
        ctx.globalCompositeOperation = 'source-over';
    }
    
    stopAnimation(): void {
        if (this.animationId !== undefined) {
            cancelAnimationFrame(this.animationId);
            this.animationId = undefined;
        }
        
        // Clean up event listeners
        if (this.resizeHandler) {
            window.removeEventListener('resize', this.resizeHandler);
            this.resizeHandler = undefined;
        }
        
        if (this.blurHandler) {
            window.removeEventListener('blur', this.blurHandler);
            this.blurHandler = undefined;
        }
        
        if (this.focusHandler) {
            window.removeEventListener('focus', this.focusHandler);
            this.focusHandler = undefined;
        }
        
        if (this.intersectionObserver) {
            this.intersectionObserver.disconnect();
            this.intersectionObserver = undefined;
        }
        
        // Reset state
        this.isWindowFocused = true;
        this.isVisible = true;
        this.lastFrameTime = 0;
    }
    
    isAnimating(): boolean {
        return this.animationId !== undefined;
    }
    
    private setupIntersectionObserver(canvas: HTMLCanvasElement): void {
        if ('IntersectionObserver' in window) {
            this.intersectionObserver = new IntersectionObserver(
                (entries) => {
                    entries.forEach(entry => {
                        this.isVisible = entry.isIntersecting;
                    });
                },
                {
                    threshold: 0.1,
                    rootMargin: '50px'
                }
            );
            
            this.intersectionObserver.observe(canvas);
        }
    }
}