import type { FaultCode, PublicFault } from '../contracts/notes.ts';
export class Fault extends Error implements PublicFault {
    readonly code: FaultCode;
    readonly fieldErrors?: Record<string, string>;
    constructor(code: FaultCode, message: string, fieldErrors?: Record<string, string>) {
        super(message);
        this.name = 'Fault';
        this.code = code;
        this.fieldErrors = fieldErrors;
    }
}
