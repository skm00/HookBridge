import type { InputHTMLAttributes, JSX } from 'react';

const Input = ({ className = '', ...props }: InputHTMLAttributes<HTMLInputElement>): JSX.Element => {
  return <input className={`w-full rounded-lg border border-border bg-white px-3 py-2 text-sm text-text shadow-sm placeholder:text-text-muted focus-ring ${className}`} {...props} />;
};

export default Input;
