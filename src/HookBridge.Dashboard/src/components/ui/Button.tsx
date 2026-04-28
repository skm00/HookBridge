import type { ButtonHTMLAttributes, JSX } from 'react';

type ButtonVariant = 'primary' | 'secondary' | 'ghost' | 'danger';
type ButtonSize = 'sm' | 'md';

type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: ButtonVariant;
  size?: ButtonSize;
};

const variantClasses: Record<ButtonVariant, string> = {
  primary: 'bg-primary text-white hover:bg-primary-dark',
  secondary: 'border border-border bg-surface text-text transition hover:bg-slate-100',
  ghost: 'text-text-muted hover:bg-slate-100 hover:text-text',
  danger: 'bg-error text-white hover:bg-red-700'
};

const sizeClasses: Record<ButtonSize, string> = {
  sm: 'px-3 py-2 text-sm',
  md: 'px-4 py-2.5 text-sm'
};

const Button = ({ variant = 'primary', size = 'md', className = '', type = 'button', ...props }: ButtonProps): JSX.Element => {
  return (
    <button
      type={type}
      className={`inline-flex items-center justify-center rounded-lg font-medium shadow-sm transition focus-ring disabled:cursor-not-allowed disabled:opacity-60 ${variantClasses[variant]} ${sizeClasses[size]} ${className}`}
      {...props}
    />
  );
};

export default Button;
