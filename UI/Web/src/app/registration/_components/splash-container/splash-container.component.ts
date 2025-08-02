import { ChangeDetectionStrategy, Component, ElementRef, HostListener, inject, OnDestroy, OnInit, ViewChild, DestroyRef, signal, computed, AfterViewInit } from '@angular/core';
import { AsyncPipe, NgStyle } from "@angular/common";
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavService } from "../../../_services/nav.service";

// Types for better type safety
interface GradientPoint {
    x: number;
    y: number;
    vx: number;
    vy: number;
    color: { r: number; g: number; b: number };
}

interface MousePosition {
    x: number;
    y: number;
}

interface TiltEffect {
    rotateX: number;
    rotateY: number;
    shadowX: number;
    shadowY: number;
    intensity: number;
}

@Component({
    selector: 'app-splash-container',
    templateUrl: './splash-container.component.html',
    styleUrls: ['./splash-container.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [NgStyle, AsyncPipe],
    standalone: true
})
export class SplashContainerComponent implements OnInit, OnDestroy, AfterViewInit {

    // Dependency injection
    protected readonly navService = inject(NavService);
    private readonly destroyRef = inject(DestroyRef);
    private readonly elementRef = inject(ElementRef<HTMLElement>);

    // ViewChild for safer DOM access
    @ViewChild('gradientCanvas', { static: true })
    private canvasRef?: ElementRef<HTMLCanvasElement>;

    @ViewChild('tiltCard', { static: true })
    private tiltCardRef?: ElementRef<HTMLElement>;

    // Configuration constants
    private readonly MAX_TILT = 5;
    private readonly SHADOW_MULTIPLIER = 2;
    private readonly MIN_INTENSITY = 0.1;
    private readonly GRADIENT_RADIUS = 0.5;

    // State management with signals (Angular 16+)
    private readonly mousePosition = signal<MousePosition>({ x: 0, y: 0 });
    private readonly isMouseOverCard = signal(false);

    // Computed values for reactive updates
    private readonly tiltEffect = computed<TiltEffect>(() => {
        const mouse = this.mousePosition();
        const isOver = this.isMouseOverCard();

        if (isOver || !this.tiltCardRef?.nativeElement) {
            return { rotateX: 0, rotateY: 0, shadowX: 0, shadowY: 0, intensity: 0.5 };
        }

        return this.calculateTiltEffect(mouse);
    });

    // Resource tracking
    private gradientAnimationId?: number;
    private mouseMoveThrottleId?: number;
    private resizeObserver?: ResizeObserver;
    private gradientPoints: GradientPoint[] = [];
    private canvasContext?: CanvasRenderingContext2D;

    ngOnInit(): void {
        this.initializeGradientColors();
    }

    ngAfterViewInit(): void {
        // Safer initialization after view is ready
        this.initializeCanvas();
        this.initializeResizeObserver();
        this.startGradientAnimation();
    }

    ngOnDestroy(): void {
        this.cleanup();
    }

    @HostListener('document:mousemove', ['$event'])
    onMouseMove(event: MouseEvent): void {
        // Cancel previous throttled update
        if (this.mouseMoveThrottleId) {
            cancelAnimationFrame(this.mouseMoveThrottleId);
        }

        // Throttle updates to animation frame rate
        this.mouseMoveThrottleId = requestAnimationFrame(() => {
            this.updateMousePosition(event);
            this.updateTiltEffects();
            this.mouseMoveThrottleId = undefined;
        });
    }

    /**
     * Initialize gradient colors from CSS variables with proper fallbacks
     */
    private initializeGradientColors(): void {
        const defaultColors = [
            { r: 73, g: 197, b: 147 },   // --gradient-color-1
            { r: 138, g: 43, b: 226 },   // --gradient-color-2
            { r: 255, g: 215, b: 0 },    // --gradient-color-3
            { r: 255, g: 20, b: 147 }    // --gradient-color-4
        ];

        this.gradientPoints = [
            { x: 0.2, y: 0.2, vx: 0.001, vy: 0.0015, color: this.getCssColorOrDefault('--gradient-color-1', defaultColors[0]) },
            { x: 0.8, y: 0.3, vx: -0.0015, vy: 0.001, color: this.getCssColorOrDefault('--gradient-color-2', defaultColors[1]) },
            { x: 0.5, y: 0.8, vx: 0.0012, vy: -0.0018, color: this.getCssColorOrDefault('--gradient-color-3', defaultColors[2]) },
            { x: 0.3, y: 0.6, vx: -0.0018, vy: -0.0012, color: this.getCssColorOrDefault('--gradient-color-4', defaultColors[3]) }
        ];
    }

    /**
     * Safely initialize canvas with proper error handling
     */
    private initializeCanvas(): void {
        const canvas = this.canvasRef?.nativeElement;
        if (!canvas) {
            console.warn('Canvas element not found, gradient animation disabled');
            return;
        }

        const context = canvas.getContext('2d', {
            alpha: false,
            desynchronized: true // Better performance for animations
        });

        if (!context) {
            console.warn('Unable to get 2D context, gradient animation disabled');
            return;
        }

        this.canvasContext = context;
        this.setupCanvasOptimizations();
        this.resizeCanvas();
    }

    /**
     * Apply performance optimizations to canvas context
     */
    private setupCanvasOptimizations(): void {
        if (!this.canvasContext) return;

        // Firefox-specific optimizations
        this.canvasContext.imageSmoothingEnabled = true;
        this.canvasContext.imageSmoothingQuality = 'high';

        const canvas = this.canvasRef!.nativeElement;
        canvas.style.imageRendering = 'auto';
        canvas.style.backfaceVisibility = 'hidden';
        canvas.style.transform = 'translateZ(0)';
        canvas.style.willChange = 'transform';
    }

    /**
     * Initialize ResizeObserver for better performance than window resize events
     */
    private initializeResizeObserver(): void {
        if (!('ResizeObserver' in window) || !this.canvasRef?.nativeElement) {
            // Fallback to window resize for older browsers
            this.initializeFallbackResize();
            return;
        }

        this.resizeObserver = new ResizeObserver((entries) => {
            for (const entry of entries) {
                if (entry.target === this.canvasRef!.nativeElement) {
                    this.resizeCanvas();
                    break;
                }
            }
        });

        this.resizeObserver.observe(this.canvasRef.nativeElement);
    }

    /**
     * Fallback resize handling for older browsers
     */
    private initializeFallbackResize(): void {
        const resizeHandler = () => this.resizeCanvas();
        window.addEventListener('resize', resizeHandler);

        // Cleanup using takeUntilDestroyed
        this.destroyRef.onDestroy(() => {
            window.removeEventListener('resize', resizeHandler);
        });
    }

    /**
     * Resize canvas with device pixel ratio consideration
     */
    private resizeCanvas(): void {
        const canvas = this.canvasRef?.nativeElement;
        if (!canvas || !this.canvasContext) return;

        const dpr = window.devicePixelRatio || 1;
        const rect = canvas.getBoundingClientRect();

        // Set actual size in memory (scaled for device pixel ratio)
        canvas.width = rect.width * dpr;
        canvas.height = rect.height * dpr;

        // Scale the canvas back down using CSS
        canvas.style.width = `${rect.width}px`;
        canvas.style.height = `${rect.height}px`;

        // Scale the drawing context so everything draws at the correct size
        this.canvasContext.scale(dpr, dpr);
    }

    /**
     * Start the gradient animation loop
     */
    private startGradientAnimation(): void {
        if (!this.canvasContext || this.gradientAnimationId) return;

        const animate = (): void => {
            this.renderGradientFrame();
            this.gradientAnimationId = requestAnimationFrame(animate);
        };

        animate();
    }

    /**
     * Render a single gradient animation frame
     */
    private renderGradientFrame(): void {
        if (!this.canvasContext || !this.canvasRef?.nativeElement) return;

        const canvas = this.canvasRef.nativeElement;
        const ctx = this.canvasContext;

        // Clear with theme background
        const bgColor = this.getCssVariable('--elevation-layer2-dark-solid') || '#212121';
        ctx.fillStyle = bgColor;
        ctx.fillRect(0, 0, canvas.width, canvas.height);

        // Update and render gradient points
        this.updateGradientPoints();
        this.renderGradientPoints(ctx, canvas);
    }

    /**
     * Update gradient point positions with boundary checking
     */
    private updateGradientPoints(): void {
        for (const point of this.gradientPoints) {
            point.x += point.vx;
            point.y += point.vy;

            // Bounce off edges
            if (point.x <= 0.1 || point.x >= 0.9) point.vx *= -1;
            if (point.y <= 0.1 || point.y >= 0.9) point.vy *= -1;

            // Clamp to bounds
            point.x = Math.max(0.1, Math.min(0.9, point.x));
            point.y = Math.max(0.1, Math.min(0.9, point.y));
        }
    }

    /**
     * Render gradient points with optimized radial gradients
     */
    private renderGradientPoints(ctx: CanvasRenderingContext2D, canvas: HTMLCanvasElement): void {
        for (const point of this.gradientPoints) {
            const gradient = ctx.createRadialGradient(
                point.x * canvas.width,
                point.y * canvas.height,
                0,
                point.x * canvas.width,
                point.y * canvas.height,
                canvas.width * this.GRADIENT_RADIUS
            );

            // Optimized gradient stops
            const { r, g, b } = point.color;
            gradient.addColorStop(0, `rgba(${r}, ${g}, ${b}, 0.15)`);
            gradient.addColorStop(0.2, `rgba(${r}, ${g}, ${b}, 0.12)`);
            gradient.addColorStop(0.4, `rgba(${r}, ${g}, ${b}, 0.08)`);
            gradient.addColorStop(0.7, `rgba(${r}, ${g}, ${b}, 0.03)`);
            gradient.addColorStop(1, `rgba(${r}, ${g}, ${b}, 0)`);

            ctx.fillStyle = gradient;
            ctx.fillRect(0, 0, canvas.width, canvas.height);
        }
    }

    /**
     * Update mouse position with boundary checks
     */
    private updateMousePosition(event: MouseEvent): void {
        const cardElement = this.tiltCardRef?.nativeElement;
        if (!cardElement) return;

        const rect = cardElement.getBoundingClientRect();
        const isOver = event.clientX >= rect.left &&
            event.clientX <= rect.right &&
            event.clientY >= rect.top &&
            event.clientY <= rect.bottom;

        this.mousePosition.set({ x: event.clientX, y: event.clientY });
        this.isMouseOverCard.set(isOver);
    }

    /**
     * Calculate tilt effect based on mouse position
     */
    private calculateTiltEffect(mouse: MousePosition): TiltEffect {
        const cardElement = this.tiltCardRef?.nativeElement;
        if (!cardElement) {
            return { rotateX: 0, rotateY: 0, shadowX: 0, shadowY: 0, intensity: 0.5 };
        }

        const rect = cardElement.getBoundingClientRect();
        const centerX = rect.left + rect.width / 2;
        const centerY = rect.top + rect.height / 2;

        const tiltX = ((mouse.y - centerY) / window.innerHeight) * this.MAX_TILT;
        const tiltY = ((mouse.x - centerX) / window.innerWidth) * this.MAX_TILT;

        const shadowX = tiltY * this.SHADOW_MULTIPLIER;
        const shadowY = tiltX * this.SHADOW_MULTIPLIER;

        // Calculate distance-based intensity
        const distance = Math.sqrt(
            Math.pow(mouse.x - centerX, 2) + Math.pow(mouse.y - centerY, 2)
        );
        const maxDistance = Math.sqrt(
            Math.pow(window.innerWidth, 2) + Math.pow(window.innerHeight, 2)
        );
        const intensity = Math.max(this.MIN_INTENSITY, 1 - (distance / maxDistance));

        return {
            rotateX: tiltX,
            rotateY: tiltY,
            shadowX,
            shadowY,
            intensity
        };
    }

    /**
     * Apply tilt effects to DOM using CSS custom properties
     */
    private updateTiltEffects(): void {
        const effect = this.tiltEffect();
        const cardElement = this.tiltCardRef?.nativeElement;

        if (!cardElement) return;

        // Apply transform
        const transform = `perspective(500px) rotateX(${effect.rotateX}deg) rotateY(${effect.rotateY}deg)`;
        cardElement.style.transform = transform;

        // Update CSS custom properties for shadows
        const root = document.documentElement;
        root.style.setProperty('--dynamic-shadow-x', `${effect.shadowX}px`);
        root.style.setProperty('--dynamic-shadow-y', `${effect.shadowY}px`);
        root.style.setProperty('--shadow-intensity', effect.intensity.toString());

        // Update shine position
        if (!this.isMouseOverCard()) {
            const mouse = this.mousePosition();
            const rect = cardElement.getBoundingClientRect();
            const relativeX = Math.max(0, Math.min(100, ((mouse.x - rect.left) / rect.width) * 100));
            const relativeY = Math.max(0, Math.min(100, ((mouse.y - rect.top) / rect.height) * 100));

            root.style.setProperty('--shine-pos-x', `${Math.round(relativeX)}%`);
            root.style.setProperty('--shine-pos-y', `${Math.round(relativeY)}%`);
        }
    }

    /**
     * Get CSS variable with proper error handling
     */
    private getCssVariable(variableName: string, element: HTMLElement = document.documentElement): string | null {
        try {
            const value = getComputedStyle(element).getPropertyValue(variableName).trim();
            return value || null;
        } catch (error) {
            console.warn(`Failed to get CSS variable ${variableName}:`, error);
            return null;
        }
    }

    /**
     * Get CSS color as RGB object with fallback
     */
    private getCssColorOrDefault(variableName: string, defaultColor: { r: number; g: number; b: number }): { r: number; g: number; b: number } {
        const cssValue = this.getCssVariable(variableName);
        if (!cssValue) return defaultColor;

        const rgbColor = this.hexToRgb(cssValue);
        return rgbColor || defaultColor;
    }

    /**
     * Convert hex color to RGB with improved validation
     */
    private hexToRgb(hex: string): { r: number; g: number; b: number } | null {
        // Remove hash and whitespace
        hex = hex.replace(/^#/, '').trim();

        // Handle 3-digit hex
        if (hex.length === 3) {
            hex = hex.split('').map(char => char + char).join('');
        }

        // Validate format
        if (hex.length !== 6 || !/^[0-9A-Fa-f]{6}$/.test(hex)) {
            return null;
        }

        // Convert to RGB
        return {
            r: parseInt(hex.substring(0, 2), 16),
            g: parseInt(hex.substring(2, 4), 16),
            b: parseInt(hex.substring(4, 6), 16)
        };
    }

    /**
     * Comprehensive cleanup of all resources
     */
    private cleanup(): void {
        // Cancel animation frames
        if (this.gradientAnimationId !== undefined) {
            cancelAnimationFrame(this.gradientAnimationId);
            this.gradientAnimationId = undefined;
        }

        if (this.mouseMoveThrottleId !== undefined) {
            cancelAnimationFrame(this.mouseMoveThrottleId);
            this.mouseMoveThrottleId = undefined;
        }

        // Cleanup ResizeObserver
        if (this.resizeObserver) {
            this.resizeObserver.disconnect();
            this.resizeObserver = undefined;
        }

        // Reset CSS custom properties
        const root = document.documentElement;
        root.style.removeProperty('--dynamic-shadow-x');
        root.style.removeProperty('--dynamic-shadow-y');
        root.style.removeProperty('--shadow-intensity');
        root.style.removeProperty('--shine-pos-x');
        root.style.removeProperty('--shine-pos-y');

        // Clear references
        this.canvasContext = undefined;
        this.gradientPoints = [];
    }
}