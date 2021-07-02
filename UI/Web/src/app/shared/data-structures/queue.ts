export class Queue<T> {
    elements: T[];
  
    constructor() {
      this.elements = [];
    }
  
    enqueue(data: T) {
      this.elements.push(data);
    }
  
    dequeue() {
      return this.elements.shift();
    }
  
    isEmpty() {
      return this.elements.length === 0;
    }
  
    peek() {
      return !this.isEmpty() ? this.elements[0] : undefined;
    }
  
    length = () => {
      return this.elements.length;
    }
  }