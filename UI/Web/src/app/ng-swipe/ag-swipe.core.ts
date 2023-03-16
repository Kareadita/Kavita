import { fromEvent, Observable, race, Subscription } from 'rxjs';
import { elementAt, map, switchMap, takeUntil, tap } from 'rxjs/operators';

export interface SwipeCoordinates {
    x: number;
    y: number;
  }
  
  export enum SwipeDirection {
    X = 'x',
    Y = 'y'
  }
  
  export interface SwipeStartEvent {
    x: number;
    y: number;
    direction: SwipeDirection;
  }
  
  export interface SwipeEvent {
    direction: SwipeDirection;
    distance: number;
  }
  
  export interface SwipeSubscriptionConfig {
    domElement: HTMLElement;
    onSwipeMove?: (event: SwipeEvent) => void;
    onSwipeEnd?: (event: SwipeEvent) => void;
  }


export function createSwipeSubscription({ domElement, onSwipeMove, onSwipeEnd }: SwipeSubscriptionConfig): Subscription {
  if (!(domElement instanceof HTMLElement)) {
    throw new Error('Provided domElement should be an instance of HTMLElement');
  }

  if ((typeof onSwipeMove !== 'function') && (typeof onSwipeEnd !== 'function')) {
    throw new Error('At least one of the following swipe event handler functions should be provided: onSwipeMove and/or onSwipeEnd');
  }

  const touchStarts$ = fromEvent<TouchEvent>(domElement, 'touchstart').pipe(map(getTouchCoordinates));
  const touchMoves$ = fromEvent<TouchEvent>(domElement, 'touchmove').pipe(map(getTouchCoordinates));
  const touchEnds$ = fromEvent<TouchEvent>(domElement, 'touchend').pipe(map(getTouchCoordinates));
  const touchCancels$ = fromEvent<TouchEvent>(domElement, 'touchcancel');

  const touchStartsWithDirection$: Observable<SwipeStartEvent> = touchStarts$.pipe(
    switchMap((touchStartEvent: SwipeCoordinates) => touchMoves$.pipe(
      elementAt(3),
      map((touchMoveEvent: SwipeCoordinates) => ({
          x: touchStartEvent.x,
          y: touchStartEvent.y,
          direction: getTouchDirection(touchStartEvent, touchMoveEvent)
        })
      ))
    )
  );

  return touchStartsWithDirection$.pipe(
    switchMap(touchStartEvent => touchMoves$.pipe(
      map(touchMoveEvent => getTouchDistance(touchStartEvent, touchMoveEvent)),
      tap((coordinates: SwipeCoordinates) => {
        if (typeof onSwipeMove !== 'function') { return; }
        onSwipeMove(getSwipeEvent(touchStartEvent, coordinates));
      }),
      takeUntil(race(
        touchEnds$.pipe(
          map(touchEndEvent => getTouchDistance(touchStartEvent, touchEndEvent)),
          tap((coordinates: SwipeCoordinates) => {
            if (typeof onSwipeEnd !== 'function') { return; }
            onSwipeEnd(getSwipeEvent(touchStartEvent, coordinates));
          })
        ),
        touchCancels$
      ))
    ))
  ).subscribe();
}

function getTouchCoordinates(touchEvent: TouchEvent): SwipeCoordinates  {
  return {
    x: touchEvent.changedTouches[0].clientX,
    y: touchEvent.changedTouches[0].clientY
  };
}

function getTouchDistance(startCoordinates: SwipeCoordinates, moveCoordinates: SwipeCoordinates): SwipeCoordinates {
  return {
    x: moveCoordinates.x - startCoordinates.x,
    y: moveCoordinates.y - startCoordinates.y
  };
}

function getTouchDirection(startCoordinates: SwipeCoordinates, moveCoordinates: SwipeCoordinates): SwipeDirection {
  const { x, y } = getTouchDistance(startCoordinates, moveCoordinates);
  return Math.abs(x) < Math.abs(y) ? SwipeDirection.Y : SwipeDirection.X;
}

function getSwipeEvent(touchStartEvent: SwipeStartEvent, coordinates: SwipeCoordinates): SwipeEvent  {
  return {
    direction: touchStartEvent.direction,
    distance: coordinates[touchStartEvent.direction]
  };
}
