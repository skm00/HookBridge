import type { JSX } from 'react';

type LoadingSpinnerProps = {
  size?: 'sm' | 'md' | 'lg';
  label?: string;
};

const sizeClasses: Record<NonNullable<LoadingSpinnerProps['size']>, string> = {
  sm: 'h-4 w-4 border-2',
  md: 'h-6 w-6 border-2',
  lg: 'h-8 w-8 border-[3px]'
};

const LoadingSpinner = ({ size = 'md', label = 'Loading' }: LoadingSpinnerProps): JSX.Element => {
  return (
    <span className="inline-flex items-center gap-2 text-sm text-slate-600" role="status" aria-live="polite">
      <span
        className={`inline-block animate-spin rounded-full border-slate-300 border-t-brand-600 ${sizeClasses[size]}`}
        aria-label={label}
      />
      <span>{label}</span>
    </span>
  );
};

export default LoadingSpinner;
