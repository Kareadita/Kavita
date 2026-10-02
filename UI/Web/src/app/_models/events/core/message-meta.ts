import {SignalRMessage} from './signalr-message';

export type MessageMeta = Omit<SignalRMessage, 'body'>;
