import type { JSX, ReactNode } from 'react';

type BadgeTone = 'neutral' | 'success' | 'warning' | 'error' | 'brand';

type BadgeProps = {
  children: ReactNode;
  tone?: BadgeTone;
  className?: string;
};

const toneClasses: Record<BadgeTone, string> = {
  neutral: 'bg-slate-100 text-slate-700 border-slate-200',
  success: 'bg-success-bg text-success border-success-border',
  warning: 'bg-warning-bg text-warning border-warning-border',
  error: 'bg-error-bg text-error border-error-border',
  brand: 'bg-primary-soft text-primary-dark border-primary-border'
};

const Badge = ({ children, tone = 'neutral', className = '' }: BadgeProps): JSX.Element => {
  return <span className={`inline-flex items-center rounded-full border px-2.5 py-1 text-xs font-semibold ${toneClasses[tone]} ${className}`}>{children}</span>;
};

export default Badge;
