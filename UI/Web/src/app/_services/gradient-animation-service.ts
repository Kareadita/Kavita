import { Injectable, inject, DestroyRef } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { BehaviorSubject, fromEvent, throttleTime, map } from 'rxjs';

// Types for better type safety
interface GradientPoint {
    x: number;
    y: number;
    vx: number;
    vy: number;
    color: { r: number; g: number; b: number };
}

interface AnimationConfig {
    gradientRadius: number;
    boundsMargin: number;
    velocityRange: { min: number; max: number };
    colors: { r: number; g: number; b: number }[];
}

@Injectable({
    providedIn: 'root'
})
export class GradientAnimationService {
    private readonly destroyRef = inject(DestroyRef);

    // Animation state
    private animationId?: number;
    private gradientPoints: GradientPoint[] = [];
    private canvas?: HTMLCanvasElement;
    private context?: CanvasRenderingContext2D;
    private isRunning = false;

    // Configuration
    private readonly defaultConfig: AnimationConfig = {
        gradientRadius: 0.5,
        boundsMargin: 0.1,
        velocityRange: { min: 0.001, max: 0.0018 },
        colors: [
            { r: 73, g: 197, b: 147 },   // Default gradient color 1
            { r: 138, g: 43, b: 226 },   // Default gradient color 2
            { r: 255, g: 215, b: 0 },    // Default gradient color 3
            { r: 255, g: 20, b: 147 }    // Default gradient color 4
        ]
    };

    // Observables for reactive updates
    private readonly isAnimating$ = new BehaviorSubject<boolean>(false);
    private readonly frameRate$ = new BehaviorSubject<number>(0);

    // Public API
    public readonly animationState$ = this.isAnimating$.asObservable();
    public readonly currentFrameRate$ = this.frameRate$.asObservable();

    /**
     * Initialize the gradient animation system
     */
    public initialize(canvas: HTMLCanvasElement, config?: Partial<AnimationConfig>): boolean {
        try {
            this.canvas = canvas;
            this.context = this.setupCanvasContext(canvas);

            if (!this.context) {
                console.warn('Failed to get canvas context for gradient animation');
                return false;
            }

            const finalConfig = { ...this.defaultConfig, ...config };
            this.initializeGradientPoints(finalConfig);
            this.setupResizeHandling();

            return true;
        } catch (error) {
            console.error('Failed to initialize gradient animation:', error);
            return false;
        }
    }

    /**
     * Start the animation loop
     */
    public start(): void {
        if (this.isRunning || !this.context || !this.canvas) {
            return;
        }

        this.isRunning = true;
        this.isAnimating$.next(true);
        this.startAnimationLoop();
    }

    /**
     * Stop the animation loop
     */
    public stop(): void {
        if (!this.isRunning) return;

        this.isRunning = false;
        this.isAnimating$.next(false);

        if (this.animationId !== undefined) {
            cancelAnimationFrame(this.animationId);
            this.animationId = undefined;
        }
    }

    /**
     * Update gradient colors from CSS variables
     */
    public updateColorsFromCSS(): void {
        const colorVariables = [
            '--gradient-color-1',
            '--gradient-color-2',
            '--gradient-color-3',
            '--gradient-color-4'
        ];

        colorVariables.forEach((variable, index) => {
            if (this.gradientPoints[index]) {
                const cssColor = this.getCssColorValue(variable);
                if (cssColor) {
                    this.gradientPoints[index].color = cssColor;
                }
            }
        });
    }

    /**
     * Dispose of all resources
     */
    public dispose(): void {
        this.stop();
        this.canvas = undefined;
        this.context = undefined;
        this.gradientPoints = [];
        this.isAnimating$.complete();
        this.frameRate$.complete();
    }

    /**
     * Setup canvas context with optimizations
     */
    private setupCanvasContext(canvas: HTMLCanvasElement): CanvasRenderingContext2D | null {
        const context = canvas.getContext('2d', {
            alpha: false,
            desynchronized: true,
            colorSpace: 'srgb'
        });

        if (!context) return null;

        // Apply performance optimizations
        context.imageSmoothingEnabled = true;
        context.imageSmoothingQuality = 'high';

        // Canvas CSS optimizations
        canvas.style.imageRendering = 'auto';
        canvas.style.backfaceVisibility = 'hidden';
        canvas.style.transform = 'translateZ(0)';

        return context;
    }

    /**
     * Initialize gradient points with configuration
     */
    private initializeGradientPoints(config: AnimationConfig): void {
        const positions = [
            { x: 0.2, y: 0.2 },
            { x: 0.8, y: 0.3 },
            { x: 0.5, y: 0.8 },
            { x: 0.3, y: 0.6 }
        ];

        this.gradientPoints = positions.map((pos, index) => ({
            x: pos.x,
            y: pos.y,
            vx: this.randomVelocity(config.velocityRange),
            vy: this.randomVelocity(config.velocityRange),
            color: config.colors[index] || config.colors[0]
        }));
    }

    /**
     * Generate random velocity within range
     */
    private randomVelocity(range: { min: number; max: number }): number {
        const velocity = range.min + Math.random() * (range.max - range.min);
        return Math.random() > 0.5 ? velocity : -velocity;
    }

