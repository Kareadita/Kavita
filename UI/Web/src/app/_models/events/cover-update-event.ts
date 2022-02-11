/**
 * Represents a generic cover update event. Id is used based on entityType
 */
export interface CoverUpdateEvent {
    id: number;
    entityType: 'series' | 'chapter' | 'volume' | 'collectionTag';
}