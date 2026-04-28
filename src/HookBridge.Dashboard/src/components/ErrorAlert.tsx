import type { JSX } from 'react';

type ErrorAlertProps = {
  message: string;
  traceId?: string | null;
  validationErrors?: Record<string, string[]>;
};

const ErrorAlert = ({ message, traceId, validationErrors }: ErrorAlertProps): JSX.Element | null => {
  if (!message.trim() && !traceId && (!validationErrors || Object.keys(validationErrors).length === 0)) {
    return null;
  }

  return (
    <div className="rounded-md border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700">
      {message ? <p>{message}</p> : null}
      {validationErrors && Object.keys(validationErrors).length > 0 ? (
        <ul className="mt-2 list-disc space-y-1 pl-5">
          {Object.entries(validationErrors).map(([field, errors]) => (
            <li key={field}>
              <span className="font-semibold">{field}</span>: {errors.join(' ')}
            </li>
          ))}
        </ul>
      ) : null}
      {traceId ? <p className="mt-2 text-xs text-slate-500">Trace ID: {traceId}</p> : null}
    </div>
  );
};

export default ErrorAlert;
