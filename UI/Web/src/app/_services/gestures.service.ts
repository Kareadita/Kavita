let swipeCoord = [0, 0];
let swipeTime = new Date().getTime();

export function swipe(e: TouchEvent, when: string): string | undefined {

    const coord: [number, number] = [e.changedTouches[0].clientX, e.changedTouches[0].clientY];
    const time = new Date().getTime();

    if (when === 'start') {
        swipeCoord = coord;
        swipeTime = time;
    } else if (when === 'end') {
        const direction = [coord[0] - swipeCoord[0], coord[1] - swipeCoord[1]];
        const duration = time - swipeTime;

        if (duration < 1000 //
            && Math.abs(direction[0]) > 30 // Long enough
            && Math.abs(direction[0]) > Math.abs(direction[1] * 3)) { // Horizontal enough
            return (direction[0] < 0 ? 'right' : 'left');
        }
    }
    return;
}