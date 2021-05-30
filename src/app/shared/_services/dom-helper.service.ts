import { ElementRef, Injectable } from '@angular/core';

@Injectable({
    providedIn: 'root'
})
export class DomHelperService {

	constructor() {}
	// from: https://stackoverflow.com/questions/40597658/equivalent-of-angular-equals-in-angular2#44649659
	deepEquals(x: any, y: any) {
	  if (x === y) {
	    return true; // if both x and y are null or undefined and exactly the same
	  } else if (!(x instanceof Object) || !(y instanceof Object)) {
	    return false; // if they are not strictly equal, they both need to be Objects
	  } else if (x.constructor !== y.constructor) {
	    // they must have the exact same prototype chain, the closest we can do is
	    // test their constructor.
	    return false;
	  } else {
	    for (const p in x) {
	      if (!x.hasOwnProperty(p)) {
	        continue; // other properties were tested using x.constructor === y.constructor
	      }
	      if (!y.hasOwnProperty(p)) {
	        return false; // allows to compare x[ p ] and y[ p ] when set to undefined
	      }
	      if (x[p] === y[p]) {
	        continue; // if they have the same strict value or identity then they are equal
	      }
	      if (typeof (x[p]) !== 'object') {
	        return false; // Numbers, Strings, Functions, Booleans must be strictly equal
	      }
	      if (!this.deepEquals(x[p], y[p])) {
	        return false;
	      }
	    }
	    for (const p in y) {
	      if (y.hasOwnProperty(p) && !x.hasOwnProperty(p)) {
	        return false;
	      }
	    }
	    return true;
	  }
	}	

	isHidden(node: ElementRef){
		const el = node.nativeElement?node.nativeElement:node;
		const elemStyle = window.getComputedStyle(el);

		return el.style.display === 'none' || elemStyle.visibility === 'hidden' || el.hasAttribute('hidden') || elemStyle.display === 'none';
	}

	isTabable(node: ElementRef): boolean {
		const el = node.nativeElement?node.nativeElement:node;
		const tagName = el.tagName;

		if(this.isHidden(node)){
			return false;
		}
		// el.attribute:NamdedNodeMap
		if (el.attributes.hasOwnProperty('tabindex')) {
			return (parseInt(el.attributes.getNamedItem('tabindex'),10) >= 0);
		}
		if (tagName === 'A' || tagName === 'AREA' || tagName === 'BUTTON' || tagName === 'INPUT' || tagName === 'TEXTAREA' || tagName === 'SELECT') {
			if (tagName === 'A' || tagName === 'AREA') {
                return (el.attributes.getNamedItem('href') !== '');
            }
            return !el.attributes.hasOwnProperty('disabled'); // TODO: check for cases when: disabled="true" and disabled="false"
        }
        return false;
	}

	private isValidChild(child: any): boolean { // child:ElementRef.nativeElement
		return child.nodeType == 1 && child.nodeName != 'SCRIPT' && child.nodeName != 'STYLE';
	}

    private hasValidParent(obj: any) { // obj:ElementRef.nativeElement
        return (this.isValidChild(obj) && obj.parentElement.nodeName !== 'BODY');
    }

    private traverse(obj: any, fromTop: boolean): ElementRef | undefined | boolean {
		 // obj:ElementRef||ElementRef.nativeElement
        var obj = obj? (obj.nativeElement?obj.nativeElement:obj) : document.getElementsByTagName('body')[0];
        if (this.isValidChild(obj) && this.isTabable(obj)) {
            return obj;
        }
        // If object is hidden, skip it's children
        if (this.isValidChild(obj) && this.isHidden(obj)) {
            return undefined;
        } 
        // If object is hidden, skip it's children
        if (obj.classList && obj.classList.contains('ng-hide')) { // some nodes don't have classList?!
            return false;
        }  
        if (obj.hasChildNodes()) {
            var child;
            if (fromTop) {
                child = obj.firstChild;
            } else {
                child = obj.lastChild;
            }
            while(child) {
                var res =  this.traverse(child, fromTop);
                if(res){
                    return res;
                }
                else{
                    if (fromTop) {
                        child = child.nextSibling;
                    } else {
                        child = child.previousSibling;
                    }
                }
            }
        }
        else{
            return undefined;
        }
    } 
    previousElement(el: any, isFocusable: boolean): any { // ElementRef | undefined | boolean

        var elem = el.nativeElement ? el.nativeElement : el;
        if (el.hasOwnProperty('length')) {
            elem = el[0];
        }

        var parent = elem.parentElement;
        var previousElem = undefined;

        if(isFocusable) {
            if (this.hasValidParent(elem)) {
                var siblings = parent.children;
                if (siblings.length > 0) {
                    // Good practice to splice out the elem from siblings if there, saving some time.
                    // We allow for a quick check for jumping to parent first before removing. 
                    if (siblings[0] === elem) {
                        // If we are looking at immidiate parent and elem is first child, we need to go higher
                        var e = this.previousElement(elem.parentNode, isFocusable);
                        if (this.isTabable(e)) {
                            return e;
                        }
                    } else {
                        // I need to filter myself and any nodes next to me from the siblings
                        var indexOfElem = Array.prototype.indexOf.call(siblings, elem);
                        const that = this;
                        siblings = Array.prototype.filter.call(siblings, function(item, itemIndex) {
                            if (!that.deepEquals(elem, item) && itemIndex < indexOfElem) {
                                return true;
                            }
                        });
                    }
                    // We need to search backwards
                    for (var i = 0; i <= siblings.length-1; i++) {//for (var i = siblings.length-1; i >= 0; i--) {
                        var ret = this.traverse(siblings[i], false);
                        if (ret !== undefined) {
                            return ret;
                        }
                    }

                    var e = this.previousElement(elem.parentNode, isFocusable);
                    if (this.isTabable(e)) {
                        return e;
                    }
                }
            }
        } else {
            var siblings = parent.children;
            if (siblings.length > 1) {
                // Since indexOf is on Array.prototype and parent.children is a NodeList, we have to use call()
                var index = Array.prototype.indexOf.call(siblings, elem);
                previousElem = siblings[index-1];
            }
        }
        return previousElem;
    };
    lastTabableElement(el: any) {
        /* This will return the first tabable element from the parent el */
        var elem = el.nativeElement?el.nativeElement:el;
        if (el.hasOwnProperty('length')) {
            elem = el[0];
        }

        return this.traverse(elem, false);
    };

    firstTabableElement(el: any) {
        /* This will return the first tabable element from the parent el */
        var elem = el.nativeElement ? el.nativeElement : el;
        if (el.hasOwnProperty('length')) {
            elem = el[0];
        }

        return this.traverse(elem, true);
    };

    isInDOM(obj: Node) {
      return document.documentElement.contains(obj);
    }       

}
