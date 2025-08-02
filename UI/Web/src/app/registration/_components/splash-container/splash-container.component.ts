import {ChangeDetectionStrategy, Component, ElementRef, inject, OnInit, OnDestroy, ViewChild, AfterViewInit, ChangeDetectorRef} from '@angular/core';
import {AsyncPipe, NgStyle} from "@angular/common";
import {NavService} from "../../../_services/nav.service";
import {GradientAnimationService} from "../../../_services/gradient-animation.service";
import {TiltService} from "../../../_services/tilt.service";
import {CssVariableService} from "../../../_services/css-variable.service";

@Component({
    selector: 'app-splash-container',
    templateUrl: './splash-container.component.html',
    styleUrls: ['./splash-container.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [
        NgStyle,
        AsyncPipe
    ],
    host: {
        'role': 'main',
        'aria-label': 'Login page with animated background'
    }
})
export class SplashContainerComponent implements OnInit, OnDestroy, AfterViewInit {
    protected readonly navService = inject(NavService);
    private readonly gradientService = inject(GradientAnimationService);
    private readonly tiltService = inject(TiltService);
    private readonly cssVariableService = inject(CssVariableService);
    private readonly cdr = inject(ChangeDetectorRef);
    
    @ViewChild('gradientCanvas') 
    gradientCanvas!: ElementRef<HTMLCanvasElement>;
    
    @ViewChild('tiltElement') 
    tiltElement!: ElementRef<HTMLElement>;

    ngOnInit() {
        this.initializeCssVariables();
    }
    
    private initializeCssVariables(): void {
        // Batch CSS variable initialization for better performance
        const defaultVariables = {
            '--shine-pos-x': '50%',
            '--shine-pos-y': '50%',
            '--dynamic-shadow-x': '0px',
            '--dynamic-shadow-y': '0px',
            '--dynamic-shadow-blur': '4px',
            '--dynamic-shadow-spread': '0px',
            '--dynamic-shadow-color': 'rgba(0, 0, 0, 0.1)',
            '--dynamic-shadow-color-intense': 'rgba(0, 0, 0, 0.2)',
            '--dynamic-shadow-color-button': 'rgba(0, 0, 0, 0.1)',
            '--dynamic-shadow-color-primary': 'rgba(74, 198, 148, 0.3)',
            '--shadow-intensity': '0.5'
        };
        
        this.cssVariableService.setVariablesBatch(defaultVariables);
    }

    ngAfterViewInit() {
        requestAnimationFrame(() => {
            this.initializeAnimations();
        });
    }

    ngOnDestroy() {
        this.gradientService.stopAnimation();
        this.tiltService.cleanup();
    }

    private initializeAnimations(): void {
        try {
            const isReducedMotion = this.tiltService.shouldReduceMotion();
            
            // For maximum accessibility, offer completely static gradients for reduced motion
            // You can change this to `false` if you want very slow animation instead
            const useStaticGradients = isReducedMotion; 

            // Initialize gradient animation
            if (this.gradientCanvas?.nativeElement) {
                this.gradientService.startAnimation(
                    this.gradientCanvas.nativeElement, 
                    isReducedMotion, 
                    useStaticGradients
                );
            }

            // Initialize tilt tracking only if reduced motion is not enabled
            if (!isReducedMotion && this.tiltElement?.nativeElement) {
                this.tiltService.initializeMouseTracking(this.tiltElement.nativeElement);
            }
        } catch (error) {
            // Clean up any partially initialized services
            this.cleanupServices();
            // Fallback: try to initialize with a delay
            setTimeout(() => {
                this.initializeAnimationsFallback();
            }, 500);
        }
    }
    
    private cleanupServices(): void {
        try {
            this.gradientService.stopAnimation();
            this.tiltService.cleanup();
        } catch (error) {
            // Silent cleanup - errors here are not critical
        }
    }

    private initializeAnimationsFallback(): void {
        const isReducedMotion = this.tiltService.shouldReduceMotion();
        const useStaticGradients = isReducedMotion;
        
        // Try to find elements by ID as fallback
        const canvas = document.getElementById('gradient-canvas') as HTMLCanvasElement;
        const tiltElement = document.querySelector('.tilt') as HTMLElement;
        
        if (canvas && !this.gradientService.isAnimating()) {
            this.gradientService.startAnimation(canvas, isReducedMotion, useStaticGradients);
        }
        
        if (tiltElement && !isReducedMotion) {
            this.tiltService.initializeMouseTracking(tiltElement);
        }
    }
}