function randomHex(bytes: number): string {
  const buffer = new Uint8Array(bytes);
  crypto.getRandomValues(buffer);
  return Array.from(buffer, (b) => b.toString(16).padStart(2, '0')).join('');
}

/** A new W3C trace context: the trace id is the request reference shown to the user. */
export function newTrace(): { traceId: string; traceparent: string } {
  const traceId = randomHex(16);
  return { traceId, traceparent: `00-${traceId}-${randomHex(8)}-01` };
}
