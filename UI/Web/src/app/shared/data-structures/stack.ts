export class Stack<T> {
    items: T[];
  
    constructor() {
      this.items = [];
    }
  
    isEmpty() {
      return this.items.length === 0;
    }
  
    peek() {
      if (!this.isEmpty()) {
        return this.items[this.items.length - 1];
      }
      return undefined;
    }
  
    pop() {
      if (this.isEmpty()) {
        return undefined;
      }
      return this.items.pop();
    }
  
    push(item: T) {
      this.items.push(item);
    }
  }