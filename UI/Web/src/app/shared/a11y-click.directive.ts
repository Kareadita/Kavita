import { Directive, Input, HostListener, OnInit, ElementRef, Inject } from '@angular/core';

@Directive({
  selector: '[a11y-click]'
})
export class A11yClickDirective {
  @Input('a11y-click') keyCodes!: string;
  keyCodeArray!: string[];
  
  constructor(@Inject(ElementRef) private element : ElementRef){}
  
  ngOnInit(){
    if(this.keyCodes) {
      this.keyCodeArray = this.keyCodes.split(',');
    }
  }
  
  @HostListener('keydown', ['$event'])
  onEvent(event: any) {
    var keyCodeCondition = function (that: any) {
      var flag = false;
      if (!(event.keyCode)) {
        if (event.which) {
          event.keyCode = event.which; 
        } else if (event.charCode) {
          event.keyCode = event.charCode;
        }
      }
      if ((event.keyCode && that.keyCodeArray.indexOf(event.keyCode.toString()) > -1)) {
        flag = true;
      }
      return flag;
    };
    const that = this;
    if (this.keyCodeArray.length > 0 && keyCodeCondition(that)) {
      this.element.nativeElement.click();
      event.preventDefault();
    }
    
  }

}
