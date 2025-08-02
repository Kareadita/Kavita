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
        'aria-label': 'Login splash screen'
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
        // Initialize CSS variables with default values
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
        
        // Batch set all variables at once to minimize DOM operations
        this.cssVariableService.setVariablesBatch(defaultVariables);
    }

    ngAfterViewInit() {
        // Use requestAnimationFrame for better performance and Zone.js integration
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
            console.log('Initializing animations...');
            console.log('Canvas element:', this.gradientCanvas?.nativeElement);
            console.log('Tilt element:', this.tiltElement?.nativeElement);
            
            const isReducedMotion = this.tiltService.shouldReduceMotion();
            console.log('Reduced motion detected:', isReducedMotion);

            // Always initialize gradient animation (but slower for reduced motion)
            if (this.gradientCanvas?.nativeElement) {
                console.log('Starting gradient animation');
                this.gradientService.startAnimation(this.gradientCanvas.nativeElement, isReducedMotion);
            } else {
                console.warn('Gradient canvas element not found');
            }

            // Only initialize tilt tracking if reduced motion is not enabled
            if (!isReducedMotion && this.tiltElement?.nativeElement) {
                console.log('Starting tilt tracking');
                this.tiltService.initializeMouseTracking(this.tiltElement.nativeElement);
            } else if (isReducedMotion) {
                console.log('Skipping tilt tracking due to reduced motion preference');
            } else {
                console.warn('Tilt element not found');
            }
        } catch (error) {
            console.error('Error initializing animations:', error);
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
            console.error('Error during service cleanup:', error);
        }
    }

    private initializeAnimationsFallback(): void {
        console.log('Trying fallback initialization...');
        
        const isReducedMotion = this.tiltService.shouldReduceMotion();
        
        // Try to find elements by ID as fallback
        const canvas = document.getElementById('gradient-canvas') as HTMLCanvasElement;
        const tiltElement = document.querySelector('.tilt') as HTMLElement;
        
        if (canvas && !this.gradientService.isAnimating()) {
            console.log('Fallback: Starting gradient animation');
            this.gradientService.startAnimation(canvas, isReducedMotion);
        }
        
        if (tiltElement && !isReducedMotion) {
            console.log('Fallback: Starting tilt tracking');
            this.tiltService.initializeMouseTracking(tiltElement);
        }
    }
}