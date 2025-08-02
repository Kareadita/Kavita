import {ChangeDetectionStrategy, Component, HostListener, inject, OnInit, OnDestroy} from '@angular/core';
import {AsyncPipe, NgStyle} from "@angular/common";
import {NavService} from "../../../_services/nav.service";

@Component({
    selector: 'app-splash-container',
    templateUrl: './splash-container.component.html',
    styleUrls: ['./splash-container.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [
        NgStyle,
        AsyncPipe
    ]
})
export class SplashContainerComponent implements OnInit, OnDestroy {
    protected readonly navService = inject(NavService);
    private maxTilt = 5; // Maximum tilt angle in degrees
    private animationId?: number;
    private resizeHandler?: () => void;
    private tiltElement?: HTMLElement;
    private mouseMoveThrottleId?: number;
    private lastMouseEvent?: MouseEvent;

    ngOnInit() {
        this.initGradientAnimationWithCssVars();
        this.cacheTiltElement();
    }

    ngOnDestroy() {
        if (this.animationId !== undefined) {
            cancelAnimationFrame(this.animationId);
        }

        if (this.mouseMoveThrottleId !== undefined) {
            cancelAnimationFrame(this.mouseMoveThrottleId);
        }

        if (this.resizeHandler) {
            window.removeEventListener('resize', this.resizeHandler);
        }
    }

    private cacheTiltElement() {
        // Cache the tilt element reference to avoid repeated DOM queries
        this.tiltElement = document.querySelector('.tilt') as HTMLElement;
    }

    @HostListener('document:mousemove', ['$event'])
    onMouseMove(event: MouseEvent) {
        // Store the latest mouse event
        this.lastMouseEvent = event;

        // Cancel any pending throttled update
        if (this.mouseMoveThrottleId !== undefined) {
            return; // Already scheduled, skip this event
        }

        // Schedule the actual update for the next animation frame
        this.mouseMoveThrottleId = requestAnimationFrame(() => {
            if (this.lastMouseEvent) {
                this.handleMouseMove(this.lastMouseEvent);
            }
            this.mouseMoveThrottleId = undefined;
        });
    }

    private handleMouseMove(event: MouseEvent) {
        if (!this.tiltElement) return;

        const elementBounds = this.tiltElement.getBoundingClientRect();
        const mouseX = event.clientX;
        const mouseY = event.clientY;

        // Check if mouse is over the element
        const isMouseOverElement =
            mouseX >= elementBounds.left &&
            mouseX <= elementBounds.right &&
            mouseY >= elementBounds.top &&
            mouseY <= elementBounds.bottom;

        // Always calculate shine position relative to the element
        const relativeX = mouseX - elementBounds.left;
        const relativeY = mouseY - elementBounds.top;

        // Convert to percentage and clamp to keep shine within element bounds (0-100%)
        const shineX = Math.max(0, Math.min(100, (relativeX / elementBounds.width) * 100));
        const shineY = Math.max(0, Math.min(100, (relativeY / elementBounds.height) * 100));

        // Apply clamped shine position to keep it within element
        document.documentElement.style.setProperty('--shine-pos-x', `${Math.round(shineX)}%`);
        document.documentElement.style.setProperty('--shine-pos-y', `${Math.round(shineY)}%`);

        // Calculate tilt values for shadow effects
        const centerX = elementBounds.left + elementBounds.width / 2;
        const centerY = elementBounds.top + elementBounds.height / 2;
        const tiltX = ((mouseY - centerY) / window.innerHeight) * this.maxTilt;
        const tiltY = ((mouseX - centerX) / window.innerWidth) * this.maxTilt;

        // Calculate shadow offset based on tilt (same direction as mouse for closer-darker effect)
        const shadowOffsetX = tiltY * 2; // Same direction as tilt for light-source effect
        const shadowOffsetY = tiltX * 2;

        // Calculate distance from mouse to element center for shadow intensity
        const distanceFromMouse = Math.sqrt(
            Math.pow(mouseX - centerX, 2) + Math.pow(mouseY - centerY, 2)
        );
        const maxDistance = Math.sqrt(Math.pow(window.innerWidth, 2) + Math.pow(window.innerHeight, 2));
        const normalizedDistance = Math.min(distanceFromMouse / maxDistance, 1);

        // Calculate shadow intensity (stronger when closer, weaker when farther)
        const shadowIntensity = Math.max(0.1, 1 - normalizedDistance); // Min 10%, Max 100%

        // Set CSS variables for dynamic shadows with intensity
        document.documentElement.style.setProperty('--dynamic-shadow-x', `${shadowOffsetX}px`);
        document.documentElement.style.setProperty('--dynamic-shadow-y', `${shadowOffsetY}px`);
        document.documentElement.style.setProperty('--shadow-intensity', shadowIntensity.toString());

        // Apply tilt effect based on hover state
        if (isMouseOverElement) {
            // Reset tilt to zero when hovering over element
            this.tiltElement.style.transform = 'perspective(500px) rotateX(0deg) rotateY(0deg)';
            // Reset shadows when hovering
            document.documentElement.style.setProperty('--dynamic-shadow-x', '0px');
            document.documentElement.style.setProperty('--dynamic-shadow-y', '0px');
            document.documentElement.style.setProperty('--shadow-intensity', '0.5');
        } else {
            // Apply tilt effect based on distance from element center when not hovering
            this.tiltElement.style.transform = `perspective(500px) rotateX(${tiltX}deg) rotateY(${tiltY}deg)`;
        }
    }

