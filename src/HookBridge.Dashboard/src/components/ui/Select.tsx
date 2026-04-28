import type { JSX, SelectHTMLAttributes } from 'react';

const Select = ({ className = '', ...props }: SelectHTMLAttributes<HTMLSelectElement>): JSX.Element => {
  return <select className={`w-full rounded-lg border border-border bg-white px-3 py-2 text-sm text-text shadow-sm focus-ring ${className}`} {...props} />;
};

export default Select;
