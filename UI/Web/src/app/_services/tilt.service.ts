import { Injectable } from '@angular/core';
import { CssVariableService } from './css-variable.service';
import { fromEvent, Subject, takeUntil, throttleTime } from 'rxjs';

export interface MousePosition {
    x: number;
    y: number;
}

export interface ElementBounds {
    left: number;
    top: number;
    width: number;
    height: number;
    right: number;
    bottom: number;
}

@Injectable({
    providedIn: 'root'
})
export class TiltService {
    private readonly destroy$ = new Subject<void>();
    private readonly maxTilt = 5; // Maximum tilt angle in degrees
    private elementBounds?: ElementBounds;
    private lastMousePosition?: MousePosition;
    private isHovering = false;
    
    constructor(private cssVariableService: CssVariableService) {}
    
    // Method to cleanup when component is destroyed
    cleanup(): void {
        this.destroy$.next();
        this.destroy$.complete();
        
        // Reset any elements that might have been tilted
        this.resetElementTransform();
    }
    
    private resetElementTransform(): void {
        // Reset CSS variables to default values
        this.cssVariableService.setVariable('--shine-pos-x', '50%');
        this.cssVariableService.setVariable('--shine-pos-y', '50%');
        this.cssVariableService.setVariable('--dynamic-shadow-x', '0px');
        this.cssVariableService.setVariable('--dynamic-shadow-y', '0px');
        this.cssVariableService.setVariable('--shadow-intensity', '0.5');
        
        // Reset element bounds
        this.elementBounds = undefined;
        this.lastMousePosition = undefined;
        this.isHovering = false;
    }
    
    initializeMouseTracking(element: HTMLElement): void {
        console.log('TiltService: Initializing mouse tracking for element:', element);
        if (!element) return;
        
        // Reset element transform before starting
        element.style.transform = '';
        
        // Cache element bounds
        this.updateElementBounds(element);
        console.log('TiltService: Element bounds cached:', this.elementBounds);
        
        // Use RxJS for better event handling with passive listeners
        fromEvent<PointerEvent>(document, 'pointermove', { passive: true })
            .pipe(
                throttleTime(16), // ~60fps
                takeUntil(this.destroy$)
            )
            .subscribe(event => {
                this.handlePointerMove(event, element);
            });
        
        // Handle pointer leave
        fromEvent<PointerEvent>(document, 'pointerleave', { passive: true })
            .pipe(takeUntil(this.destroy$))
            .subscribe(() => {
                this.handlePointerLeave();
            });
        
        // Update bounds on resize with passive listener
        fromEvent(window, 'resize', { passive: true })
            .pipe(
                throttleTime(100),
                takeUntil(this.destroy$)
            )
            .subscribe(() => {
                this.updateElementBounds(element);
            });
    }
    
    private updateElementBounds(element: HTMLElement): void {
        const bounds = element.getBoundingClientRect();
        this.elementBounds = {
            left: bounds.left,
            top: bounds.top,
            width: bounds.width,
            height: bounds.height,
            right: bounds.right,
            bottom: bounds.bottom
        };
    }
    
    private handlePointerMove(event: PointerEvent, element: HTMLElement): void {
        // Check if element is still in the DOM
        if (!document.contains(element)) {
            console.warn('Element no longer in DOM, stopping tilt tracking');
            this.cleanup();
            return;
        }
        
        if (!this.elementBounds) {
            this.updateElementBounds(element);
        }
        
        const mouseX = event.clientX;
        const mouseY = event.clientY;
        
        this.lastMousePosition = { x: mouseX, y: mouseY };
        
        // Check if mouse is over the element
        this.isHovering = this.isMouseOverElement(mouseX, mouseY);
        
        // Always calculate shine position relative to the element
        const relativeX = mouseX - this.elementBounds!.left;
        const relativeY = mouseY - this.elementBounds!.top;
        
        // Convert to percentage and clamp to keep shine within element bounds (0-100%)
        const shineX = Math.max(0, Math.min(100, (relativeX / this.elementBounds!.width) * 100));
        const shineY = Math.max(0, Math.min(100, (relativeY / this.elementBounds!.height) * 100));
        
        // Apply clamped shine position to keep it within element
        this.cssVariableService.setVariable('--shine-pos-x', `${Math.round(shineX)}%`);
        this.cssVariableService.setVariable('--shine-pos-y', `${Math.round(shineY)}%`);
        
        // Calculate tilt values for shadow effects
        const centerX = this.elementBounds!.left + this.elementBounds!.width / 2;
        const centerY = this.elementBounds!.top + this.elementBounds!.height / 2;
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
        this.cssVariableService.setVariable('--dynamic-shadow-x', `${shadowOffsetX}px`);
        this.cssVariableService.setVariable('--dynamic-shadow-y', `${shadowOffsetY}px`);
        this.cssVariableService.setVariable('--shadow-intensity', shadowIntensity.toString());
        
        // Apply tilt effect based on hover state
        if (this.isHovering) {
            // Reset tilt to zero when hovering over element
            element.style.transform = 'perspective(500px) rotateX(0deg) rotateY(0deg)';
            // Reset shadows when hovering
            this.cssVariableService.setVariable('--dynamic-shadow-x', '0px');
            this.cssVariableService.setVariable('--dynamic-shadow-y', '0px');
            this.cssVariableService.setVariable('--shadow-intensity', '0.5');
        } else {
            // Apply tilt effect based on distance from element center when not hovering
            element.style.transform = `perspective(500px) rotateX(${tiltX}deg) rotateY(${tiltY}deg)`;
        }
    }
    
    private handlePointerLeave(): void {
        // Reset to default state when pointer leaves the document
        this.isHovering = false;
        this.lastMousePosition = undefined;
    }
    
    private isMouseOverElement(mouseX: number, mouseY: number): boolean {
        if (!this.elementBounds) return false;
        
        return mouseX >= this.elementBounds.left &&
               mouseX <= this.elementBounds.right &&
               mouseY >= this.elementBounds.top &&
               mouseY <= this.elementBounds.bottom;
    }
    
    // Public method to check if should reduce motion (accessibility)
    shouldReduceMotion(): boolean {
        return window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    }
} 