    private getCssVariable(variableName: string, element?: HTMLElement): string | null {
        const targetElement = element || document.documentElement;
        const value = getComputedStyle(targetElement).getPropertyValue(variableName).trim();

        return value || null;
    }

    private getCssVariableAsRgb(variableName: string, element?: HTMLElement): { r: number; g: number; b: number } | null {
        const hexValue = this.getCssVariable(variableName, element);

        if (!hexValue) {
            return null;
        }

        return this.hexToRgb(hexValue);
    }

    // Updated gradient initialization using CSS variables
    private initGradientAnimationWithCssVars() {
        const canvas = document.getElementById('gradient-canvas') as HTMLCanvasElement;

        if (!canvas) {
            return; // Exit gracefully if canvas element doesn't exist
        }

        const ctx = canvas.getContext('2d');

        if (!ctx) {
            return; // Exit gracefully if context cannot be obtained
        }

        // Firefox-specific optimizations for smoother gradients
        ctx.imageSmoothingEnabled = true;
        ctx.imageSmoothingQuality = 'high';

        // Set canvas size
        const resizeCanvas = () => {
            canvas.width = window.innerWidth;
            canvas.height = window.innerHeight;
        };

        this.resizeHandler = resizeCanvas;
        resizeCanvas();
        window.addEventListener('resize', this.resizeHandler);

        // Apply Firefox-specific CSS smoothing
        canvas.style.imageRendering = 'auto';
        canvas.style.backfaceVisibility = 'hidden';
        canvas.style.perspective = '1000px';
        canvas.style.transform = 'translateZ(0)';
        canvas.style.willChange = 'transform';

        // Get colors from CSS variables
        const gradientColor1 = this.getCssVariableAsRgb('--gradient-color-1') || { r: 73, g: 197, b: 147 };
        const gradientColor2 = this.getCssVariableAsRgb('--gradient-color-2') || { r: 138, g: 43, b: 226 };
        const gradientColor3 = this.getCssVariableAsRgb('--gradient-color-3') || { r: 255, g: 215, b: 0 };
        const gradientColor4 = this.getCssVariableAsRgb('--gradient-color-4') || { r: 255, g: 20, b: 147 };

        // Gradient points configuration with CSS variable colors
        const gradientPoints = [
            {
                x: 0.2,
                y: 0.2,
                vx: 0.001,
                vy: 0.0015,
                color: gradientColor1
            },
            {
                x: 0.8,
                y: 0.3,
                vx: -0.0015,
                vy: 0.001,
                color: gradientColor2
            },
            {
                x: 0.5,
                y: 0.8,
                vx: 0.0012,
                vy: -0.0018,
                color: gradientColor3
            },
            {
                x: 0.3,
                y: 0.6,
                vx: -0.0018,
                vy: -0.0012,
                color: gradientColor4
            }
        ];

        const animate = () => {
            // Clear canvas with background color from CSS variable
            const canvasBackgroundColor = this.getCssVariable('--elevation-layer2-dark-solid') || '#1f2020';
            ctx.fillStyle = canvasBackgroundColor;
            ctx.fillRect(0, 0, canvas.width, canvas.height);

            // Update point positions
            gradientPoints.forEach(point => {
                point.x += point.vx;
                point.y += point.vy;

                // Bounce off edges
                if (point.x <= 0.1 || point.x >= 0.9) point.vx *= -1;
                if (point.y <= 0.1 || point.y >= 0.9) point.vy *= -1;

                // Keep within bounds
                point.x = Math.max(0.1, Math.min(0.9, point.x));
                point.y = Math.max(0.1, Math.min(0.9, point.y));
            });

            // Create gradients for each point with more color stops for smoother transitions
            gradientPoints.forEach((point, index) => {
                const gradient = ctx.createRadialGradient(
                    point.x * canvas.width,
                    point.y * canvas.height,
                    0,
                    point.x * canvas.width,
                    point.y * canvas.height,
                    canvas.width * 0.5
                );

                gradient.addColorStop(0, `rgba(${point.color.r}, ${point.color.g}, ${point.color.b}, 0.15)`);
                gradient.addColorStop(0.2, `rgba(${point.color.r}, ${point.color.g}, ${point.color.b}, 0.12)`);
                gradient.addColorStop(0.4, `rgba(${point.color.r}, ${point.color.g}, ${point.color.b}, 0.08)`);
                gradient.addColorStop(0.7, `rgba(${point.color.r}, ${point.color.g}, ${point.color.b}, 0.03)`);
                gradient.addColorStop(1, `rgba(${point.color.r}, ${point.color.g}, ${point.color.b}, 0)`);

                ctx.globalCompositeOperation = 'source-over';
                ctx.fillStyle = gradient;
                ctx.fillRect(0, 0, canvas.width, canvas.height);
            });

            this.animationId = requestAnimationFrame(animate);
        };

        animate();
    }

    private hexToRgb(hex: string): { r: number; g: number; b: number } | null {
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
}