    /**
     * Setup responsive canvas resizing
     */
    private setupResizeHandling(): void {
        if (!this.canvas) return;

        // Use ResizeObserver for better performance
        if ('ResizeObserver' in window) {
            const resizeObserver = new ResizeObserver(() => {
                this.resizeCanvas();
            });

            resizeObserver.observe(this.canvas);

            // Cleanup observer
            this.destroyRef.onDestroy(() => {
                resizeObserver.disconnect();
            });
        } else {
            // Fallback to window resize with throttling
            fromEvent(window, 'resize')
                .pipe(
                    throttleTime(100),
                    takeUntilDestroyed(this.destroyRef)
                )
                .subscribe(() => this.resizeCanvas());
        }

        // Initial resize
        this.resizeCanvas();
    }

    /**
     * Resize canvas with device pixel ratio support
     */
    private resizeCanvas(): void {
        if (!this.canvas || !this.context) return;

        const dpr = window.devicePixelRatio || 1;
        const rect = this.canvas.getBoundingClientRect();

        // Set actual size in memory
        this.canvas.width = rect.width * dpr;
        this.canvas.height = rect.height * dpr;

        // Scale canvas back down using CSS
        this.canvas.style.width = `${rect.width}px`;
        this.canvas.style.height = `${rect.height}px`;

        // Scale the drawing context
        this.context.scale(dpr, dpr);
    }

    /**
     * Main animation loop with performance monitoring
     */
    private startAnimationLoop(): void {
        let lastTime = performance.now();
        let frameCount = 0;
        let lastFpsUpdate = lastTime;

        const animate = (currentTime: number): void => {
            if (!this.isRunning || !this.context || !this.canvas) return;

            // Calculate frame rate
            frameCount++;
            if (currentTime - lastFpsUpdate >= 1000) {
                this.frameRate$.next(frameCount);
                frameCount = 0;
                lastFpsUpdate = currentTime;
            }

            // Render frame
            this.renderFrame();

            // Schedule next frame
            this.animationId = requestAnimationFrame(animate);
            lastTime = currentTime;
        };

        this.animationId = requestAnimationFrame(animate);
    }

    /**
     * Render a single animation frame
     */
    private renderFrame(): void {
        if (!this.context || !this.canvas) return;

        // Clear canvas with theme background
        const bgColor = this.getCssVariable('--elevation-layer2-dark-solid') || '#212121';
        this.context.fillStyle = bgColor;
        this.context.fillRect(0, 0, this.canvas.width, this.canvas.height);

        // Update and render gradient points
        this.updateGradientPoints();
        this.renderGradientPoints();
    }

    /**
     * Update gradient point positions with boundary collision
     */
    private updateGradientPoints(): void {
        const margin = this.defaultConfig.boundsMargin;

        for (const point of this.gradientPoints) {
            // Update position
            point.x += point.vx;
            point.y += point.vy;

            // Boundary collision with margin
            if (point.x <= margin || point.x >= 1 - margin) {
                point.vx *= -1;
            }
            if (point.y <= margin || point.y >= 1 - margin) {
                point.vy *= -1;
            }

            // Clamp to bounds
            point.x = Math.max(margin, Math.min(1 - margin, point.x));
            point.y = Math.max(margin, Math.min(1 - margin, point.y));
        }
    }

    /**
     * Render gradient points with optimized radial gradients
     */
    private renderGradientPoints(): void {
        if (!this.context || !this.canvas) return;

        const width = this.canvas.width;
        const height = this.canvas.height;

        for (const point of this.gradientPoints) {
            const centerX = point.x * width;
            const centerY = point.y * height;
            const radius = Math.max(width, height) * this.defaultConfig.gradientRadius;

            const gradient = this.context.createRadialGradient(
                centerX, centerY, 0,
                centerX, centerY, radius
            );

            // Optimized gradient stops
            const { r, g, b } = point.color;
            gradient.addColorStop(0, `rgba(${r}, ${g}, ${b}, 0.15)`);
            gradient.addColorStop(0.2, `rgba(${r}, ${g}, ${b}, 0.12)`);
            gradient.addColorStop(0.4, `rgba(${r}, ${g}, ${b}, 0.08)`);
            gradient.addColorStop(0.7, `rgba(${r}, ${g}, ${b}, 0.03)`);
            gradient.addColorStop(1, `rgba(${r}, ${g}, ${b}, 0)`);

            this.context.fillStyle = gradient;
            this.context.fillRect(0, 0, width, height);
        }
    }

    /**
     * Get CSS variable value safely
     */
    private getCssVariable(variableName: string): string | null {
        try {
            const value = getComputedStyle(document.documentElement)
                .getPropertyValue(variableName)
                .trim();
            return value || null;
        } catch (error) {
            return null;
        }
    }

    /**
     * Get CSS color as RGB object
     */
    private getCssColorValue(variableName: string): { r: number; g: number; b: number } | null {
        const cssValue = this.getCssVariable(variableName);
        if (!cssValue) return null;

        return this.hexToRgb(cssValue);
    }

    /**
     * Convert hex to RGB
     */
    private hexToRgb(hex: string): { r: number; g: number; b: number } | null {
        hex = hex.replace(/^#/, '').trim();

        if (hex.length === 3) {
            hex = hex.split('').map(char => char + char).join('');
        }

        if (hex.length !== 6 || !/^[0-9A-Fa-f]{6}$/.test(hex)) {
            return null;
        }

        return {
            r: parseInt(hex.substring(0, 2), 16),
            g: parseInt(hex.substring(2, 4), 16),
            b: parseInt(hex.substring(4, 6), 16)
        };
    }
}