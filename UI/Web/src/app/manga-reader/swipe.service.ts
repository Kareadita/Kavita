let swipeCoord = [0, 0];
let swipeTime = new Date().getTime();

export function swipe(e: TouchEvent, when: 'start' | 'end'): 'right' | 'left' | 'up' | 'down' | undefined {
    if (e.changedTouches.length < 1) return;
    const coord: [number, number] = [e.changedTouches[0].clientX, e.changedTouches[0].clientY];
    const time = new Date().getTime();
    if (when === 'start') {
        swipeCoord = coord;
        swipeTime = time;
    } else if (when === 'end') {
        const direction = [coord[0] - swipeCoord[0], coord[1] - swipeCoord[1]];
        const duration = time - swipeTime;
        if (duration > 1000) return; // Swipe lasts at most 1000ms
        if (Math.abs(direction[0]) > 30  // Swipe is long enough
            && Math.abs(direction[0]) > Math.abs(direction[1] * 3)) { // Swipe is horizontal enough
            return (direction[0] < 0 ? 'right' : 'left');
        }
        else if (Math.abs(direction[1]) > 30  // Swipe is long enough
            && Math.abs(direction[1]) > Math.abs(direction[0] * 3)) { // Swipe is vertical enough
                return (direction[1] < 0 ? 'up' : 'down');
        }
    }
    return;
}
