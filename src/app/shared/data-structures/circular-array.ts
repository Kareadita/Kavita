export class CircularArray<T> {
    arr: T[];
    currentIndex: number;
  
    constructor(arr: T[], startIndex: number) {
      this.arr = arr;
      this.currentIndex = startIndex || 0;
    }
  
    next() {
      const i = this.currentIndex;
      const arr = this.arr;
      this.currentIndex = i < arr.length - 1 ? i + 1 : 0;
      return this.current();
    }
  
    prev() {
      const i = this.currentIndex;
      const arr = this.arr;
      this.currentIndex = i > 0 ? i - 1 : arr.length - 1;
      return this.current();
    }
  
    current() {
      return this.arr[this.currentIndex];
    }
  
    peek(offset: number = 0) {
      const i = this.currentIndex + 1 + offset;
      const arr = this.arr;
      const peekIndex = i < arr.length - 1 ? i + 1 : 0;
      return this.arr[peekIndex];
    }
  
    size() {
      return this.arr.length;
    }
  
    applyUntil(func: (item: T, index: number) => void, index?: number) {
      index = index || this.currentIndex;
      /// Applies a func against elements up until index. If index is 1 and size is 3, will apply on [2, 3, 0]
      for (let offset = 1; offset < this.size(); offset++) {
        const i = this.currentIndex + offset;
        const arr = this.arr;
        const peekIndex = i < arr.length ? i : 0;
  
        if (peekIndex === index) {
          break;
        }
  
        func(this.arr[peekIndex], peekIndex);
      }
  
    }
  
    /// Applies a func against elements for X times. If limit is 1, size is 3, and index is 2. It will apply on [3]
    applyFor(func: (item: T, index: number) => void, limit: number) {
      for (let offset = 1; offset < limit; offset++) {
        const i = this.currentIndex + offset;
        const peekIndex = i < this.arr.length ? i : 0;
  
        func(this.arr[peekIndex], peekIndex);
      }
  
    }
  
  
  }